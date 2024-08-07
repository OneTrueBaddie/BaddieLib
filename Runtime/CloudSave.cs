namespace Baddie.Saving.Cloud
{
    using Baddie.Commons;
    using Baddie.Utils;
    using System.Collections.Generic;
    using Unity.Services.CloudSave;
    using Unity.Services.CloudSave.Models;
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Diagnostics;

    public static class CloudSaver
    {
        public static volatile Dictionary<string, Item> LoadedData = new();
        public static volatile Dictionary<string, object> SavedData = new();

        static CloudSaver()
        {
            if (!Services.IsSetup())
                Utils.Debugger.Log("Unity services is not setup, make sure it is setup otherwise CloudSaver will not work.", LogColour.Yellow, LogType.Warning);
            else if (!Services.IsSignedIn())
                Utils.Debugger.Log("Current player is not signed into Unity Services, make sure they are signed in otherwise CloudSaver will not work.", LogColour.Yellow, LogType.Warning);
        }

        public static async void Save()
        {
            if (Services.IsSignedIn())
            {
                await GetRawData();
                await ConvertToCloud();
                await SaveToCloud();
            }
        }

        public static async Task SaveToCloud()
        {
            if (SavedData == null || SavedData.Count == 0)
                await ConvertToCloud();

            await CloudSaveService.Instance.Data.Player.SaveAsync(SavedData);
        }

        /// <summary>
        /// Converts all CloudSave variables from all scripts and then saves them to the cloud
        /// </summary>
        public static Task ConvertToCloud()
        {
            if (!Services.IsSetup())
            {
                Utils.Debugger.Log("Cannot covert to cloud, unity services is not setup", LogColour.Red, Utils.LogType.Error);
                return null;
            }
            if (!Services.IsSignedIn())
            {
                Utils.Debugger.Log("Cannot covert to cloud, player is not signed into unity services", LogColour.Red, Utils.LogType.Error);
                return null;
            }

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

                Utils.Debugger.Log($"Converted current data to cloud formatting ({Services.UUID}, {sw.Elapsed.TotalMilliseconds:F3}ms)", LogColour.Green);
            });
        }


        /// <summary>
        /// Gets the data from the cloud and assigns those values to all CloudSave instances, assuming the variables are found and correctly formatted
        /// </summary>
        public static Task ConvertFromCloud()
        {
            if (!Services.IsSetup())
            {
                Utils.Debugger.Log("Cannot covert from cloud, unity services is not setup", LogColour.Red, Utils.LogType.Error);
                return null;
            }
            if (!Services.IsSignedIn()) 
            {
                Utils.Debugger.Log("Cannot covert from cloud, player is not signed into unity services", LogColour.Red, Utils.LogType.Error);
                return null;
            }

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
                Utils.Debugger.Log($"Coverted and loaded raw data from the cloud ({Services.UUID}, {sw.Elapsed.TotalMilliseconds:F3}ms)", LogColour.Green);
            });
        }

        public static async void DeleteAllData()
        {
            Utils.Debugger.Log($"Cleared all cloud data ({Services.UUID})", LogColour.Yellow);
            await CloudSaveService.Instance.Data.Player.DeleteAllAsync();
        }

        /// <summary>
        /// Checks if the current player has any data on the cloud
        /// </summary>
        /// <returns>(bool) true if the player has data, false if not</returns>
        public static bool HasCloudData() { return LoadedData.Count > 0; }

        static Task<Dictionary<string, Item>> GetRawData()
        {
            Utils.Debugger.Log($"Loaded raw data from cloud ({Services.UUID})", LogColour.Green);

            return CloudSaveService.Instance.Data.Player.LoadAllAsync();
        }
    }
}
