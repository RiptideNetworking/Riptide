
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2022 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking;
using RiptideNetworking.Utils;
using System;
using System.Threading;

namespace RiptideDemos.RudpTransport.MonoGame.TestServer
{
    internal class Program
    {
        internal static Server Server { get; private set; }

        private static void Main()
        {
            RiptideLogger.Initialize(Console.WriteLine, false);

            Server = new Server();
            Server.Start(7777, 4);

            Server.ClientConnected += (s, e) => new Player(e.Client.Id);
            Server.ClientDisconnected += (s, e) => Player.List.Remove(e.Id);

            while (true)
            {
                Server.Tick();
                Player.SendPositions();

                Thread.Sleep(10);
            }
        }
    }
}
