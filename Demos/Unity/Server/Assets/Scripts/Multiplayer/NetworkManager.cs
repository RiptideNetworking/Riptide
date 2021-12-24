
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Utils;
using System;
using UnityEngine;

namespace RiptideNetworking.Demos.RudpTransport.Unity.ExampleServer
{
    public enum ServerToClientId : ushort
    {
        spawnPlayer = 1,
        playerMovement,
    }
    public enum ClientToServerId : ushort
    {
        playerName = 1,
        playerInput,
    }

    public class NetworkManager : MonoBehaviour
    {
        private static NetworkManager _singleton;
        public static NetworkManager Singleton
        {
            get => _singleton;
            private set
            {
                if (_singleton == null)
                    _singleton = value;
                else if (_singleton != value)
                {
                    Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying object!");
                    Destroy(value);
                }
            }
        }

        [SerializeField] private ushort port;
        [SerializeField] private ushort maxClientCount;
        [SerializeField] private GameObject playerPrefab;

        public GameObject PlayerPrefab => playerPrefab;

        public Server Server { get; private set; }

        private void Awake()
        {
            Singleton = this;
        }

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

#if UNITY_EDITOR
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
#else
            Console.Title = "Server";
            Console.Clear();
            Application.SetStackTraceLogType(UnityEngine.LogType.Log, StackTraceLogType.None);
            RiptideLogger.Initialize(Debug.Log, true);
#endif

            Server = new Server();
            Server.ClientConnected += NewPlayerConnected;
            Server.ClientDisconnected += PlayerLeft;

            Server.Start(port, maxClientCount);
        }

        private void FixedUpdate()
        {
            Server.Tick();
        }

        private void OnApplicationQuit()
        {
            Server.Stop();

            Server.ClientConnected -= NewPlayerConnected;
            Server.ClientDisconnected -= PlayerLeft;
        }

        private void NewPlayerConnected(object sender, ServerClientConnectedEventArgs e)
        {
            foreach (Player player in Player.List.Values)
            {
                if (player.Id != e.Client.Id)
                    player.SendSpawn(e.Client.Id);
            }
        }

        private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
        {
            Destroy(Player.List[e.Id].gameObject);
        }
    }
}