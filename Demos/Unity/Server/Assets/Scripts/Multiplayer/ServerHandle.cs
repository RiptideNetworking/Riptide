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
        Player.List[fromClient.Id].SetInput(message.GetBoolArray(5), message.GetVector3());
    }
}
