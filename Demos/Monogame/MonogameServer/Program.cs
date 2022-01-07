// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Microsoft.Xna.Framework;
using Monogame.Common;
using RiptideNetworking;
using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Monogame.TestServer
{
    internal class Program
    {
        private static Server server;

        private static List<ushort> Players = new List<ushort>();

        public static void Main()
        {
            RiptideLogger.Initialize(Console.WriteLine, false);

            server = new Server();
            server.Start(7777, 4);

            server.ClientConnected += (s, e) => OnPlayerConnected(e.Client.Id);
            server.ClientDisconnected += (s, e) => OnPlayerDisconnected(e.Id);

            while (true)
            {
                server.Tick();
                Thread.Sleep(10);
            }
        }

        [MessageHandler((ushort)MessageId.PlayerPosition)]
        public static void HandlePlayerMovement(ushort fromClientId, Message message)
        {
            Vector2 x = message.GetVector2();

            // Console.WriteLine($"Player {fromClientId} : {x}, {y}");

            Message msg = Message.Create(MessageSendMode.unreliable, (ushort)MessageId.PlayerPosition);
            msg.AddUShort(fromClientId);
            msg.AddVector2(x);
            server.SendToAll(msg, fromClientId);
        }

        public static void OnPlayerConnected(ushort id)
        {
            Message msg = Message.Create(MessageSendMode.reliable, (ushort)MessageId.PlayerConnected);
            msg.AddUShorts(Players.ToArray());
            server.Send(msg, id);

            Players.Add(id);
        }

        private static void OnPlayerDisconnected(ushort id)
        {
            Players.Remove(id);
        }
    }
}
