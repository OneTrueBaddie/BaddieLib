namespace Baddie.Saving.Leaderboard
{
    using Baddie.Commons;
    using Baddie.Utils;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Unity.Services.Leaderboards;
    using Unity.Services.Leaderboards.Models;

    public static class LeaderboardSaver
    {
        static LeaderboardSaver()
        {
            if (!Services.IsSetup())
                Debugger.Log("Unity services is not setup, make sure it is setup otherwise LeaderboardSaver will not work.", LogColour.Yellow, LogType.Warning);
            else if (!Services.IsSignedIn())
                Debugger.Log("Current player is not signed into Unity Services, make sure they are signed in otherwise LeaderboardSaver will not work fully.", LogColour.Yellow, LogType.Warning);
        }

        /// <summary>
        /// Adds the given score the a leaderboard
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="score"></param>
        public static async void AddScore(string leaderboard, double score)
        {
            if (!Services.IsSignedIn())
            {
                Debugger.Log("Cannot add score to leaderboard, make sure UnityServices is initilized and the player is signed in", LogColour.Red, LogType.Error);
                return;
            }

            LeaderboardEntry result = await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, score);
            Debugger.Log(JsonConvert.SerializeObject(result));
        }

        /// <summary>
        /// Adds the given score to a leaderboard with MetaData
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="score"></param>
        /// <param name="metadata"></param>
        public static async void AddScore(string leaderboard, double score, Dictionary<string, string> metadata)
        {
            if (!Services.IsSignedIn())
            {
                Debugger.Log("Cannot add score to leaderboard, make sure UnityServices is initilized and the player is signed in", LogColour.Red, LogType.Error);
                return;
            }

            LeaderboardEntry result = await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, score, new() { Metadata = metadata });
            Debugger.Log(JsonConvert.SerializeObject(result));
        }

        /// <summary>
        /// Gets all the scores on the given leaderboard
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <returns>(LeaderboardScoresPage) All found scores</returns>
        public static async Task<LeaderboardScoresPage> GetScores(string leaderboard)
        {
            return await LeaderboardsService.Instance.GetScoresAsync(leaderboard);
        }

        /// <summary>
        /// Get all the player scores on the leaderboard with metadata
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="metaData"></param>
        /// <returns>(LeaderboardScoresPage) All found scores with metadata</returns>
        public static async Task<LeaderboardScoresPage> GetScores(string leaderboard, bool metaData)
        {
            return await LeaderboardsService.Instance.GetScoresAsync(leaderboard, new() { IncludeMetadata = metaData });
        }

        /// <summary>
        /// Gets only the values of the given metadata
        /// </summary>
        /// <param name="json"></param>
        /// <returns>String array of the values, must parse to use outside of a string</returns>
        public static string[] GetMetadataValues(string json)
        {
            var metadata = JObject.Parse(json);
            List<string> values = new();

            foreach (KeyValuePair<string, JToken> item in metadata)
                values.Add(item.Value.ToString());

            return values.ToArray();
        }

        /// <summary>
        /// Gets the full metadata of a given json, both keys and values
        /// </summary>
        /// <param name="json"></param>
        /// <returns>Dictionary of the key and value, must parse the value to use outside of a string</returns>
        public static Dictionary<string, string> GetMetadata(string json)
        {
            if (json == null)
                return null;

            var metadata = JObject.Parse(json);
            Dictionary<string, string> result = new();

            foreach (KeyValuePair<string, JToken> item in metadata)
                result.Add(item.Key, item.Value.ToString());

            return result;
        }

        /// <summary>
        /// Removes all meta data from the currently signed in player
        /// </summary>
        /// <param name="leaderboard"></param>
        public static async void RemoveMetadata(string leaderboard)
        {
            try
            {
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboard);

                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, entry.Score, new() { Metadata = null });

                Debugger.Log($"Successfully wiped all metadata ({Services.UUID})", LogColour.Green);
            }
            catch (Exception e)
            {
                Debugger.Log($"Could not wipe metadata, exception: {e}", LogColour.Red, LogType.Error);
            }
        }

        /// <summary>
        /// Removes a specific key from the currently signed in players metadata without removing the rest
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="key"></param>
        public static async void RemoveMetadata(string leaderboard, string key)
        {
            try
            {
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboard, new() { IncludeMetadata = true });
                var metaData = GetMetadata(entry.Metadata);

                if (metaData == null)
                {
                    Debugger.Log("Cannot remove metadata key, metadata is null", LogColour.Yellow, LogType.Warning);
                    return;
                }

                if (metaData.Count > 0 && metaData.ContainsKey(key))
                    metaData.Remove(key);

                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, entry.Score, new() { Metadata = metaData });
            }
            catch (Exception e)
            {
                Debugger.Log($"Could not remove metadata key '{key}', exception: {e}", LogColour.Red, LogType.Error);
            }
        }

        /// <summary>
        /// Changes a specific value in the currently signed in players metadata without modifying the rest
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static async void ChangeMetadata(string leaderboard, string key, string value)
        {
            try
            {
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboard, new() { IncludeMetadata = true });
                var metaData = GetMetadata(entry.Metadata);

                if (metaData == null)
                {
                    Debugger.Log("Cannot change metadata key, metadata is null", LogColour.Yellow, LogType.Warning);
                    return;
                }

                if (metaData.Count > 0 && metaData.ContainsKey(key))
                    metaData[key] = value;

                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, entry.Score, new() { Metadata = metaData });
            }
            catch (Exception e)
            {
                Debugger.Log($"Could not change metadata key '{key}' ({value}), exception: {e}", LogColour.Red, LogType.Error);
            }
        }

        /// <summary>
        /// Changes or adds meta data to the current players leaderboard
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static async void ChangeOrAddMetadata(string leaderboard, string key, string value)
        {
            try
            {
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboard, new() { IncludeMetadata = true });
                var metaData = GetMetadata(entry.Metadata);

                if (metaData == null)
                {
                    Debugger.Log("Cannot add or change metadata key, metadata is null", LogColour.Yellow, LogType.Warning);
                    return;
                }

                if (!metaData.ContainsKey(key))
                    metaData.Add(key, value);
                else
                    metaData[key] = value;

                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, entry.Score, new() { Metadata = metaData });
            }
            catch (Exception e)
            {
                Debugger.Log($"Could not change or add metadata key '{key}' ({value}), exception: {e}", LogColour.Red, LogType.Error);
            }
        }

        /// <summary>
        /// Changes or adds meta data to the current players leaderboard
        /// </summary>
        /// <param name="leaderboard"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static async void ChangeOrAddMetadata(string leaderboard, string[] keys, string[] values)
        {
            try
            {
                var entry = await LeaderboardsService.Instance.GetPlayerScoreAsync(leaderboard, new() { IncludeMetadata = true });
                var metaData = GetMetadata(entry.Metadata);

                if (metaData == null)
                {
                    Debugger.Log("Could not change or add metadata keys, metadata is null", LogColour.Yellow, LogType.Warning);
                    return;
                }

                for (var i = 0; i < keys.Length; i++)
                {
                    if (i > values.Length)
                        break;

                    if (!metaData.ContainsKey(keys[i]))
                        metaData.Add(keys[i], values[i]);
                    else
                        metaData[keys[i]] = values[i];
                }

                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboard, entry.Score, new() { Metadata = metaData });
            }
            catch (Exception e)
            {
                Debugger.Log($"Could not change or add metadata keys, exception: {e}", LogColour.Red, LogType.Error);
            }
        }
    }
}