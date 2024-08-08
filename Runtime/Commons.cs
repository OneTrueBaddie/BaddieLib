namespace Baddie.Commons
{
    using System;
    using System.Threading;
    using System.Collections.Concurrent;
    using Baddie.Utils;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Services.Core;
    using System.Collections;
    using Unity.Services.Authentication;
    using Newtonsoft.Json;

#if PHOTON_UNITY_NETWORKING
    using Photon.Pun;
    using Photon.Realtime;
#endif

    [Obsolete("The threading implementation is quite poor, I wouldnt recommend it")]
    public static class Threading
    {
        public struct ThreadedJob
        {
            public Thread Worker;
            public ThreadStart Job;
            public bool Important;
        }

        public static volatile ThreadedJob[] ThreadPool = new ThreadedJob[Environment.ProcessorCount];
        public static volatile ConcurrentQueue<Action> JobQueue = new();
        public static volatile AutoResetEvent JobAvailable = new(false);

        static Threading()
        {
            for (int i = 0; i < ThreadPool.Length; i++)
            {
                ThreadPool[i].Worker = new(BackgroundThread)
                {
                    Name = $"Thread {i + 1}",
                    Priority = System.Threading.ThreadPriority.BelowNormal,
                    IsBackground = true
                };

                ThreadPool[i].Job = BackgroundThread;
                ThreadPool[i].Worker.Start();
            }
        }

        public static void Shutdown()
        {
            string log = "";

            for (var i = 0; i < ThreadPool.Length;i++)
            {
                if (ThreadPool[i].Job == BackgroundThread || !ThreadPool[i].Important)
                {
                    ThreadPool[i].Worker.Abort();
                }
                else if (ThreadPool[i].Worker.ThreadState == ThreadState.Running && ThreadPool[i].Important)
                {
                    var times = 0;

                    while (ThreadPool[i].Worker.ThreadState == ThreadState.Running)
                    {
                        if (times > 25000)
                        {
                            ThreadPool[i].Worker.Abort();
                            break;
                        }

                        times++;
                    }
                }

                log += $"Shutdown thread '{ThreadPool[i].Worker.Name}'\n";
            }

            Debugger.Log(log);
        }

        public static void AddJob(Action job)
        {
            JobQueue.Enqueue(job);
            JobAvailable.Set();
        }

        public static void AssignThread(Action work, bool important = true)
        {
            ThreadedJob thread = ThreadPool.First(t => t.Job == null || t.Job == BackgroundThread);

            if (thread.Worker.ThreadState == ThreadState.Running)
                thread.Worker.Abort();

            void job()
            {
                work.Invoke();

                thread.Important = false;
                thread.Job = BackgroundThread;
                thread.Worker = new(BackgroundThread)
                {
                    Priority = System.Threading.ThreadPriority.BelowNormal,
                    IsBackground = true,
                };

                thread.Worker.Start();
            }

            thread.Worker = new(job)
            {
                Priority = System.Threading.ThreadPriority.AboveNormal,
                IsBackground = false,
            };

            thread.Job = job;    
            thread.Important = important;
            thread.Worker.Start();
        }

        static void BackgroundThread()
        {
            while (true)
            {
                JobAvailable.WaitOne();

                if (JobQueue.TryDequeue(out Action job))
                    job.Invoke();
            }
        }
    }

    public static class ConversionHelper
    {
        /// <summary>
        /// Find and convert the given keys value from an object dictionary
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="d"></param>
        /// <param name="key"></param>
        /// <returns>(T) The converted value of the key if found, default if not</returns>
        public static T FromDictionary<T>(Dictionary<string, object> d, string key) 
        { 
            if (d == null || d.Count == 0)
            {
                Debugger.Log("Cannot get value from dictionary as it is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                return default;
            }

            try
            {
                if (d.TryGetValue(key, out var value))
                {
                    if (value is string str)
                        return FromString<T>(str);

                    return (T)Convert.ChangeType(value, typeof(T));
                }

                return default;
            }
            catch (Exception e)
            {
                Debugger.Log($"Error trying to get value from dictionary with key '{key}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                return default;
            }
        }

        /// <summary>
        /// Convert the given string to T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="str"></param>
        /// <returns>(T) Converted value if successful, default if not</returns>
        public static T FromString<T>(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                Debugger.Log("Cannot convert from string, string is null or empty");
                return default;
            }

            // Some edge cases that cant just be converted from ChangeType
            // Will have to expand this as I run into errors as idk what types would need to be added here
            if (typeof(T) == typeof(byte[]))
                return (T)(object)Convert.FromBase64String(str);

            try
            {
                return (T)Convert.ChangeType(str, typeof(T));
            }
            catch (Exception e)
            {
                Debugger.Log($"Could not convert from string '{str}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                return default;
            }
        }

        /// <summary>
        /// Find the given value between 2 points in a string
        /// </summary>
        /// <param name="original"></param>
        /// <param name="startAt"></param>
        /// <param name="endAt"></param>
        /// <returns>(string) The value if found, null if not</returns>
        public static string FindInString(string original, string startAt, string endAt)
        {
            if (original.Contains(startAt) && original.Contains(endAt))
            {
                var start = original.IndexOf(startAt, 0, StringComparison.Ordinal) + startAt.Length;
                var end = original.IndexOf(endAt, start, StringComparison.Ordinal);

                return start < 0 || end < 0 ? null : original.Substring(start, end - start);
            }

            return null;
        }

        /// <summary>
        /// Find the given value between 2 points in a string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="original"></param>
        /// <param name="startAt"></param>
        /// <param name="endAt"></param>
        /// <returns>(T) The found value converted to T if found, default if not</returns>
        public static T FindInString<T>(string original, string startAt, string endAt)
        {
            if (original.Contains(startAt) && original.Contains(endAt))
            {
                var start = original.IndexOf(startAt, 0, StringComparison.Ordinal) + startAt.Length;
                var end = original.IndexOf(endAt, start, StringComparison.Ordinal);
                var result = original.Substring(start, end - start);

                try
                {
                    return FromString<T>(result);
                }
                catch
                {
                    Debugger.Log($"Could not convert string to type '{typeof(T).Name}'");
                    return default;
                }
            }

            return default;
        }
    }

    public static class Reflection
    {
        /// <summary>
        /// Get all the instances based on the given filter, such as a specific attribute or type
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public static object[] GetInstances(Type attribute)
        {
            List<object> result = new();

            IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && Attribute.IsDefined(type, attribute));

            foreach (var type in types)
            {
                if (type.BaseType == typeof(MonoBehaviour))
                {
                    object[] found = UnityEngine.Object.FindObjectsOfType(type);

                    if (found.Length == 0)
                        continue;

                    foreach (var obj in found)
                        result.Add(obj);
                }
                #if PHOTON_UNITY_NETWORKING
                else if (type.BaseType == typeof(MonoBehaviourPun || type.BaseType == typeof(MonoBehaviourPunCallbacks)) 
                {
                    object[] found = UnityEngine.Object.FindObjectsOfType(type);

                    if (found.Length == 0)
                        continue;

                    foreach (var obj in found)
                        result.Add(obj);
                }
                #endif
                else
                {
                    result.Add(Activator.CreateInstance(type));
                }
            }

            return result.FindAll(x => x != null).ToArray();
        }

        /// <summary>
        /// Get all the instances based on the given filter, such as a specific attribute or type
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public static Task<object[]> GetInstancesAsync(Type attribute)
        {
            return Task.Run(() =>
            {
                List<object> result = new();

                IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type.IsClass && Attribute.IsDefined(type, attribute));

                foreach (var type in types)
                {
                    if (type.BaseType == typeof(MonoBehaviour))
                    {
                        Debugger.Log("You are trying to get a monobehaviour instance from another thread, use the non async method for this instead", LogColour.Yellow, Utils.LogType.Warning);
                        continue;
                    }
                    #if PHOTON_UNITY_NETWORKING
                    if (type.BaseType == typeof(MonoBehaviourPun || type.BaseType == typeof(MonoBehaviourPunCallbacks)) 
                    {
                        Debugger.Log("You are trying to get a monobehaviour instance from another thread, use the non async method for this instead", LogColour.Yellow, Utils.LogType.Warning);
                        continue;
                    }
                    #endif

                    result.Add(Activator.CreateInstance(type));
                }

                return result.FindAll(x => x != null).ToArray();
            });
        }
    }

    public static class UniqueIdentifer
    {
        public static GameObject Instaniate(string name, bool active = true)
        {
            var id = Guid.NewGuid();
            var obj = new GameObject($"{name} #{id}");

            obj.SetActive(active);

            return obj;
        }

        public static GameObject Instaniate(string name, Transform parent, bool active = true)
        {
            var id = Guid.NewGuid();
            var obj = new GameObject($"{name} #{id}");

            obj.transform.SetParent(parent);
            obj.SetActive(active);

            return obj;
        }

        public static GameObject Instaniate(string name, Transform parent, Vector3 pos, bool active = true)
        {
            var id = Guid.NewGuid();
            var obj = new GameObject($"{name} #{id}");

            obj.transform.SetParent(parent);
            obj.transform.position = pos;
            obj.SetActive(active);

            return obj;
        }

        public static GameObject Instaniate(string name, Transform parent, Vector2 pos, bool active = true)
        {
            var id = Guid.NewGuid();
            var obj = new GameObject($"{name} #{id}");

            obj.transform.SetParent(parent);
            obj.transform.position = pos;
            obj.SetActive(active);

            return obj;
        }

        public static GameObject Instaniate(string name, Transform parent, Vector3 pos, bool worldSpace, bool active = true)
        {
            var id = Guid.NewGuid();
            var obj = new GameObject($"{name} #{id}");

            obj.transform.SetParent(parent, worldSpace);
            obj.transform.position = pos;
            obj.SetActive(active);

            return obj;
        }

        public static GameObject Instaniate(string name, Transform parent, Vector2 pos, bool worldSpace, bool active = true)
        {
            var id = Guid.NewGuid();
            var obj = new GameObject($"{name} #{id}");

            obj.transform.SetParent(parent, worldSpace);
            obj.transform.position = pos;
            obj.SetActive(active);

            return obj;
        }

        public static string GetID(GameObject obj) { return obj.name.Split('#')[1]; }
        public static string GetID(string name) { return name.Split('#')[1]; }
    }

    public static class Services
    {
        public static string UUID;
        static bool FirstTime = true;

        public static event Action OnSignIn = () =>
        {
            UUID = AuthenticationService.Instance.PlayerId;
        };
        public static event Action OnSignOut = () =>
        {
            UUID = null;

            ResetEvents();

            Debugger.Log($"Signed out from unity service ({UUID})", LogColour.Yellow);
        };
        public static event Action<RequestFailedException> OnSignInFail = (request) =>
        {

        };

        static Action OnSignInOriginal = OnSignIn;
        static Action OnSignOutOriginal = OnSignOut;
        static Action<RequestFailedException> OnSignInFailOriginal = OnSignInFail;

        /// <summary>
        /// Initialize the unity service
        /// </summary>
        public static Task Setup()
        {
            return UnityServices.State == ServicesInitializationState.Uninitialized ? UnityServices.InitializeAsync() : null;
        }

        /// <summary>
        /// Sign into the unity service
        /// </summary>
        public static Task SignIn()
        {
            if (FirstTime)
            {
                AuthenticationService.Instance.SignedIn += OnSignIn;
                AuthenticationService.Instance.SignedOut += OnSignOut;
                AuthenticationService.Instance.SignInFailed += OnSignInFail;

                FirstTime = false;
            }

            return AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Sign out of the unity service
        /// </summary>
        public static void SignOut()
        {
            if (IsSignedIn())
                AuthenticationService.Instance.SignOut();
        }

        /// <summary>
        /// Sign out of the unity service and clear all credentials
        /// </summary>
        /// <param name="clearCredentials"></param>
        public static void SignOut(bool clearCredentials)
        {
            AuthenticationService.Instance.SignOut(clearCredentials);
        }

        /// <summary>
        /// Change the current players name on Unity Services
        /// </summary>
        /// <param name="name"></param>
        public static async void ChangePlayerName(string name)
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(name);
        }

        /// <summary>
        /// Invoke a job once unity services is initialized, if it already is then the job will instantly invoke
        /// </summary>
        /// <param name="job"></param>
        public static IEnumerator WaitUntilInitialized(Action job)
        {
            yield return new WaitUntil(IsSetup);

            job?.Invoke();
        }

        /// <summary>
        /// Invoke a job once the user is signed in, if they already are then the job will instantly invoke
        /// </summary>
        /// <param name="job"></param>
        public static IEnumerator WaitUntilSignedIn(Action job)
        {
            yield return new WaitUntil(IsSignedIn);

            job?.Invoke();
        }

        /// <summary>
        /// Check if the unity service is initialized
        /// </summary>
        /// <returns>(bool) True if it is, false if not</returns>
        public static bool IsSetup() { return UnityServices.State == ServicesInitializationState.Initialized; }

        /// <summary>
        /// Check if the user is signed in
        /// </summary>
        /// <returns>(bool) true if they are, false if not</returns>
        public static bool IsSignedIn() { return UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn; }

        static void ResetEvents()
        {
            OnSignIn = OnSignInOriginal;
            OnSignOut = OnSignOutOriginal;
            OnSignInFail = OnSignInFailOriginal;

            FirstTime = true;
        }
    }
}
