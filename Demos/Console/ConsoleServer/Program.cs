using RiptideNetworking;
using System;
using System.Collections.Generic;
using System.Timers;

namespace ConsoleServer
{
    class Program
    {
        private static readonly Server server = new Server();
        private static readonly Dictionary<ushort, Player> players = new Dictionary<ushort, Player>();

        /// <summary>Encapsulates a method that handles a message from a certain client.</summary>
        /// <param name="fromClient">The client from whom the message was received.</param>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(ServerClient fromClient, Message message);
        private static Dictionary<ushort, MessageHandler> messageHandlers;

        private static bool isRoundTripTest;
        private static int testIdAmount;
        private static List<int> remainingTestIds;
        private static Timer testEndWaitTimer;

        private static void Main()
        {
            Console.Title = "Server";
            
            RiptideLogger.Initialize(Console.WriteLine, true);

            messageHandlers = new Dictionary<ushort, MessageHandler>()
            {
                { (ushort)MessageId.startTest, HandleStartTest },
                { (ushort)MessageId.testMessage, HandleTestMessage }
            };

            server.ClientConnected += (s, e) => players.Add(e.Client.Id, new Player(e.Client));
            server.MessageReceived += (s, e) => messageHandlers[e.Message.GetUShort()](e.FromClient, e.Message);
            server.ClientDisconnected += (s, e) => players.Remove(e.Id);

            server.Start(7777, 10);
            server.ClientTimeoutTime = ushort.MaxValue; // Avoid getting timed out for as long as possible when testing with very high loss rates (if all heartbeat messages are lost during this period of time, it will trigger a disconnection)

            Console.WriteLine("Press enter to stop the server at any time.");
            Console.ReadLine();
            server.Stop();

            Console.ReadLine();
        }

        private static void HandleStartTest(ServerClient fromClient, Message message)
        {
            isRoundTripTest = message.GetBool();
            testIdAmount = message.GetInt();

            if (!isRoundTripTest)
            {
                remainingTestIds = new List<int>(testIdAmount);
                for (int i = 0; i < testIdAmount; i++)
                    remainingTestIds.Add(i + 1);
            }

            server.Send(Message.Create(MessageSendMode.reliable, (ushort)MessageId.startTest).Add(isRoundTripTest).Add(testIdAmount), fromClient, 25);
        }

        
        private static void SendTestMessage(ServerClient fromClient, int reliableTestId)
        {
            Message message = Message.Create(MessageSendMode.reliable, (ushort)MessageId.testMessage);
            message.Add(reliableTestId);

            server.Send(message, fromClient);
        }

        private static void HandleTestMessage(ServerClient fromClient, Message message)
        {
            int reliableTestId = message.GetInt();

            if (isRoundTripTest)
                SendTestMessage(fromClient, reliableTestId);
            else
            {
                lock (remainingTestIds)
                    if (!remainingTestIds.Remove(reliableTestId))
                        Console.WriteLine($"Duplicate message received (Test ID: {reliableTestId}).");
            }

            if (reliableTestId > testIdAmount - 25 && testEndWaitTimer == null)
            {
                testEndWaitTimer = new Timer(20000);
                testEndWaitTimer.Elapsed += (s, e) => ReliabilityTestEnded();
                testEndWaitTimer.AutoReset = false;
                testEndWaitTimer.Start();
            }
        }

        private static void ReliabilityTestEnded()
        {
            Console.WriteLine();

            if (isRoundTripTest)
                Console.WriteLine("Reliability test complete (round-trip)! See client console for results.");
            else
            {
                Console.WriteLine("Reliability test complete (one-way):");
                Console.WriteLine($"  Messages sent: {testIdAmount}");
                Console.WriteLine($"  Messages lost: {remainingTestIds.Count}");
                if (remainingTestIds.Count > 0)
                    Console.WriteLine($"  Test IDs lost: {string.Join(",", remainingTestIds)}");
            }

            Console.WriteLine();
        }
    }

    public enum MessageId : ushort
    {
        startTest = 1,
        testMessage
    }
}