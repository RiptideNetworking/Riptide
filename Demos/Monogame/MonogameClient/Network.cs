// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monogame.Common;
using RiptideNetworking;

namespace Monogame.TestClient
{
    public static class Network
    {
        public static Client Client { get; set; }
        public static Dictionary<ushort, Player> RemoteClients { get; set; }

        public static void Init()
        {
            Client = new Client();
            Client.Connect("127.0.0.1:7777");

            Client.ClientConnected += (s, e) => ClientConnected(e.Id);
            Client.ClientDisconnected += (s, e) => ClientDisconnected(e.Id);

            RemoteClients = new Dictionary<ushort, Player>();
        }

        [MessageHandler((ushort)MessageId.PlayerPosition)]
        public static void HandlePlayerMovement(Message message)
        {
            ushort id = message.GetUShort();
            Vector2 pos = message.GetVector2();

            if (RemoteClients.TryGetValue(id, out Player player))
            {
                player.Position = pos;
            }
        }

        [MessageHandler((ushort)MessageId.PlayerConnected)]
        public static void HandlePlayerConnection(Message message)
        {
            ushort[] ids = message.GetUShorts();

            for (int i = 0; i < ids.Length; i++)
            {
                ClientConnected(ids[i]);
            }
        }

        private static void ClientDisconnected(ushort id)
        {
            _ = RemoteClients.Remove(id);
        }

        private static void ClientConnected(ushort id)
        {
            RemoteClients.Add(id, new Player());
        }
    }
}
