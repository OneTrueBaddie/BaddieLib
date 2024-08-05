namespace Baddie.Cloud.Requests
{
    //using System.Net.Http;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Unity.Services.CloudCode;
    using Baddie.Utils;
    using System;
    using System.Reflection;
    using Baddie.Saving.Cloud;
    using Unity.Services.Core;

    public static class RequestAPI
    {
        //static readonly HttpClient Client = new();
        static Dictionary<string, object> CloudData = new();

        static RequestAPI()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                Setup();
        }

        static async void Setup() 
        { 
            await UnityServices.InitializeAsync();
        }

        static async void CallEndpoint(string methodName, Dictionary<string, object> args = null)
        {

            CloudData = await CloudCodeService.Instance.CallEndpointAsync<Dictionary<string, object>>(methodName, args);
            Debugger.Log(CloudData.Count);
        }
    
        public static Task<T> GetCloudValue<T>(string methodName, string key, Dictionary<string, object> args = null)
        {
            CallEndpoint(methodName, args);

            return Task.Run(() =>
            {
                try
                {
                    return (T)Convert.ChangeType(CloudData[key], typeof(T));
                }
                catch (Exception e)
                {
                    Debugger.Log($"Error trying to get cloud value from '{methodName}' with key '{key}', exception: {e}", LogColour.Red, LogType.Error);
                    return default;
                }
            });
        }

        public static Task<Dictionary<string, object>> GetCloudDictionary(string methodName, Dictionary<string, object> args = null)
        {
            CallEndpoint(methodName, args);

            return Task.Run(() =>
            {
                try
                {
                    foreach (KeyValuePair<string, object> pair in CloudData)
                        Debugger.Log(pair.Key + " " + pair.Value);

                    return CloudData;
                }
                catch (Exception e)
                {
                    Debugger.Log($"Error trying to get cloud dictionary from '{methodName}', exception: {e}", LogColour.Red, LogType.Error);
                    return null;
                }
            });
        }
    }
}
