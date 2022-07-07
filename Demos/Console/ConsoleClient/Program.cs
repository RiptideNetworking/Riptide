using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Riptide.Demos.Rudp.ConsoleClient
{
    internal class Program
    {
        private static Client client;
        private static bool isRunning;

        private static bool isRoundTripTest;
        private static bool isTestRunning;
        private static int nextReliableTestId = 1;
        private static readonly int testIdAmount = 10000;
        private static List<int> remainingTestIds;
        private static Timer testEndWaitTimer;
        private static int duplicateCount;
        private static int sendCount;

        private static void Main()
        {
            Console.Title = "Client";

            Console.WriteLine("Press\n  1 to start a one-way test\n  2 to start a round-trip test");
            Console.WriteLine();

            while (true)
            {
                ConsoleKey pressedKey = Console.ReadKey().Key;
                if (pressedKey == ConsoleKey.D1)
                {
                    isRoundTripTest = false;
                    break;
                }
                else if (pressedKey == ConsoleKey.D2)
                {
                    isRoundTripTest = true;
                    break;
                }
            }
            Console.SetCursorPosition(0, Console.CursorTop);

            RiptideLogger.Initialize(Console.WriteLine, true);
            isRunning = true;

            new Thread(new ThreadStart(Loop)).Start();

            Console.ReadLine();

            isRunning = false;
            
            Console.ReadLine();
        }

        private static void Loop()
        {
            client = new Client
            {
                TimeoutTime = ushort.MaxValue // Max value timeout to avoid getting timed out for as long as possible when testing with very high loss rates (if all heartbeat messages are lost during this period of time, it will trigger a disconnection)
            };
            client.Connected += (s, e) => Connected();
            client.Disconnected += (s, e) => Disconnected();
            client.Connect("127.0.0.1:7777");

            while (isRunning)
            {
                client.Tick();

                if (isTestRunning)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        if (sendCount < testIdAmount)
                        {
                            SendTestMessage(nextReliableTestId++);
                            sendCount++;
                        }
                    }
                }

                Thread.Sleep(10);
            }

            client.Disconnect();
            Disconnected();
        }

        private static void Connected()
        {
            Console.WriteLine();
            Console.WriteLine("Press enter to disconnect at any time.");

            client.Send(Message.Create(MessageSendMode.reliable, MessageId.startTest, 25).AddBool(isRoundTripTest).AddInt(testIdAmount));
        }

        private static void Disconnected()
        {
            if (testEndWaitTimer != null)
                testEndWaitTimer.Stop();

            if (isTestRunning)
            {
                Console.WriteLine();
                Console.WriteLine($"Cancelled reliability test ({(isRoundTripTest ? "round-trip" : "one-way")}) due to disconnection.");
                Console.WriteLine();
            }

            isTestRunning = false;
        }

        [MessageHandler((ushort)MessageId.startTest)]
        private static void HandleStartTest(Message message)
        {
            if (message.GetBool() != isRoundTripTest || message.GetInt() != testIdAmount)
            {
                Console.WriteLine();
                Console.WriteLine("Test initialization failed! Please try again.");
                return;
            }

            if (!isTestRunning)
                StartReliabilityTest();
        }

        private static void StartReliabilityTest()
        {
            isTestRunning = true;
            duplicateCount = 0;
            sendCount = 0;

            remainingTestIds = new List<int>(testIdAmount);
            for (int i = 0; i < testIdAmount; i++)
                remainingTestIds.Add(i + 1);

            Console.WriteLine($"Starting reliability test ({(isRoundTripTest ? "round-trip" : "one-way")})!");

            testEndWaitTimer = new Timer(20000);
            testEndWaitTimer.Elapsed += (e, s) => ReliabilityTestEnded();
            testEndWaitTimer.AutoReset = false;
            testEndWaitTimer.Start();
        }

        private static void ReliabilityTestEnded()
        {
            Console.WriteLine();

            if (isRoundTripTest)
            {
                Console.WriteLine("Reliability test complete (round-trip):");
                Console.WriteLine($"  Messages sent: {testIdAmount}");
                Console.WriteLine($"  Messages lost: {remainingTestIds.Count}");
                Console.WriteLine($"  Duplicates:    {duplicateCount}");
                Console.WriteLine($"  Latency (RTT): {client.SmoothRTT}ms");
                if (remainingTestIds.Count > 0)
                    Console.WriteLine($"  Test IDs lost: {string.Join(",", remainingTestIds)}");
                if (duplicateCount > 0)
                    Console.WriteLine("\nThis demo sends a reliable message every 2-3 milliseconds during the test, which is a very extreme use case.\n" +
                        "Riptide's duplicate filter has a limited range, and a send rate like 33-50 reliable messages per 100ms will\n" +
                        "max it out fast once combined with any amount of packet loss.\n\n" +
                        "Most duplicates will be filtered out even at high send rates, but any messages that need to be resent due to\n" +
                        "packet loss (and therefore take longer to arrive) are highly likely to be missed by Riptide's filter. If you\n" +
                        "are simulating latency & packet loss with an app like Clumsy, you'll notice that increasing those numbers\n" +
                        "will also increase the number of duplicates that aren't filtered out.\n\n" +
                        "To reduce the amount of duplicate messages that Riptide does NOT manage to filter out, applications should\n" +
                        "send reliable messages at a reasonable rate (unlike this demo). However, applications should also be\n" +
                        "prepared to handle duplicate messages. Riptide can only filter out duplicates based on sequence ID, but if\n" +
                        "(for example) a hacker modifies his client to send a message twice with different sequence IDs and your\n" +
                        "server is not prepared for that, you may end up with issues such as players being spawned multiple times.");
            }
            else
                Console.WriteLine("Reliability test complete (one-way)! See server console for results.");
            
            Console.WriteLine();
            isTestRunning = false;
        }

        private static void SendTestMessage(int reliableTestId)
        {
            Message message = Message.Create(MessageSendMode.reliable, MessageId.testMessage);
            message.AddInt(reliableTestId);

            client.Send(message);
        }

        [MessageHandler((ushort)MessageId.testMessage)]
        private static void HandleTestMessage(Message message)
        {
            int reliableTestId = message.GetInt();

            lock (remainingTestIds)
            {
                if (!remainingTestIds.Remove(reliableTestId))
                {
                    duplicateCount++;
                    Console.WriteLine($"Duplicate message received (Test ID: {reliableTestId}).");
                }
            }
        }
    }

    public enum MessageId : ushort
    {
        startTest = 1,
        testMessage
    }
}
