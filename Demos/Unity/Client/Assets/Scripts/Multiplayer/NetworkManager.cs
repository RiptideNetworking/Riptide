
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Utils;
using System;
using UnityEngine;

namespace RiptideNetworking.Demos.RudpTransport.Unity.ExampleClient
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

        [SerializeField] private string ip;
        [SerializeField] private ushort port;

        [SerializeField] private GameObject localPlayerPrefab;
        [SerializeField] private GameObject playerPrefab;

        public GameObject LocalPlayerPrefab => localPlayerPrefab;
        public GameObject PlayerPrefab => playerPrefab;

        public Client Client { get; private set; }

        private void Awake()
        {
            Singleton = this;
        }

        private void Start()
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

            Client = new Client();
            Client.Connected += DidConnect;
            Client.ConnectionFailed += FailedToConnect;
            Client.ClientDisconnected += PlayerLeft;
            Client.Disconnected += DidDisconnect;
        }

        private void FixedUpdate()
        {
            Client.Tick();
        }

        private void OnApplicationQuit()
        {
            Client.Disconnect();

            Client.Connected -= DidConnect;
            Client.ConnectionFailed -= FailedToConnect;
            Client.ClientDisconnected -= PlayerLeft;
            Client.Disconnected -= DidDisconnect;
        }

        public void Connect()
        {
            Client.Connect($"{ip}:{port}");
        }

        private void DidConnect(object sender, EventArgs e)
        {
            UIManager.Singleton.SendName();
        }

        private void FailedToConnect(object sender, EventArgs e)
        {
            UIManager.Singleton.BackToMain();
        }

        private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
        {
            Destroy(Player.list[e.Id].gameObject);
        }

        private void DidDisconnect(object sender, EventArgs e)
        {
            UIManager.Singleton.BackToMain();

            foreach (Player player in Player.list.Values)
                Destroy(player.gameObject);
        }
    }
}
