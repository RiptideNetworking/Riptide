using Microsoft.Xna.Framework;
using Riptide.Demos.MGCommon;
using System.Collections.Generic;

namespace Riptide.Demos.MGServer
{
    internal class Player
    {
        internal static readonly Dictionary<ushort, Player> List = new Dictionary<ushort, Player>();

        private readonly ushort id;
        private Vector2 position = Vector2.Zero;

        internal Player(ushort clientId)
        {
            id = clientId;

            foreach (Player otherPlayer in List.Values)
                Program.Server.Send(otherPlayer.CreateSpawnMessage(), id);
            
            List.Add(clientId, this);
            Program.Server.SendToAll(CreateSpawnMessage());
        }

        internal static void SendPositions()
        {
            foreach (Player player in List.Values)
                player.SendPosition();
        }

        private Message CreateSpawnMessage()
        {
            Message message = Message.Create(MessageSendMode.Reliable, MessageId.PlayerSpawn);
            message.AddUShort(id);
            message.AddVector2(position);
            return message;
        }

        internal void SendPosition()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, MessageId.PlayerPosition);
            message.AddUShort(id);
            message.AddVector2(position);
            Program.Server.SendToAll(message, id);
        }

        [MessageHandler((ushort)MessageId.PlayerPosition)]
        private static void HandlePosition(ushort fromClientId, Message message)
        {
            if (List.TryGetValue(fromClientId, out Player player))
                player.position = message.GetVector2();
        }
    }
}
