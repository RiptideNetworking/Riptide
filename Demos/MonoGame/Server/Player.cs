
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2022 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Microsoft.Xna.Framework;
using RiptideDemos.RudpTransport.MonoGame.Common;
using RiptideNetworking;
using System.Collections.Generic;

namespace RiptideDemos.RudpTransport.MonoGame.TestServer
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
            Message message = Message.Create(MessageSendMode.reliable, MessageId.PlayerSpawn);
            message.AddUShort(id);
            message.AddVector2(position);
            return message;
        }

        internal void SendPosition()
        {
            Message message = Message.Create(MessageSendMode.unreliable, MessageId.PlayerPosition);
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
