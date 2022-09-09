using System.Collections.Generic;
using UnityEngine;

namespace Riptide.Demos.DedicatedServer
{
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

        public bool[] Inputs { get; set; }
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

            Inputs = new bool[5];
        }

        private void FixedUpdate()
        {
            Vector2 inputDirection = Vector2.zero;
            if (Inputs[0])
                inputDirection.y += 1;

            if (Inputs[1])
                inputDirection.y -= 1;

            if (Inputs[2])
                inputDirection.x -= 1;

            if (Inputs[3])
                inputDirection.x += 1;

            Move(inputDirection);
        }

        private void Move(Vector2 inputDirection)
        {
            Vector3 moveDirection = transform.right * inputDirection.x + transform.forward * inputDirection.y;
            moveDirection *= moveSpeed;

            if (controller.isGrounded)
            {
                yVelocity = 0f;
                if (Inputs[4])
                    yVelocity = jumpSpeed;
            }
            yVelocity += gravity;

            moveDirection.y = yVelocity;
            controller.Move(moveDirection);

            SendMovement();
        }

        public void SetForwardDirection(Vector3 forward)
        {
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
        public void SendSpawn(ushort toClient)
        {
            NetworkManager.Singleton.Server.Send(GetSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.SpawnPlayer)), toClient);
        }
        /// <summary>Sends a player's info to all clients.</summary>
        private void SendSpawn()
        {
            NetworkManager.Singleton.Server.SendToAll(GetSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.SpawnPlayer)));
        }

        private Message GetSpawnData(Message message)
        {
            message.AddUShort(Id);
            message.AddString(Username);
            message.AddVector3(transform.position);
            return message;
        }

        private void SendMovement()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.PlayerMovement);
            message.AddUShort(Id);
            message.AddVector3(transform.position);
            message.AddVector3(transform.forward);
            NetworkManager.Singleton.Server.SendToAll(message);
        }

        [MessageHandler((ushort)ClientToServerId.PlayerName)]
        private static void PlayerName(ushort fromClientId, Message message)
        {
            Spawn(fromClientId, message.GetString());
        }

        [MessageHandler((ushort)ClientToServerId.PlayerInput)]
        private static void PlayerInput(ushort fromClientId, Message message)
        {
            Player player = List[fromClientId];
            message.GetBools(5, player.Inputs);
            player.SetForwardDirection(message.GetVector3());
        }
        #endregion
    }
}
