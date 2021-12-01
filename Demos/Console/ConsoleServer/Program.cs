
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking;
using RiptideNetworking.Transports.RudpTransport;
using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using Timer = System.Timers.Timer;

namespace ConsoleServer
{
    class Program
    {
        private static Server server;
        private static readonly Dictionary<ushort, Player> players = new Dictionary<ushort, Player>();
        private static bool isRunning;

        private static bool isRoundTripTest;
        private static int testIdAmount;
        private static List<int> remainingTestIds;
        private static Timer testEndWaitTimer;

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
            server = new Server(new RudpServer(ushort.MaxValue)); // Max value timeout to avoid getting timed out for as long as possible when testing with very high loss rates (if all heartbeat messages are lost during this period of time, it will trigger a disconnection)
            server.ClientConnected += (s, e) => players.Add(e.Client.Id, new Player(e.Client));
            server.ClientDisconnected += (s, e) => players.Remove(e.Id);

            server.Start(7777, 10);

            while (isRunning)
            {
                server.Tick();
                Thread.Sleep(10);
            }

            server.Stop();
        }

        [MessageHandler((ushort)MessageId.startTest)]
        private static void HandleStartTest(ushort fromClientId, Message message)
        {
            isRoundTripTest = message.GetBool();
            testIdAmount = message.GetInt();

            if (!isRoundTripTest)
            {
                remainingTestIds = new List<int>(testIdAmount);
                for (int i = 0; i < testIdAmount; i++)
                    remainingTestIds.Add(i + 1);
            }

            server.Send(Message.Create(MessageSendMode.reliable, (ushort)MessageId.startTest, 25).Add(isRoundTripTest).Add(testIdAmount), fromClientId);
        }

        private static void SendTestMessage(ushort fromClientId, int reliableTestId)
        {
            Message message = Message.Create(MessageSendMode.reliable, (ushort)MessageId.testMessage);
            message.Add(reliableTestId);

            server.Send(message, fromClientId);
        }

        [MessageHandler((ushort)MessageId.testMessage)]
        private static void HandleTestMessage(ushort fromClientId, Message message)
        {
            int reliableTestId = message.GetInt();

            if (isRoundTripTest)
                SendTestMessage(fromClientId, reliableTestId);
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