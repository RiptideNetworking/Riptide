using System.Collections.Generic;
using UnityEngine;

namespace Riptide.Demos.DedicatedClient
{
    public class Player : MonoBehaviour
    {
        public static Dictionary<ushort, Player> list = new Dictionary<ushort, Player>();

        [SerializeField] private ushort id;
        [SerializeField] private string username;

        public void Move(Vector3 newPosition, Vector3 forward)
        {
            transform.position = newPosition;

            if (id != NetworkManager.Singleton.Client.Id) // Don't overwrite local player's forward direction to avoid noticeable rotational snapping
                transform.forward = forward;
        }

        private void OnDestroy()
        {
            list.Remove(id);
        }

        public static void Spawn(ushort id, string username, Vector3 position)
        {
            Player player;
            if (id == NetworkManager.Singleton.Client.Id)
                player = Instantiate(NetworkManager.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            else
                player = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<Player>();

            player.name = $"Player {id} ({username})";
            player.id = id;
            player.username = username;
            list.Add(player.id, player);
        }

        #region Messages
        [MessageHandler((ushort)ServerToClientId.SpawnPlayer)]
        private static void SpawnPlayer(Message message)
        {
            Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
        }

        [MessageHandler((ushort)ServerToClientId.PlayerMovement)]
        private static void PlayerMovement(Message message)
        {
            ushort playerId = message.GetUShort();
            if (list.TryGetValue(playerId, out Player player))
                player.Move(message.GetVector3(), message.GetVector3());
        }
        #endregion
    }
}
