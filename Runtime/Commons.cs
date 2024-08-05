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

#if PHOTON_UNITY_NETWORKING
    using Photon.Pun;
    using Photon.Realtime;
#endif


    [AttributeUsage(AttributeTargets.All)]
    public class LocalSaveAttribute : Attribute
    {
        public LocalSaveAttribute() { }
    }

    [AttributeUsage(AttributeTargets.All)]
    public class CloudSaveAttribute : Attribute
    {
        public CloudSaveAttribute() { }
    }

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

            ThreadStart job = () =>
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
            };

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

    public static class StringHelper
    {
        public static string FindValueInString(string original, string startAt, string endAt)
        {
            if (original.Contains(startAt) && original.Contains(endAt))
            {
                int start = original.IndexOf(startAt, 0, StringComparison.Ordinal) + startAt.Length;
                int end = original.IndexOf(endAt, start, StringComparison.Ordinal);

                return start < 0 || end < 0 ? null : original.Substring(start, end - start);
            }

            return null;
        }

        public static T FindValueInString<T>(string original, string startAt, string endAt)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(startAt) || string.IsNullOrEmpty(endAt))
                return default;
            if (!original.Contains(startAt) || !original.Contains(endAt))
                return default;

            int start = original.IndexOf(startAt, 0, StringComparison.Ordinal) + startAt.Length;
            int end = original.IndexOf(endAt, start, StringComparison.Ordinal);
            string result = original.Substring(start, end - start);

            try
            {
                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch
            {
                Debugger.Log($"Could not convert string to type '{typeof(T).Name}'");
                return default;
            }
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
                object instance = null;

                if (type.BaseType == typeof(MonoBehaviour))
                {
                    object[] found = UnityEngine.Object.FindObjectsOfType(type);

                    if (found.Length > 0)
                        instance = found.First(t => Attribute.IsDefined(t.GetType(), attribute));
                    else
                        continue;
                }
                #if PHOTON_UNITY_NETWORKING
                else if (type.BaseType == typeof(MonoBehaviourPun || type.BaseType == typeof(MonoBehaviourPunCallbacks)) 
                {
                    object[] found = UnityEngine.Object.FindObjectsOfType(type);

                    if (found.Length > 0)
                        instance = found.First(t => Attribute.IsDefined(t.GetType(), attribute));
                    else
                        continue;
                }
                #endif
                else
                {
                    instance = Activator.CreateInstance(type);
                }

                if (instance != null)
                    result.Add(instance);
            }

            return result.ToArray();
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

                    object instance = Activator.CreateInstance(type);

                    if (instance != null)
                        result.Add(instance);
                }

                return result.ToArray();
            });
        }
    }
}
