using Riptide.Utils;
using System;
using System.Threading;

namespace Riptide.Demos.MGServer
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
            Server.ClientDisconnected += (s, e) => Player.List.Remove(e.Client.Id);

            while (true)
            {
                Server.Update();
                Player.SendPositions();

                Thread.Sleep(10);
            }
        }
    }
}
