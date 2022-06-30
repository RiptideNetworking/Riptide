﻿using Riptide.Utils;
using System;
using System.Threading;

namespace Riptide.Demos.Rudp.MGServer
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
