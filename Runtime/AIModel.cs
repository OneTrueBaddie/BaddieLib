using UnityEngine.Networking;
using UnityEngine;
using System.Collections;

public static class AIGenerator
{
    public static string URL = "http://localhost:5000/api";

    public static IEnumerator SendRequest(string input, System.Action<string> callback)
    {
        string jsonData = JsonUtility.ToJson(new RequestData(input));

        using (UnityWebRequest webRequest = new UnityWebRequest(URL, "POST"))
        {
            byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);
            webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = webRequest.downloadHandler.text;
                callback(responseText);
            }
            else
            {
                Debug.LogError("Error: " + webRequest.error);
            }
        }
    }

    [System.Serializable]
    private class RequestData
    {
        public string prompt;

        public RequestData(string prompt) { this.prompt = prompt; }
    }
}