using RiptideNetworking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientHandle : MonoBehaviour
{
    public static void SpawnPlayer(Message message)
    {
        Player.Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    public static void PlayerMovement(Message message)
    {
        ushort playerId = message.GetUShort();
        if (Player.list.TryGetValue(playerId, out Player player))
            player.Move(message.GetVector3(), message.GetVector3());
    }
}
