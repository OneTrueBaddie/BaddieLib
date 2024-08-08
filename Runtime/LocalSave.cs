namespace Baddie.Saving.Local
{
    using Baddie.Utils;
    using Baddie.Commons;
    using Baddie.Requests;
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using UnityEngine;
    using System.Diagnostics;
    using Newtonsoft.Json;
    using System.Collections.Generic;

    [AttributeUsage(AttributeTargets.All)]
    public class LocalSaveAttribute : Attribute
    {
        public LocalSaveAttribute() { }
    }

    public class LocalSaveData
    {
        public Type Type;
        public string Name;
        public string ID;
        public string Json;

        public LocalSaveData(Type type, string name, string id, string json) 
        { 
            Type = type;
            Name = name;
            ID = id;
            Json = json;
        }
    }

    public class LocalSaver
    {
        public static string SavePath = $"{Path.GetPathRoot(Environment.SystemDirectory)}Users\\{Environment.UserName.ToLower()}\\Documents\\My Games\\{Application.companyName}\\{Application.productName}";
        public static bool Genuine = Application.genuine;

        static string EncryptionKey = null;
        static byte[] EncryptionIV = null;

        static LocalSaver()
        {
            if (!Directory.Exists(SavePath))
                Directory.CreateDirectory(SavePath);
        }

        /// <summary>
        /// Gets the encryption key and iv once and stores it for all future use
        /// </summary>
        public static async void StoreEncryption()
        {
            EncryptionKey = await Requests.GetCloudValue<string>("GetEncryption", "Key");
            EncryptionIV = await Requests.GetCloudValue<byte[]>("GetEncryption", "IV");
        }

        /// <summary>
        /// Gets the encryption key and iv once and stores it for all future use
        /// </summary>
        public static async Task StoreEncryption(bool task)
        {
            EncryptionKey = await Requests.GetCloudValue<string>("GetEncryption", "Key");
            EncryptionIV = await Requests.GetCloudValue<byte[]>("GetEncryption", "IV");
        }

        /// <summary>
        /// Save the given data to a file as plain-text, if the file already exists then it will be overwritten.
        /// This has the limit of only being able to handle 1 script per file.
        /// Use 'SaveAuto' to allow saving multiple different scripts/instances
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static void SaveRaw(string name, object data)
        {
            var path = $"{SavePath}\\{name}.json";
            var json = JsonUtility.ToJson(data);

            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Save the given data to a file as plain-text, if the file already exists then it will be overwritten
        /// This has the limit of only being able to handle 1 script per file.
        /// Use 'SaveAuto' to allow saving multiple different scripts/instances
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static Task SaveRaw(string name, object data, bool task)
        {
            return Task.Run(() =>
            {
                var path = $"{SavePath}\\{name}.json";
                var json = JsonUtility.ToJson(data);

                File.WriteAllText(path, json);
            });
        }

        /// <summary>
        /// Save and encrypt the given data to a file, if the file already exists then it will be overwritten
        /// This has the limit of only being able to handle 1 script per file.
        /// Use 'SaveAuto' to allow saving multiple different scripts/instances
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static async void SaveAndEncrypt(string name, object data)
        {
            var key = EncryptionKey ?? await Requests.GetCloudValue<string>("GetEncryption", "Key");
            var iv = EncryptionIV ?? await Requests.GetCloudValue<byte[]>("GetEncryption", "IV");
            var json = await Encrypt(JsonUtility.ToJson(data, true), key, iv);

            File.WriteAllText($"{SavePath}\\{name}.json", json);
        }

        /// <summary>
        /// Save and encrypt the given data to a file, if the file already exists then it will be overwritten
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public static async Task SaveAndEncrypt(string name, object data, bool task)
        {
            var key = EncryptionKey ?? await Requests.GetCloudValue<string>("GetEncryption", "Key");
            var iv = EncryptionIV ?? await Requests.GetCloudValue<byte[]>("GetEncryption", "IV");
            var json = await Encrypt(JsonUtility.ToJson(data, true), key, iv);

            File.WriteAllText($"{SavePath}\\{name}.json", json);
        }

        /// <summary>
        /// Automatically saves all values that have the LocalSave attribute.
        /// Some data may not be parsable, such as structs or lists/arrays that dont contain simple variables.
        /// (I may have to add custom parsing to support more advanced variables)
        /// </summary>
        public static void SaveAuto(string name)
        {
            var scripts = Reflection.GetInstances(typeof(LocalSaveAttribute));
            List<object> objects = new();

            if (scripts == null || scripts.Length == 0)
            {
                Utils.Debugger.Log("Cannot local save, 'scripts' is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();

            if (!File.Exists($"{SavePath}\\{name}.json"))
                File.Create($"{SavePath}\\{name}.json");

            foreach (var script in scripts)
            {
                objects.Add(script);
                Utils.Debugger.Log(script.GetType().Name);
            }

            var json = JsonConvert.SerializeObject(objects, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
            });

            File.WriteAllText($"{SavePath}\\{name}.json", json);

            sw.Stop();
            Utils.Debugger.Log($"Automatically saved data to local ({sw.Elapsed.TotalMilliseconds}ms)", LogColour.Green);
        }

        /// <summary>
        /// Automatically saves all values that have the LocalSave attribute.
        /// Some data may not be parsable, such as structs or lists/arrays that dont contain simple variables.
        /// (I may have to add custom parsing to support more advanced variables)
        /// </summary>
        public static Task SaveAuto(string name, bool task)
        {
            var scripts = Reflection.GetInstances(typeof(LocalSaveAttribute));
            List<object> objects = new();

            if (!File.Exists($"{SavePath}\\{name}.json"))
                File.Create($"{SavePath}\\{name}.json");

            return Task.Run(() =>
            {
                if (scripts == null || scripts.Length == 0)
                {
                    Utils.Debugger.Log("Cannot local save, 'scripts' is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                    return;
                }

                Stopwatch sw = Stopwatch.StartNew();

                foreach (var script in scripts)
                    objects.Add(script);

                var json = JsonConvert.SerializeObject(objects, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                File.WriteAllText($"{SavePath}\\{name}.json", json);

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
        public static Task<(bool, T)> TryLoadSave<T>(string name, bool encrypted = false)
        {
            return Task.Run(async () =>
            {
                var path = $"{SavePath}\\{name}.json";

                if (!File.Exists(path))
                    return (false, default(T));

                try
                {
                    var json = File.ReadAllText(path);

                    if (encrypted)
                    {
                        var key = EncryptionKey ?? await Requests.GetCloudValue<string>("GetEncryption", "Key");
                        var iv = EncryptionIV ?? await Requests.GetCloudValue<byte[]>("GetEncryption", "IV");

                        json = await Decrypt(json, key, iv);
                    }

                    return (true, JsonUtility.FromJson<T>(json));
                }
                catch (Exception e)
                {
                    Utils.Debugger.Log($"Error trying to load save '{name}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                    return (false, default(T));
                }
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
            var type = value.GetType();

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
            var type = typeof(T);

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
            var type = data.GetType();

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
            return Task.Run(() =>
            {
                using (var aes = Aes.Create())
                {
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

                    aes.Key = Encoding.UTF8.GetBytes(key);
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

                    aes.Key = Encoding.UTF8.GetBytes(key);
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
