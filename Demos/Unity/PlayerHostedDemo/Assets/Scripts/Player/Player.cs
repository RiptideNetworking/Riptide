using System.Collections.Generic;
using UnityEngine;

namespace Riptide.Demos.Rudp.PlayerHosted
{
    public class Player : MonoBehaviour
    {
        internal static Dictionary<ushort, Player> List = new Dictionary<ushort, Player>();

        internal ushort Id;
        private string username;

        private void OnDestroy()
        {
            List.Remove(Id);
        }

        private void Move(Vector3 newPosition, Vector3 forward)
        {
            transform.position = newPosition;
            forward.y = 0;
            transform.forward = forward.normalized;
        }

        internal static void Spawn(ushort id, string username, Vector3 position, bool shouldSendSpawn = false)
        {
            Player player;
            if (id == NetworkManager.Singleton.Client.Id)
                player = Instantiate(NetworkManager.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            else
                player = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<Player>();

            player.Id = id;
            player.username = username;
            player.name = $"Player {id} ({username})";

            List.Add(id, player);
            if (shouldSendSpawn)
                player.SendSpawn();
        }

        #region Messages
        private void SendSpawn()
        {
            Message message = Message.Create(MessageSendMode.reliable, MessageId.spawnPlayer, shouldAutoRelay: true);
            message.AddUShort(Id);
            message.AddString(username);
            message.AddVector3(transform.position);
            NetworkManager.Singleton.Client.Send(message);
        }

        [MessageHandler((ushort)MessageId.spawnPlayer)]
        private static void SpawnPlayer(Message message)
        {
            Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
        }

        internal void SendSpawn(ushort newPlayerId)
        {
            Message message = Message.Create(MessageSendMode.reliable, MessageId.spawnPlayer);
            message.AddUShort(newPlayerId);
            message.AddUShort(Id);
            message.AddString(username);
            message.AddVector3(transform.position);
            NetworkManager.Singleton.Client.Send(message);
        }

        [MessageHandler((ushort)MessageId.spawnPlayer)]
        private static void SpawnPlayer(ushort fromClientId, Message message)
        {
            ushort newPlayerId = message.GetUShort();
            Message messageToSend = Message.Create(MessageSendMode.reliable, MessageId.spawnPlayer);
            messageToSend.AddUShort(message.GetUShort());
            messageToSend.AddString(message.GetString());
            messageToSend.AddVector3(message.GetVector3());
            NetworkManager.Singleton.Server.Send(messageToSend, newPlayerId);
        }

        [MessageHandler((ushort)MessageId.playerMovement)]
        private static void PlayerMovement(Message message)
        {
            ushort playerId = message.GetUShort();
            if (List.TryGetValue(playerId, out Player player))
                player.Move(message.GetVector3(), message.GetVector3());
        }
        #endregion
    }
}
