using RiptideNetworking;
using System;
using System.Collections.Generic;

namespace ConsoleServer
{
    class Program
    {
        private static Server server = new Server();
        private static Dictionary<ushort, Player> players = new Dictionary<ushort, Player>();

        /// <summary>Encapsulates a method that handles a message from a certain client.</summary>
        /// <param name="fromClient">The client from whom the message was received.</param>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(ServerClient fromClient, Message message);
        private static Dictionary<ushort, MessageHandler> messageHandlers;

        private static void Main(string[] args)
        {
            Console.Title = "Server";
            
            RiptideLogger.Initialize(Console.WriteLine, true);

            messageHandlers = new Dictionary<ushort, MessageHandler>()
            {
                { (ushort)MessageId.reliableTest, HandleReliableTest }
            };

            server.Start(7777, 10);

            server.ClientConnected += (s, e) => players.Add(e.Client.Id, new Player(e.Client));
            server.MessageReceived += (s, e) => messageHandlers[e.Message.GetUShort()](e.FromClient, e.Message);
            server.ClientDisconnected += (s, e) => players.Remove(e.Id);

            Console.ReadKey();

            server.Stop();

            Console.ReadKey();
        }

        private static void SendReliableTest(ServerClient fromClient, int reliableTestId)
        {
            Message message = new Message(MessageSendMode.reliable, (ushort)MessageId.reliableTest, 4);
            message.Add(reliableTestId);

            server.Send(message, fromClient);
        }

        private static void HandleReliableTest(ServerClient fromClient, Message message)
        {
            int reliableTestId = message.GetInt();

            SendReliableTest(fromClient, reliableTestId);
        }
    }

    public enum MessageId : ushort
    {
        reliableTest = 1
    }
}