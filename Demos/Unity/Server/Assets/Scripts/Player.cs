using RiptideNetworking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> List { get; private set; } = new Dictionary<ushort, Player>();

    public ushort Id { get; private set; }
    public string Username { get; private set; }

    [SerializeField] private CharacterController controller;
    [SerializeField] private float gravity;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float jumpSpeed;

    private bool[] inputs;
    private float yVelocity;

    private void OnValidate()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        gravity *= Time.fixedDeltaTime * Time.fixedDeltaTime;
        moveSpeed *= Time.fixedDeltaTime;
        jumpSpeed *= Time.fixedDeltaTime;

        inputs = new bool[5];
    }

    private void FixedUpdate()
    {
        Vector2 inputDirection = Vector2.zero;
        if (inputs[0])
        {
            inputDirection.y += 1;
        }
        if (inputs[1])
        {
            inputDirection.y -= 1;
        }
        if (inputs[2])
        {
            inputDirection.x -= 1;
        }
        if (inputs[3])
        {
            inputDirection.x += 1;
        }

        Move(inputDirection);
    }

    private void Move(Vector2 inputDirection)
    {
        Vector3 moveDirection = transform.right * inputDirection.x + transform.forward * inputDirection.y;
        moveDirection *= moveSpeed;

        if (controller.isGrounded)
        {
            yVelocity = 0f;
            if (inputs[4])
            {
                yVelocity = jumpSpeed;
            }
        }
        yVelocity += gravity;

        moveDirection.y = yVelocity;
        controller.Move(moveDirection);

        SendMovement();
    }

    public void SetInput(bool[] inputs, Vector3 forward)
    {
        this.inputs = inputs;
        forward.y = 0; // Keep the player upright
        transform.forward = forward;
    }

    private void OnDestroy()
    {
        List.Remove(Id);
    }

    public static void Spawn(ushort id, string username)
    {
        Player player = Instantiate(NetworkManager.Singleton.PlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity).GetComponent<Player>();
        player.name = $"Player {id} ({(username == "" ? "Guest" : username)})";
        player.Id = id;
        player.Username = username;

        player.SendSpawn();
        List.Add(player.Id, player);
    }

    #region Messages
    /// <summary>Sends a player's info to the given client.</summary>
    /// <param name="toClient">The client to send the message to.</param>
    public void SendSpawn(ServerClient toClient)
    {
        NetworkManager.Singleton.Server.Send(GetSpawnData(Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawnPlayer)), toClient);
    }
    /// <summary>Sends a player's info to all clients.</summary>
    private void SendSpawn()
    {
        NetworkManager.Singleton.Server.SendToAll(GetSpawnData(Message.Create(MessageSendMode.reliable, (ushort)ServerToClientId.spawnPlayer)));
    }

    private Message GetSpawnData(Message message)
    {
        message.Add(Id);
        message.Add(Username);
        message.Add(transform.position);
        return message;
    }

    private void SendMovement()
    {
        Message message = Message.Create(MessageSendMode.unreliable, (ushort)ServerToClientId.playerMovement);
        message.Add(Id);
        message.Add(transform.position);
        message.Add(transform.forward);
        NetworkManager.Singleton.Server.SendToAll(message);
    }
    #endregion
}
