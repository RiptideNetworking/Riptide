using Riptide.Utils;
using System;
using UnityEngine;

namespace Riptide.Demos.PlayerHosted
{
    internal enum MessageId : ushort
    {
        SpawnPlayer = 1,
        PlayerMovement
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
        [SerializeField] private ushort maxPlayers;
        [Header("Prefabs")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject localPlayerPrefab;

        public GameObject PlayerPrefab => playerPrefab;
        public GameObject LocalPlayerPrefab => localPlayerPrefab;

        internal Server Server { get; private set; }
        internal Client Client { get; private set; }

        private void Awake()
        {
            Singleton = this;
        }

        private void Start()
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

            Server = new Server();
            Server.ClientConnected += PlayerJoined;
            Server.RelayFilter = new MessageRelayFilter(typeof(MessageId), MessageId.SpawnPlayer, MessageId.PlayerMovement);

            Client = new Client();
            Client.Connected += DidConnect;
            Client.ConnectionFailed += FailedToConnect;
            Client.ClientDisconnected += PlayerLeft;
            Client.Disconnected += DidDisconnect;
        }

        private void FixedUpdate()
        {
            if (Server.IsRunning)
                Server.Update();
            
            Client.Update();
        }

        private void OnApplicationQuit()
        {
            Server.Stop();
            Client.Disconnect();
        }

        internal void StartHost()
        {
            Server.Start(port, maxPlayers);
            Client.Connect($"127.0.0.1:{port}");
        }

        internal void JoinGame(string ipString)
        {
            Client.Connect($"{ipString}:{port}");
        }

        internal void LeaveGame()
        {
            Server.Stop();
            Client.Disconnect();
        }

        private void DidConnect(object sender, EventArgs e)
        {
            Player.Spawn(Client.Id, UIManager.Singleton.Username, Vector3.zero, true);
        }

        private void FailedToConnect(object sender, EventArgs e)
        {
            UIManager.Singleton.BackToMain();
        }

        private void PlayerJoined(object sender, ServerConnectedEventArgs e)
        {
            foreach (Player player in Player.List.Values)
                if (player.Id != e.Client.Id)
                    player.SendSpawn(e.Client.Id);
        }

        private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
        {
            Destroy(Player.List[e.Id].gameObject);
        }

        private void DidDisconnect(object sender, DisconnectedEventArgs e)
        {
            foreach (Player player in Player.List.Values)
                Destroy(player.gameObject);

            UIManager.Singleton.BackToMain();
        }
    }
}
