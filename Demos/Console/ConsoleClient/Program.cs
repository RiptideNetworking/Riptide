using RiptideNetworking;
using RiptideNetworking.Transports.RudpTransport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Timer = System.Timers.Timer;

namespace ConsoleClient
{
    class Program
    {
        private static Client client;
        private static bool isRunning;

        private static bool isRoundTripTest;
        private static bool isTestRunning;
        private static int nextReliableTestId = 1;
        private static readonly int testIdAmount = 10000;
        private static List<int> remainingTestIds;
        private static Timer testEndWaitTimer;

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
            client = new Client(new RudpClient(ushort.MaxValue)); // Max value timeout to avoid getting timed out for as long as possible when testing with very high loss rates (if all heartbeat messages are lost during this period of time, it will trigger a disconnection)
            client.Connected += (s, e) => Connected();
            client.Disconnected += (s, e) => Disconnected();
            client.Connect("127.0.0.1:7777");

            while (isRunning)
            {
                client.Tick();
                Thread.Sleep(10);
            }

            client.Disconnect();
            Disconnected();
        }

        private static void Connected()
        {
            Console.WriteLine();
            Console.WriteLine("Press enter to disconnect at any time.");

            client.Send(Message.Create(MessageSendMode.reliable, (ushort)MessageId.startTest).Add(isRoundTripTest).Add(testIdAmount), 25);
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
        public static void HandleStartTest(Message message)
        {
            if (message.GetBool() != isRoundTripTest || message.GetInt() != testIdAmount)
            {
                Console.WriteLine();
                Console.WriteLine("Test initialization failed! Please try again.");
                return;
            }

            if (!isTestRunning)
                new Thread(new ThreadStart(StartReliabilityTest)).Start(); // StartReliabilityTest blocks the thread it runs on, so we need to put it on a different thread to avoid blocking the receive thread
        }

        private static void StartReliabilityTest()
        {
            isTestRunning = true;

            remainingTestIds = new List<int>(testIdAmount);
            for (int i = 0; i < testIdAmount; i++)
            {
                remainingTestIds.Add(i + 1);
            }

            Console.WriteLine($"Starting reliability test ({(isRoundTripTest ? "round-trip" : "one-way")})!");

            Stopwatch sw = new Stopwatch();
            for (int i = 0; i < testIdAmount; i++)
            {
                if (!isTestRunning)
                    return;

                sw.Restart();
                while (sw.ElapsedMilliseconds < 2)
                {
                    // Wait
                }
                
                SendTestMessage(nextReliableTestId++);
            }

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
                if (remainingTestIds.Count > 0)
                    Console.WriteLine($"  Test IDs lost: {string.Join(",", remainingTestIds)}");
            }
            else
                Console.WriteLine("Reliability test complete (one-way)! See server console for results.");
            
            Console.WriteLine();
            isTestRunning = false;
        }

        private static void SendTestMessage(int reliableTestId)
        {
            Message message = Message.Create(MessageSendMode.reliable, (ushort)MessageId.testMessage);
            message.Add(reliableTestId);

            client.Send(message);
        }

        [MessageHandler((ushort)MessageId.testMessage)]
        public static void HandleTestMessage(Message message)
        {
            int reliableTestId = message.GetInt();

            lock (remainingTestIds)
            {
                if (!remainingTestIds.Remove(reliableTestId))
                    Console.WriteLine($"Duplicate message received (Test ID: {reliableTestId}).");
            }
        }
    }

    public enum MessageId : ushort
    {
        startTest = 1,
        testMessage
    }
}
