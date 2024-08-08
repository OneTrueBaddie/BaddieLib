namespace Baddie.Requests
{
    //using System.Net.Http;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Unity.Services.CloudCode;
    using Baddie.Utils;
    using System;
    using Baddie.Commons;
    using UnityEngine.Networking;
    using UnityEngine;
    using System.Text;
    using System.Collections;

    public static class Requests
    {
        //static readonly HttpClient Client = new();

        static Requests()
        {
            if (!Services.IsSetup())
                Debugger.Log("Unity services is not setup, make sure it is setup otherwise RequestAPI will not work.", LogColour.Yellow, Utils.LogType.Warning);
            else if (!Services.IsSignedIn())
                Debugger.Log("Current player is not signed into Unity Services, make sure they are signed in otherwise RequestAPI will not work fully.", LogColour.Yellow, Utils.LogType.Warning);
        }

        public static Task<Dictionary<string, object>> CallEndpoint(string methodName, Dictionary<string, object> args = null)
        {
            return CloudCodeService.Instance.CallEndpointAsync<Dictionary<string, object>>(methodName, args);
        }

        public static async Task<T> GetCloudValue<T>(string methodName, string key, Dictionary<string, object> args = null)
        {
            return await Task.Run(async() =>
            {
                var dictionary = await CallEndpoint(methodName, args);

                if (dictionary == null || dictionary.Count == 0)
                {
                    Debugger.Log("Could not get cloud value, dictionary is null or empty", LogColour.Yellow, Utils.LogType.Warning);
                    return default;
                }

                try
                {
                    if (dictionary.TryGetValue(key, out var data))
                    {
                        if (data is string str)
                            return ConversionHelper.FromString<T>(str);

                        return (T)Convert.ChangeType(data, typeof(T));
                    }

                    return default;
                }
                catch (Exception e)
                {
                    Debugger.Log($"Error trying to get cloud value from key '{key}', exception: {e}", LogColour.Red, Utils.LogType.Error);
                    return default;
                }
            });
        }

        public static IEnumerator SendWebRequest(string url, Action<string> callback, string method = "POST", params object[] args)
        {
            var data = JsonUtility.ToJson(args);

            using (UnityWebRequest webRequest = new(url, method.ToUpper()))
            {
                if (!string.IsNullOrEmpty(data))
                {
                    var jsonToSend = new UTF8Encoding().GetBytes(data);
                    webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
                }

                webRequest.downloadHandler = new DownloadHandlerBuffer();

                webRequest.SetRequestHeader("Content-Type", "application/json");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success && callback != null)
                {
                    var response = webRequest.downloadHandler.text;
                    callback(response);
                }
                else
                {
                    Debugger.Log($"Error trying to send web request to '{url}' with method '{method}', response: {webRequest.error}", LogColour.Red, Utils.LogType.Error);
                }
            }
        }

        public static IEnumerator IsWifiReachable(Action<bool> callback)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                callback(false);
                yield break;
            }

            using (UnityWebRequest webRequest = UnityWebRequest.Get("https://google.com"))
            {
                yield return webRequest.SendWebRequest();

                callback(webRequest.result == UnityWebRequest.Result.Success);
            }
        }
    }
}
