namespace Baddie.Saving.Cloud
{
    using Baddie.Commons;
    using Baddie.Utils;
    using System.Collections.Generic;
    using Unity.Services.Authentication;
    using Unity.Services.CloudSave;
    using Unity.Services.CloudSave.Models;
    using Unity.Services.Core;
    using System;
    using System.Collections;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using System.Threading.Tasks;
    using System.Diagnostics;

    public static class CloudSaver
    {
        public static Dictionary<string, Item> LoadedData = new();
        public static Dictionary<string, object> SavedData = new();
        public static string UUID = null;
        public static bool FirstTime = true;

        public static event Action OnSignIn = () =>
        {
            GetRawData();

            UUID = GetUUID();
        };
        public static event Action OnSignOut = () =>
        {
            ResetEvents();

            Utils.Debugger.Log($"Signed out from cloud ({UUID})", LogColour.Yellow);

            UUID = null;
            FirstTime = true;
        };
        public static event Action<RequestFailedException> OnSignInFail = (request) =>
        {
            UUID = null;
        };

        static Action OnSignInOriginal = OnSignIn;
        static Action OnSignOutOriginal = OnSignOut;
        static Action<RequestFailedException> OnSignInFailOriginal = OnSignInFail;

        public static async void Setup()
        {
            SignOut();

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();

                // We only want to do this the first ever time, otherwise it duplicates the actions
                if (FirstTime)
                {
                    AuthenticationService.Instance.SignedIn += OnSignIn;
                    AuthenticationService.Instance.SignedOut += OnSignOut;
                    AuthenticationService.Instance.SignInFailed += OnSignInFail;
                    FirstTime = false;
                }

                SignIn();
            }
        }

        public static async void SignIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                Setup();

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public static void SignOut()
        {
            if (IsSignedIn())
                AuthenticationService.Instance.SignOut();
        }

        public static async void SignOutAndSave()
        {
            if (IsSignedIn())
            {
                await ConvertToCloud();
                await SaveToCloud();

                AuthenticationService.Instance.SignOut();
            }
        }

        public static async Task SaveToCloud()
        {
            if (SavedData == null || SavedData.Count == 0)
                await ConvertToCloud();

            await CloudSaveService.Instance.Data.Player.SaveAsync(SavedData);
        }

        /// <summary>
        /// Invoke a job once the user is connected to the cloud, if the user is already connected it will instantly invoke
        /// </summary>
        /// <param name="job"></param>
        /// <returns></returns>
        public static IEnumerator WaitForCloud(Action job)
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                Setup();

            yield return new WaitUntil(IsSignedIn);

            job?.Invoke();
        }

        /// <summary>
        /// Converts all CloudSave variables from all scripts and then saves them to the cloud
        /// </summary>
        public static Task ConvertToCloud()
        {
            object[] scripts = Reflection.GetInstances(typeof(CloudSaveAttribute));

            return Task.Run(() =>
            {
                if (scripts == null || scripts.Length == 0)
                {
                    Utils.Debugger.Log("Cannot convert to cloud, 'scripts' is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                    return;
                }

                Stopwatch sw = Stopwatch.StartNew();

                SavedData.Clear();

                foreach (var script in scripts)
                {
                    Type type = script.GetType();
                    IEnumerable<FieldInfo> fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var field in fields)
                    {
                        if (!Attribute.IsDefined(field, typeof(CloudSaveAttribute)))
                            continue;

                        try
                        {
                            object value = field.GetValue(script);
                            string fieldName = field.Name.Replace(" ", "_");

                            lock (SavedData) { SavedData.Add(fieldName, value); }
                        }
                        catch (Exception e)
                        {
                            Utils.Debugger.Log($"Error trying to convert variable '{field.Name}' from instance '{type.Name}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                        }
                    }
                }

                Utils.Debugger.Log($"Converted current data to cloud formatting ({UUID}, {sw.Elapsed.TotalMilliseconds:F3}ms)", LogColour.Green);
            });
        }


        /// <summary>
        /// Gets the data from the cloud and assigns those values to all CloudSave instances, assuming the variables are found and correctly formatted
        /// </summary>
        public static Task ConvertFromCloud()
        {
            object[] scripts = Reflection.GetInstances(typeof(CloudSaveAttribute));

            return Task.Run(() =>
            {
                if (scripts == null || scripts.Length == 0)
                {
                    Utils.Debugger.Log("Cannot covert from cloud, 'scripts' is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                    return;
                }

                MethodInfo getAs = null;
                Stopwatch sw = Stopwatch.StartNew();

                LoadedData.Clear();

                foreach (var script in scripts)
                {
                    Type type = script.GetType();
                    IEnumerable<FieldInfo> fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var field in fields)
                    {
                        if (!Attribute.IsDefined(field, typeof(CloudSaveAttribute)))
                            return;

                        lock (LoadedData)
                        {
                            if (LoadedData.TryGetValue(field.Name, out Item item))
                            {
                                if (getAs == null)
                                {
                                    getAs = item.Value.GetType()
                                    .GetMethods()
                                    .FirstOrDefault(m => m.Name == "GetAs" && m.IsGenericMethod && m.GetParameters().Length == 0);
                                }

                                getAs.MakeGenericMethod(field.FieldType);
                                field.SetValue(script, getAs.Invoke(item.Value, null));
                            }
                        }
                    }
                }

                sw.Stop();
                Utils.Debugger.Log($"Coverted and loaded raw data from the cloud ({UUID}, {sw.Elapsed.TotalMilliseconds:F3}ms)", LogColour.Green);
            });
        }

        public static async void DeleteAllData()
        {
            Utils.Debugger.Log($"Cleared all cloud data ({UUID})", LogColour.Yellow);
            await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
        }

        public static void ResetEvents()
        {
            OnSignIn = OnSignInOriginal;
            OnSignOut = OnSignOutOriginal;
            OnSignInFail = OnSignInFailOriginal;
        }

        /// <summary>
        /// Get the access token of the current player signed into the cloud anonymously
        /// </summary>
        /// <returns>(string) Access token of current player</returns>
        public static string GetUUID() { return AuthenticationService.Instance.PlayerId; }

        /// <summary>
        /// Check if the current player is signed into the cloud and that the cloud is setup
        /// </summary>
        /// <returns>(bool) true if the player is signed in, false if not. Also returns false if the cloud instance is not initialized</returns>
        public static bool IsSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return false;

            return AuthenticationService.Instance.IsSignedIn;
        }

        /// <summary>
        /// Checks if the current player has any data on the cloud
        /// </summary>
        /// <returns></returns>
        public static bool HasCloudData() { return LoadedData.Count > 0; }

        static async void GetRawData()
        {
            LoadedData = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
            Utils.Debugger.Log($"Loaded raw data from cloud ({UUID})", LogColour.Green);
        }
    }
}