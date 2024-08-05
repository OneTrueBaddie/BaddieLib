namespace Baddie.Photon
{
    using UnityEngine;
    using System;
    using System.Collections.Generic;

#if PHOTON_UNITY_NETWORKING

    using Photon.Pun;
    using Photon.Realtime;

    public static class NetworkHelper
    {
        public static string GameVersion = Application.version;
        public static int MaxNameLength = 24;

        static NetworkHelper()
        {
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.PhotonServerSettings.AppSettings.EnableLobbyStatistics = true;
        }

        /// <summary>
        /// Create and host a new room, if the player is not connected then it will first wait until the connection completes
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="roomOptions"></param>
        /// <param name="typedLobby"></param>
        /// <param name="expectedUsers"></param>
        /// <returns></returns>
        public static IEnumerator CreateRoom(string roomName, RoomOptions roomOptions = null, TypedLobby typedLobby = null, string[] expectedUsers = null)
        {
            // If the client is already authenticating then we dont want to attempt more until it completes
            if (PhotonNetwork.NetworkClientState == ClientState.Authenticating)
                yield return new WaitUntil(() => PhotonNetwork.NetworkClientState != ClientState.Authenticating);

            if (roomName == "")
                roomName = $"Room {PhotonNetwork.CountOfRooms}";

            if (!PhotonNetwork.IsConnectedAndReady && PhotonNetwork.NetworkClientState != ClientState.Authenticating)
            {
                PhotonNetwork.ConnectUsingSettings();
                yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer);
            }

            if (PhotonNetwork.IsConnectedAndReady)
                PhotonNetwork.CreateRoom(roomName: roomName, roomOptions, typedLobby, expectedUsers);
        }

        /// <summary>
        /// Try to join any random room with the passed options, if no room is found then it will create and host a new room with the options.
        /// If the player is not connected it will first wait until the connection completes.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="roomOptions"></param>
        /// <param name="typedLobby"></param>
        /// <param name="expectedUsers"></param>
        /// <returns></returns>
        public static IEnumerator JoinRandomOrCreateRoom(RoomOptions roomOptions = null, TypedLobby typedLobby = null, string[] expectedUsers = null)
        {
            // If the client is already authenticating then we dont want to attempt more until it completes
            if (PhotonNetwork.NetworkClientState == ClientState.Authenticating)
                yield return new WaitUntil(() => PhotonNetwork.NetworkClientState != ClientState.Authenticating);

            if (!PhotonNetwork.IsConnectedAndReady && PhotonNetwork.NetworkClientState != ClientState.Authenticating)
            {
                PhotonNetwork.ConnectUsingSettings();
                yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer);
            }

            if (PhotonNetwork.IsConnectedAndReady)
                PhotonNetwork.JoinRandomOrCreateRoom(roomOptions: roomOptions, typedLobby: typedLobby, expectedUsers: expectedUsers);
        }

        public static string ChangeName(string name)
        {
            if (name == "")
                name = $"Player {PhotonNetwork.CountOfPlayers}";
            else if (name.Length > MaxNameLength)
                name = name.Remove(MaxNameLength, name.Length - MaxNameLength);

            return name;
        }
    }
#endif
}