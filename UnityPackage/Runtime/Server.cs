
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Transports;
using RiptideNetworking.Utils;
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
        /// <inheritdoc cref="IServer.AllowAutoMessageRelay"/>
        public bool AllowAutoMessageRelay
        {
            get => server.AllowAutoMessageRelay;
            set => server.AllowAutoMessageRelay = value;
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

        /// <summary>Handles initial setup using the built-in RUDP transport.</summary>
        /// <param name="clientTimeoutTime">The time (in milliseconds) after which to disconnect a client without a heartbeat.</param>
        /// <param name="clientHeartbeatInterval">The interval (in milliseconds) at which heartbeats are to be expected from clients.</param>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public Server(ushort clientTimeoutTime = 5000, ushort clientHeartbeatInterval = 1000, string logName = "SERVER") => server = new Transports.RudpTransport.RudpServer(clientTimeoutTime, clientHeartbeatInterval, logName);

        /// <summary>Stops the server if it's running and swaps out the transport it's using.</summary>
        /// <param name="server">The underlying server that is used for managing connections and sending and receiving data.</param>
        /// <remarks>This method does not automatically restart the server. To continue accepting connections, <see cref="Start(ushort, ushort, byte)"/> will need to be called again.</remarks>
        public void ChangeTransport(IServer server)
        {
            Stop();
            this.server = server;
        }

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        public void Start(ushort port, ushort maxClientCount, byte messageHandlerGroupId = 0)
        {
            Stop();

            CreateMessageHandlersDictionary(Assembly.GetCallingAssembly(), messageHandlerGroupId);

            server.ClientConnected += OnClientConnected;
            server.MessageReceived += OnMessageReceived;
            server.ClientDisconnected += OnClientDisconnected;
            server.Start(port, maxClientCount);

            IsRunning = true;
        }
        
        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(Assembly assembly, byte messageHandlerGroupId)
        {
            MethodInfo[] methods = assembly.GetTypes()
                                           .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) // Include instance methods in the search so we can show the developer an error instead of silently not adding instance methods to the dictionary
                                           .Where(m => m.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0)
                                           .ToArray();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            for (int i = 0; i < methods.Length; i++)
            {
                MessageHandlerAttribute attribute = methods[i].GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    continue;

                if (!methods[i].IsStatic)
                    throw new Exception($"Message handler methods should be static, but '{methods[i].DeclaringType}.{methods[i].Name}' is an instance method!");

                Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (serverMessageHandler != null)
                {
                    // It's a message handler for Server instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                    {
                        MethodInfo otherMethodWithId = messageHandlers[attribute.MessageId].GetMethodInfo();
                        throw new Exception($"Server-side message handler methods '{methods[i].DeclaringType}.{methods[i].Name}' and '{otherMethodWithId.DeclaringType}.{otherMethodWithId.Name}' are both set to handle messages with ID {attribute.MessageId}! Only one handler method is allowed per message ID!");
                    }
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)serverMessageHandler);
                }
                else
                {
                    // It's not a message handler for Server instances, but it might be one for Client instances
                    Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(Client.MessageHandler), methods[i], false);
                    if (clientMessageHandler == null)
                        throw new Exception($"'{methods[i].DeclaringType}.{methods[i].Name}' doesn't match any acceptable message handler method signatures, double-check its parameters!");
                }
            }
        }

        /// <inheritdoc/>
        public override void Tick() => server.Tick();

        /// <inheritdoc cref="IServer.Send(Message, ushort, bool)"/>
        public void Send(Message message, ushort toClientId, bool shouldRelease = true) => server.Send(message, toClientId, shouldRelease);

        /// <inheritdoc cref="IServer.SendToAll(Message, bool)"/>
        public void SendToAll(Message message, bool shouldRelease = true) => server.SendToAll(message, shouldRelease);

        /// <inheritdoc cref="IServer.SendToAll(Message, ushort, bool)"/>
        public void SendToAll(Message message, ushort exceptToClientId, bool shouldRelease = true) => server.SendToAll(message, exceptToClientId, shouldRelease);

        /// <inheritdoc cref="IServer.DisconnectClient(ushort)"/>
        public void DisconnectClient(ushort clientId) => server.DisconnectClient(clientId);

        /// <summary>Stops the server.</summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            server.Shutdown();
            server.ClientConnected -= OnClientConnected;
            server.MessageReceived -= OnMessageReceived;
            server.ClientDisconnected -= OnClientDisconnected;

            IsRunning = false;
        }

        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        private void OnClientConnected(object s, ServerClientConnectedEventArgs e) => ClientConnected?.Invoke(this, e);

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        private void OnMessageReceived(object s, ServerMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);

            if (messageHandlers.TryGetValue(e.MessageId, out MessageHandler messageHandler))
                messageHandler(e.FromClientId, e.Message);
            else
                RiptideLogger.Log(LogType.warning, $"No server-side handler method found for message ID {e.MessageId}!");
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        private void OnClientDisconnected(object s, ClientDisconnectedEventArgs e) => ClientDisconnected?.Invoke(this, e);
    }
}
