using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Riptide.Demos.ConsoleServer
{
    internal class Program
    {
        private static Server server;
        private static bool isRunning;

        private static bool isRoundTripTest;
        private static int testIdAmount;
        private static List<int> remainingTestIds;
        private static Timer testEndWaitTimer;
        private static int duplicateCount;

        private static void Main()
        {
            Console.Title = "Server";
            
            RiptideLogger.Initialize(Console.WriteLine, true);
            isRunning = true;

            new Thread(new ThreadStart(Loop)).Start();

            Console.WriteLine("Press enter to stop the server at any time.");
            Console.ReadLine();

            isRunning = false;

            Console.ReadLine();
        }

        private static void Loop()
        {
            server = new Server
            {
                TimeoutTime = ushort.MaxValue // Max value timeout to avoid getting timed out for as long as possible when testing with very high loss rates (if all heartbeat messages are lost during this period of time, it will trigger a disconnection)
            };
            server.Start(7777, 10);

            while (isRunning)
            {
                server.Update();
                Thread.Sleep(10);
            }

            server.Stop();
        }

        [MessageHandler((ushort)MessageId.StartTest)]
        private static void HandleStartTest(ushort fromClientId, Message message)
        {
            isRoundTripTest = message.GetBool();
            testIdAmount = message.GetInt();

            if (!isRoundTripTest)
            {
                duplicateCount = 0;
                remainingTestIds = new List<int>(testIdAmount);
                for (int i = 0; i < testIdAmount; i++)
                    remainingTestIds.Add(i + 1);
            }

            server.Send(Message.Create(MessageSendMode.Reliable, MessageId.StartTest).AddBool(isRoundTripTest).AddInt(testIdAmount), fromClientId);
        }

        private static void SendTestMessage(ushort fromClientId, int reliableTestId)
        {
            Message message = Message.Create(MessageSendMode.Reliable, MessageId.TestMessage);
            message.AddInt(reliableTestId);

            server.Send(message, fromClientId);
        }

        [MessageHandler((ushort)MessageId.TestMessage)]
        private static void HandleTestMessage(ushort fromClientId, Message message)
        {
            int reliableTestId = message.GetInt();

            if (isRoundTripTest)
                SendTestMessage(fromClientId, reliableTestId);
            else
            {
                lock (remainingTestIds)
                {
                    if (!remainingTestIds.Remove(reliableTestId))
                    {
                        duplicateCount++;
                        Console.WriteLine($"Duplicate message received (Test ID: {reliableTestId}).");
                    }
                }
            }

            if (reliableTestId > testIdAmount - 25 && testEndWaitTimer == null)
            {
                testEndWaitTimer = new Timer(5000);
                testEndWaitTimer.Elapsed += (s, e) => ReliabilityTestEnded();
                testEndWaitTimer.AutoReset = false;
                testEndWaitTimer.Start();
            }
        }

        private static void ReliabilityTestEnded()
        {
            Console.WriteLine();

            if (isRoundTripTest)
                Console.WriteLine("Round-trip reliability test complete! See client console for results.");
            else
            {
                Console.WriteLine("One-way reliability test complete:");
                Console.WriteLine($"  Messages sent: {testIdAmount}");
                Console.WriteLine($"  Messages lost: {remainingTestIds.Count}");
                Console.WriteLine($"  Duplicates:    {duplicateCount}");
                Console.WriteLine($"  Latency (RTT): {server.Clients[0].SmoothRTT}ms"); // Won't work with more than 1 client connected, but this demo isn't designed to work with more than 1 client anyways
                if (remainingTestIds.Count > 0)
                    Console.WriteLine($"  Test IDs lost: {string.Join(",", remainingTestIds)}");
                if (duplicateCount > 0)
                    Console.WriteLine("\nThis demo sends 5 reliable messages every 10 milliseconds during the test, which is quite an extreme use\n" +
                        "case. Riptide's duplicate filter has a limited range, and a very high reliable message send rate will push\n" +
                        "it to its limit once combined with any amount of packet loss.\n\n" +
                        "If a message is sent, received by the other end, but the corresponding ack packet is lost or delayed, the\n" +
                        "sender will initiate a resend. While most duplicates will be filtered out even at high send rates, in the\n" +
                        "case of an unnecessary resend like this, the second message may not be filtered out if enough other reliable\n" +
                        "messages were sent after the first send (the duplicate filter only tracks the last 80 sequence IDs).\n\n" +
                        "If you are simulating latency & packet loss with an app like Clumsy, you'll notice that increasing those\n" +
                        "values will also increase the number of duplicates that aren't filtered out.\n\n" +
                        "To reduce the amount of duplicate messages that Riptide does NOT manage to filter out, applications should\n" +
                        "send reliable messages at a more reasonable rate (unlike this demo). However, applications should also be\n" +
                        "prepared to handle duplicate messages. Riptide can only filter out duplicates based on sequence ID, but if\n" +
                        "(for example) a hacker modifies his client to send a message twice with different sequence IDs and your\n" +
                        "server is not prepared for that, you may end up with issues such as players being spawned multiple times.");
            }

            Console.WriteLine();
        }
    }

    public enum MessageId : ushort
    {
        StartTest = 1,
        TestMessage
    }
}
