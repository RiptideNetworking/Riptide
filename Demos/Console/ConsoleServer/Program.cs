using RiptideNetworking;
using System;
using System.Collections.Generic;

namespace ConsoleServer
{
    class Program
    {
        private static Server server = new Server();
        private static Dictionary<ushort, Player> players = new Dictionary<ushort, Player>();

        private static void Main(string[] args)
        {
            Console.Title = "Server";
            
            RiptideLogger.Initialize(Console.WriteLine, true);

            Dictionary<ushort, Server.MessageHandler> messageHandlers = new Dictionary<ushort, Server.MessageHandler>()
            {
                { (ushort)MessageIDs.reliableTest, HandleReliableTest }
            };

            server.Start(7777, 10, messageHandlers);

            server.ClientConnected += (s, e) => players.Add(e.Client.Id, new Player(e.Client));
            server.ClientDisconnected += (s, e) => players.Remove(e.Id);

            Console.ReadKey();

            server.Stop();

            Console.ReadKey();
        }

        private static void SendReliableTest(ServerClient fromClient, int reliableTestId)
        {
            Message message = new Message((ushort)MessageIDs.reliableTest);
            message.Add(reliableTestId);

            server.SendReliable(message, fromClient);
        }

        private static void HandleReliableTest(ServerClient fromClient, Message message)
        {
            int reliableTestId = message.GetInt();

            SendReliableTest(fromClient, reliableTestId);
        }
    }

    public enum MessageIDs : ushort
    {
        reliableTest = 1
    }
}