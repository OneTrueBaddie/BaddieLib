namespace Baddie.Cloud.Requests
{
    //using System.Net.Http;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Unity.Services.CloudCode;
    using Baddie.Utils;
    using System;

    public static class RequestAPI
    {
        //static readonly HttpClient Client = new();

        public static Task<T> GetCloudValue<T>(string methodName, string key, Dictionary<string, object> args = null)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var response = await CloudCodeService.Instance.CallEndpointAsync<Dictionary<string, object>>(methodName, args);
                    return (T)Convert.ChangeType(response[key], typeof(T));
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
            return Task.Run(async () =>
            {
                try
                {
                    return await CloudCodeService.Instance.CallEndpointAsync<Dictionary<string, object>>(methodName, args);
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
