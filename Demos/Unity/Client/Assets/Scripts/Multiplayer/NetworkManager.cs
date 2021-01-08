using RiptideNetworking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            {
                _singleton = value;
            }
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }

    public Client Client { get; private set; }
    private ActionQueue actionQueue;

    [SerializeField] private string ip;
    [SerializeField] private ushort port;

    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject playerPrefab;

    public GameObject LocalPlayerPrefab { get => localPlayerPrefab; }
    public GameObject PlayerPrefab { get => playerPrefab; }

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        RiptideLogger.Initialize(Debug.Log, false);

        actionQueue = new ActionQueue();

        Client = new Client();
        Client.Connected += DidConnect;
        Client.ClientDisconnected += PlayerLeft;
    }

    private void FixedUpdate()
    {
        actionQueue.ExecuteAll();
    }

    private void OnApplicationQuit()
    {
        Client.Disconnect();
    }

    public void Connect()
    {
        Dictionary<ushort, Client.MessageHandler> messageHandlers = new Dictionary<ushort, Client.MessageHandler>()
        {
            { (ushort)ServerToClientId.spawnPlayer, ClientHandle.SpawnPlayer },
            { (ushort)ServerToClientId.playerMovement, ClientHandle.PlayerMovement },
        };
        Client.Connect(ip, port, messageHandlers, actionQueue);
    }

    private void DidConnect(object sender, EventArgs e)
    {
        UIManager.Singleton.SendName();
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Id].gameObject);
    }
}
