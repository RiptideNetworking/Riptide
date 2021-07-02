using RiptideNetworking;
using UnityEngine;

public class ServerHandle : MonoBehaviour
{
    public static void PlayerName(ServerClient fromClient, Message message)
    {
        Player.Spawn(fromClient.Id, message.GetString());
    }

    public static void PlayerInput(ServerClient fromClient, Message message)
    {
        Player player = Player.List[fromClient.Id];
        message.GetBools(5, player.Inputs);
        player.SetForwardDirection(message.GetVector3());
    }
}
