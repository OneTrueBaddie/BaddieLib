namespace Baddie.Saving.Local
{
    using Baddie.Utils;
    using Baddie.Commons;
    using Baddie.Cloud.Requests;
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEngine;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Reflection;

    public class LocalSaver
    {
        public static string SavePath = $"{Path.GetPathRoot(Environment.SystemDirectory)}Users\\{Environment.UserName.ToLower()}\\Documents\\My Games\\{Application.companyName}\\{Application.productName}";
        public static bool Genuine = Application.genuine;

        static LocalSaver()
        {
            if (!Directory.Exists(SavePath))
                Directory.CreateDirectory(SavePath);
        }

        /// <summary>
        /// Save the given data to a file, if the file already exists then it will be overwritten
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static void Save(string name, object data)
        {
            var path = $"{SavePath}\\{name}.json";
            var json = JsonUtility.ToJson(data);

            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Save and encrypt the given data to a file, if the file already exists then it will be overwritten
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static async void SaveAndEncrypt(string name, object data)
        {
            var key = await RequestAPI.GetCloudValue<string>("GetEncryption", "Key");
            var iv = await RequestAPI.GetCloudValue<byte[]>("GetEncryption", "IV");
            var json = await Encrypt(JsonUtility.ToJson(data, true), key, iv);

            key = null;
            iv = null;

            File.WriteAllText($"{SavePath}\\{name}.json", json);
        }

        /// <summary>
        /// Automatically saves all values that have the LocalSave attribute.
        /// Some data may not be parsable, such as structs or lists/arrays that dont contain simple variables.
        /// (I may have to add custom parsing to support more advanced variables)
        /// </summary>
        public static Task SaveAuto()
        {
            // Collect instances on the main thread
            object[] scripts = Reflection.GetInstances(typeof(LocalSaveAttribute));

            return Task.Run(() =>
            {
                if (scripts == null || scripts.Length == 0)
                {
                    Utils.Debugger.Log("Cannot local save, 'scripts' is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                    return;
                }

                Stopwatch sw = Stopwatch.StartNew();

                foreach (var script in scripts)
                {
                    Type type = script.GetType();
                    IEnumerable<FieldInfo> fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var field in fields)
                    {
                        if (!Attribute.IsDefined(field, typeof(LocalSaveAttribute)))
                            continue;

                        try
                        {
                            object fieldValue = field.GetValue(script);

                            if (fieldValue != null)
                            {
                                string json = JsonUtility.ToJson(fieldValue, true);
                                Utils.Debugger.Log(json);
                            }
                            else
                            {
                                Utils.Debugger.Log($"Field '{field.Name}' in instance '{type.Name}' is null and was skipped.", LogColour.Yellow, Utils.LogType.Warning);
                            }
                        }
                        catch (Exception e)
                        {
                            Utils.Debugger.Log($"Error trying to save variable '{field.Name}' from instance '{type.Name}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                        }
                    }
                }

                sw.Stop();
                Utils.Debugger.Log($"Automatically saved data to local ({sw.Elapsed.TotalMilliseconds}ms)", LogColour.Green);
            });
        }

        /// <summary>
        /// Try to load a save with its filename
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns>(bool, T) weither or not the task succeeded and the value of the loaded save</returns>
        public static Task<(bool, T)> TryLoadSave<T>(string name)
        {
            return Task.Run(async () =>
            {
                string path = SavePath + $"{name}.json";
                T data = default;

                if (!File.Exists(path))
                    return (false, data);

                try
                {
                    string json = await Decrypt(File.ReadAllText(path));
                    data = JsonUtility.FromJson<T>(json);
                }
                catch (Exception e)
                {
                    Utils.Debugger.Log($"Error trying to load save '{name}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                    return (false, data);
                }

                return (true, data);
            });
        }

        /// <summary>
        /// Creates a new player pref with the given value, if the key exists then it will be overwritten
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="type"></param>
        public static void SavePlayerPref(string name, object value)
        {
            Type type = value.GetType();

            try
            {
                if (type == typeof(int))
                    PlayerPrefs.SetInt(name, (int)value);
                else if (type == typeof(float))
                    PlayerPrefs.SetFloat(name, (float)value);
                else if (type == typeof(string))
                    PlayerPrefs.SetString(name, (string)value);
                else
                    Utils.Debugger.Log($"Cannot save a variable with type 'type.Name' into PlayerPrefs, Unity only supports types Int, Float and String", LogColour.Yellow, Utils.LogType.Warning);

                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Utils.Debugger.Log($"Error trying to save PlayerPref '{name}', exception: {e}", LogColour.Red, Utils.LogType.Error);
            }
        }

        /// <summary>
        /// Loads the given key from PlayerPrefs
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns>(T) The value of the given key if found, otherwise returns the default value of T</returns>
        public static T LoadPlayerPref<T>(string name)
        {
            Type type = typeof(T);

            try
            {
                if (!PlayerPrefs.HasKey(name))
                {
                    Utils.Debugger.Log($"Cannot load PlayerPref '{name}' as it does not exist", LogColour.Yellow, Utils.LogType.Warning);
                    return default;
                }

                if (type == typeof(int))
                    return (T)Convert.ChangeType(PlayerPrefs.GetInt(name), typeof(T));
                else if (type == typeof(float))
                    return (T)Convert.ChangeType(PlayerPrefs.GetFloat(name), typeof(T));
                else if (type == typeof(string))
                    return (T)Convert.ChangeType(PlayerPrefs.GetString(name), typeof(T));
                else 
                    Utils.Debugger.Log($"Cannot load a variable with type 'type.Name' from PlayerPrefs, Unity only supports types Int, Float and String", LogColour.Yellow, Utils.LogType.Warning);
                
                return default;
            }
            catch (Exception e)
            {
                Utils.Debugger.Log($"Could not load PlayerPref '{name}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                return default;
            }
        }

        /// <summary>
        /// Loads the given key from PlayerPrefs
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="data"></param>
        /// <returns>(bool) True if the key was found, false if not</returns>
        public static bool LoadPlayerPref<T>(string name, out T data)
        {
            data = default;
            Type type = data.GetType();

            if (!PlayerPrefs.HasKey(name))
            {
                Utils.Debugger.Log($"Cannot load PlayerPref '{name}' as it does not exist", LogColour.Yellow, Utils.LogType.Warning);
                return false;
            }

            try
            {
                if (type == typeof(int))
                    data = (T)Convert.ChangeType(PlayerPrefs.GetInt(name), typeof(T));
                else if (type == typeof(float))
                    data = (T)Convert.ChangeType(PlayerPrefs.GetFloat(name), typeof(T));
                else if (type == typeof(string))
                    data = (T)Convert.ChangeType(PlayerPrefs.GetString(name), typeof(T));
                else
                    return false;

                return true;
            }
            catch (Exception e)
            {
                Utils.Debugger.Log($"Error trying to load PlayerPref '{name}', execption: {e}", LogColour.Red, Utils.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// Get the number of save files in the game save directory
        /// </summary>
        /// <returns>(int) Number of save files</returns>
        public static int GetSaveCount() { return Directory.GetFiles(SavePath).Length; }

        static Task<string> Encrypt(string plainText, string key, byte[] iv)
        {
            return Task.Run(async () =>
            {
                using (var aes = Aes.Create())
                {
                    if (string.IsNullOrEmpty(key))
                    {
                        Utils.Debugger.Log("Could not encrypt text, key is null or empty", LogColour.Red, Utils.LogType.Error);
                        return null;
                    }
                    if (iv == null)
                    {
                        Utils.Debugger.Log("Could not encrypt text, iv is null", LogColour.Red, Utils.LogType.Error);
                        return null;
                    }

                    aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32, ' '));
                    aes.IV = iv;

                    ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                    using var ms = new MemoryStream();
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            });
        }

        static Task<string> Decrypt(string cipherText, string key, byte[] iv)
        {
            return Task.Run(() =>
            {
                using (var aes = Aes.Create())
                {
                    string result = "";

                    if (string.IsNullOrEmpty(key))
                    {
                        Utils.Debugger.Log("Could not decrypt text, key is null or empty", LogColour.Red, Utils.LogType.Error);
                        return null;
                    }
                    if (iv == null)
                    {
                        Utils.Debugger.Log("Could not decrypt text, iv is null", LogColour.Red, Utils.LogType.Error);
                        return null;
                    }

                    aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32, ' '));
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
                    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs);

                    return sr.ReadToEnd();
                }
            });
        }
    }
}
