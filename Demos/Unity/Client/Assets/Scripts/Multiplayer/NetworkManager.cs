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

    [SerializeField] private string ip;
    [SerializeField] private ushort port;

    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject playerPrefab;

    public GameObject LocalPlayerPrefab { get => localPlayerPrefab; }
    public GameObject PlayerPrefab { get => playerPrefab; }

    public Client Client { get; private set; }
    private ActionQueue actionQueue;

    /// <summary>Encapsulates a method that handles a message from the server.</summary>
    /// <param name="message">The message that was received.</param>
    public delegate void MessageHandler(Message message);
    private Dictionary<ushort, MessageHandler> messageHandlers;

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
        Client.MessageReceived += MessageReceived;
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
        messageHandlers = new Dictionary<ushort, MessageHandler>()
        {
            { (ushort)ServerToClientId.spawnPlayer, ClientHandle.SpawnPlayer },
            { (ushort)ServerToClientId.playerMovement, ClientHandle.PlayerMovement },
        };
        Client.Connect(ip, port, actionQueue);
    }

    private void DidConnect(object sender, EventArgs e)
    {
        UIManager.Singleton.SendName();
    }

    private void MessageReceived(object sender, ClientMessageReceivedEventArgs e)
    {
        messageHandlers[e.Message.GetUShort()](e.Message);
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        Destroy(Player.list[e.Id].gameObject);
    }
}
