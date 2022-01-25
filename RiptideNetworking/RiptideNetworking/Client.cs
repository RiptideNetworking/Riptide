
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
    /// <summary>A client that can connect to a <see cref="Server"/>.</summary>
    public class Client : Common
    {
        /// <inheritdoc cref="IClient.Connected"/>
        public event EventHandler Connected;
        /// <inheritdoc cref="IClient.ConnectionFailed"/>
        public event EventHandler ConnectionFailed;
        /// <inheritdoc cref="IClient.MessageReceived"/>
        public event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        /// <inheritdoc cref="IClient.Disconnected"/>
        public event EventHandler Disconnected;
        /// <inheritdoc cref="IClient.ClientConnected"/>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <inheritdoc cref="IClient.ClientDisconnected"/>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <inheritdoc cref="IConnectionInfo.Id"/>
        public ushort Id => client.Id;
        /// <inheritdoc cref="IConnectionInfo.RTT"/>
        public short RTT => client.RTT;
        /// <inheritdoc cref="IConnectionInfo.SmoothRTT"/>
        public short SmoothRTT => client.SmoothRTT;
        /// <inheritdoc cref="IConnectionInfo.IsNotConnected"/>
        public bool IsNotConnected => client.IsNotConnected;
        /// <inheritdoc cref="IConnectionInfo.IsConnecting"/>
        public bool IsConnecting => client.IsConnecting;
        /// <inheritdoc cref="IConnectionInfo.IsConnected"/>
        public bool IsConnected => client.IsConnected;
        /// <summary>Encapsulates a method that handles a message from the server.</summary>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(Message message);

        /// <summary>Methods used to handle messages, accessible by their corresponding message IDs.</summary>
        private Dictionary<ushort, MessageHandler> messageHandlers;
        /// <summary>The underlying client that is used for sending and receiving data.</summary>
        private IClient client;

        /// <summary>Handles initial setup.</summary>
        /// <param name="client">The underlying client that is used for sending and receiving data.</param>
        public Client(IClient client) => this.client = client;

        /// <summary>Handles initial setup using the built-in RUDP transport.</summary>
        /// <param name="timeoutTime">The time (in milliseconds) after which to disconnect if there's no heartbeat from the server.</param>
        /// <param name="heartbeatInterval">The interval (in milliseconds) at which heartbeats should be sent to the server.</param>
        /// <param name="maxConnectionAttempts">How many connection attempts to make before giving up.</param>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public Client(ushort timeoutTime = 5000, ushort heartbeatInterval = 1000, byte maxConnectionAttempts = 5, string logName = "CLIENT") => client = new Transports.RudpTransport.RudpClient(timeoutTime, heartbeatInterval, maxConnectionAttempts, logName);

        /// <summary>Disconnects the client if it's connected and swaps out the transport it's using.</summary>
        /// <param name="client">The underlying client that is used for managing the connection to the server.</param>
        /// <remarks>This method does not automatically reconnect to the server. To continue communicating with the server, <see cref="Connect(string, byte, Message)"/> will need to be called again.</remarks>
        public void ChangeTransport(IClient client)
        {
            Disconnect();
            this.client = client;
        }

        /// <summary>Attempts to connect to the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        /// <param name="message">A message containing data that should be sent to the server with the connection attempt. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        /// <remarks>
        ///   Riptide's default transport expects the host address to consist of an IP and port, separated by a colon. For example: <c>127.0.0.1:7777</c>.<br/>
        ///   If you are using a different transport, check the relevant documentation for what information it requires in the host address.
        /// </remarks>
        public void Connect(string hostAddress, byte messageHandlerGroupId = 0, Message message = null)
        {
            Disconnect();

            CreateMessageHandlersDictionary(Assembly.GetCallingAssembly(), messageHandlerGroupId);

            client.Connected += OnConnected;
            client.ConnectionFailed += OnConnectionFailed;
            client.MessageReceived += OnMessageReceived;
            client.Disconnected += OnDisconnected;
            client.ClientConnected += OnClientConnected;
            client.ClientDisconnected += OnClientDisconnected;
            client.Connect(hostAddress, message);
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

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Client instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                    {
                        MethodInfo otherMethodWithId = messageHandlers[attribute.MessageId].GetMethodInfo();
                        throw new Exception($"Client-side message handler methods '{methods[i].DeclaringType}.{methods[i].Name}' and '{otherMethodWithId.DeclaringType}.{otherMethodWithId.Name}' are both set to handle messages with ID {attribute.MessageId}! Only one handler method is allowed per message ID!");
                    }
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Client instances, but it might be one for Server instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Server.MessageHandler), methods[i], false);
                    if (serverMessageHandler == null)
                        throw new Exception($"'{methods[i].DeclaringType}.{methods[i].Name}' doesn't match any acceptable message handler method signatures, double-check its parameters!");
                }
            }
        }

        /// <inheritdoc/>
        public override void Tick() => client.Tick();

        /// <inheritdoc cref="IClient.Send(Message, bool)"/>
        public void Send(Message message, bool shouldRelease = true) => client.Send(message, shouldRelease);

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            if (IsNotConnected)
                return;

            client.Disconnect();
            LocalDisconnect();
        }

        private void LocalDisconnect()
        {
            client.Connected -= OnConnected;
            client.ConnectionFailed -= OnConnectionFailed;
            client.MessageReceived -= OnMessageReceived;
            client.Disconnected -= OnDisconnected;
            client.ClientConnected -= OnClientConnected;
            client.ClientDisconnected -= OnClientDisconnected;
        }

        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        private void OnConnected(object s, EventArgs e) => Connected?.Invoke(this, e);

        /// <summary>Invokes the <see cref="ConnectionFailed"/> event.</summary>
        private void OnConnectionFailed(object s, EventArgs e)
        {
            LocalDisconnect();
            ConnectionFailed?.Invoke(this, e);
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        private void OnMessageReceived(object s, ClientMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);

            if (messageHandlers.TryGetValue(e.MessageId, out MessageHandler messageHandler))
                messageHandler(e.Message);
            else
                RiptideLogger.Log(LogType.warning, $"No client-side handler method found for message ID {e.MessageId}!");
        }

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        private void OnDisconnected(object s, EventArgs e)
        {
            LocalDisconnect();
            Disconnected?.Invoke(this, e);
        }

        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        private void OnClientConnected(object s, ClientConnectedEventArgs e) => ClientConnected?.Invoke(this, e);

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        private void OnClientDisconnected(object s, ClientDisconnectedEventArgs e) => ClientDisconnected?.Invoke(this, e);
    }
}
