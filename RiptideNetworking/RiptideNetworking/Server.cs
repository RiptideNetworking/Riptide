
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Transports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RiptideNetworking
{
    /// <summary>A server that can accept connections from <see cref="Client"/>s.</summary>
    public class Server : Common
    {
        /// <inheritdoc cref="IServer.ClientConnected"/>
        public event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        /// <inheritdoc cref="IServer.MessageReceived"/>
        public event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        /// <inheritdoc cref="IServer.ClientDisconnected"/>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>Whether or not the server is currently running.</summary>
        public bool IsRunning { get; private set; }
        /// <inheritdoc cref="IServer.Port"/>
        public ushort Port => server.Port;
        /// <inheritdoc cref="IServer.Clients"/>
        public IConnectionInfo[] Clients => server.Clients;
        /// <inheritdoc cref="IServer.MaxClientCount"/>
        public ushort MaxClientCount => server.MaxClientCount;
        /// <inheritdoc cref="IServer.ClientCount"/>
        public int ClientCount => server.ClientCount;
        /// <inheritdoc/>
        public override bool ShouldOutputInfoLogs
        {
            get => server.ShouldOutputInfoLogs;
            set => server.ShouldOutputInfoLogs = value;
        }
        /// <summary>Encapsulates a method that handles a message from a certain client.</summary>
        /// <param name="fromClientId">The numeric ID of the client from whom the message was received.</param>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(ushort fromClientId, Message message);
        
        /// <summary>Methods used to handle messages, accessible by their corresponding message IDs.</summary>
        private Dictionary<ushort, MessageHandler> messageHandlers;
        /// <summary>The underlying server that is used for managing connections and sending and receiving data.</summary>
        private IServer server;

        /// <summary>Handles initial setup.</summary>
        /// <param name="server">The underlying server that is used for managing connections and sending and receiving data.</param>
        public Server(IServer server) => this.server = server;

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        public void Start(ushort port, ushort maxClientCount, byte messageHandlerGroupId = 0)
        {
            if (IsRunning)
                Stop();

            CreateMessageHandlersDictionary(Assembly.GetCallingAssembly(), messageHandlerGroupId);

            server.ClientConnected += ClientConnected;
            server.MessageReceived += OnMessageReceived;
            server.ClientDisconnected += ClientDisconnected;
            server.Start(port, maxClientCount);

            IsRunning = true;
        }

        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(Assembly assembly, byte messageHandlerGroupId)
        {
            MethodInfo[] methods = assembly.GetTypes()
                                           .SelectMany(t => t.GetMethods())
                                           .Where(m => m.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0)
                                           .ToArray();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            for (int i = 0; i < methods.Length; i++)
            {
                MessageHandlerAttribute attribute = methods[i].GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    break;

                if (!methods[i].IsStatic)
                {
                    RiptideLogger.Log("ERROR", $"Message handler methods should be static, but '{methods[i].DeclaringType}.{methods[i].Name}' is an instance method!");
                    break;
                }

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Server instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                        RiptideLogger.Log("ERROR", $"Message handler method (type: server) already exists for message ID {attribute.MessageId}! Only one handler method is allowed per ID!");
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Server instances, but it might be one for Client instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Client.MessageHandler), methods[i], false);
                    if (serverMessageHandler == null)
                        RiptideLogger.Log("ERROR", $"'{methods[i].DeclaringType}.{methods[i].Name}' doesn't match any acceptable message handler method signatures, double-check its parameters!");
                }
            }
        }

        /// <inheritdoc/>
        public override void Tick() => server.Tick();

        /// <inheritdoc cref="IServer.Send(Message, ushort, byte, bool)"/>
        public void Send(Message message, ushort toClientId, byte maxSendAttempts = 15, bool shouldRelease = true) => server.Send(message, toClientId, maxSendAttempts, shouldRelease);

        /// <inheritdoc cref="IServer.SendToAll(Message, byte, bool)"/>
        public void SendToAll(Message message, byte maxSendAttempts = 15, bool shouldRelease = true) => server.SendToAll(message, maxSendAttempts, shouldRelease);

        /// <inheritdoc cref="IServer.SendToAll(Message, ushort, byte, bool)"/>
        public void SendToAll(Message message, ushort exceptToClientId, byte maxSendAttempts = 15, bool shouldRelease = true) => server.SendToAll(message, exceptToClientId, maxSendAttempts, shouldRelease);

        /// <inheritdoc cref="IServer.DisconnectClient(ushort)"/>
        public void DisconnectClient(ushort clientId) => server.DisconnectClient(clientId);

        /// <summary>Stops the server.</summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            server.Shutdown();
            server.ClientConnected -= ClientConnected;
            server.MessageReceived -= OnMessageReceived;
            server.ClientDisconnected -= ClientDisconnected;

            IsRunning = false;
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        private void OnMessageReceived(object s, ServerMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);

            if (messageHandlers.TryGetValue(e.MessageId, out MessageHandler messageHandler))
                messageHandler(e.FromClientId, e.Message);
            else
                RiptideLogger.Log("ERROR", $"No handler method (type: server) found for message ID {e.MessageId}!");
        }
    }
}
