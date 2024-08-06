namespace Baddie.Cloud.Requests
{
    //using System.Net.Http;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Unity.Services.CloudCode;
    using Baddie.Utils;
    using System;
    using Baddie.Commons;

    public static class RequestAPI
    {
        //static readonly HttpClient Client = new();

        static RequestAPI()
        {
            if (!Services.IsSetup())
                Debugger.Log("Unity services is not setup, make sure it is setup otherwise RequestAPI will not work.", LogColour.Yellow, LogType.Warning);
            if (!Services.IsSignedIn())
                Debugger.Log("Current player is not signed into Unity Services, make sure they are signed in otherwise RequestAPI will not work.", LogColour.Yellow, LogType.Warning);
        }

        public static Task<Dictionary<string, object>> CallEndpoint(string methodName, Dictionary<string, object> args = null)
        {
            return CloudCodeService.Instance.CallEndpointAsync<Dictionary<string, object>>(methodName, args);
        }
    }
}
