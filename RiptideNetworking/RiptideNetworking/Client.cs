// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Riptide
{
    /// <summary>A client that can connect to a <see cref="Server"/>.</summary>
    public class Client : Peer
    {
        /// <summary>Invoked when a connection to the server is established.</summary>
        public event EventHandler Connected;
        /// <summary>Invoked when a connection to the server fails to be established.</summary>
        /// <remarks>This occurs when a connection request fails, either because no server is listening on the on the given host address, or because something (firewall, antivirus, no/poor internet access, etc.) is preventing the connection.</remarks>
        public event EventHandler ConnectionFailed;
        /// <summary>Invoked when a message is received.</summary>
        public event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when disconnected from the server.</summary>
        public event EventHandler<DisconnectedEventArgs> Disconnected;
        /// <summary>Invoked when a client connects.</summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>The client's numeric ID.</summary>
        public ushort Id => connection.Id;
        /// <inheritdoc cref="Connection.RTT"/>
        public short RTT => connection.RTT;
        /// <summary><inheritdoc cref="Connection.SmoothRTT"/></summary>
        /// <remarks>This value is slower to accurately represent lasting changes in latency than <see cref="RTT"/>, but it is less susceptible to changing drastically due to significant—but temporary—jumps in latency.</remarks>
        public short SmoothRTT => connection.SmoothRTT;
        /// <summary>Whether or not the client is currently <i>not</i> connected nor trying to connect.</summary>
        public bool IsNotConnected => connection.IsNotConnected;
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        public bool IsConnecting => connection.IsConnecting;
        /// <summary>Whether or not the client is currently connected.</summary>
        public bool IsConnected => connection.IsConnected;
        /// <summary>Encapsulates a method that handles a message from a server.</summary>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(Message message);

        /// <summary>The client's connection to a server.</summary>
        private Connection connection;
        /// <summary>How many connection attempts have been made so far.</summary>
        private int connectionAttempts;
        /// <summary>How many connection attempts to make before giving up.</summary>
        private int maxConnectionAttempts;
        /// <inheritdoc cref="Server.messageHandlers"/>
        private Dictionary<ushort, MessageHandler> messageHandlers;
        /// <summary>The underlying transport's client that is used for sending and receiving data.</summary>
        private IClient transport;
        /// <summary>Custom data to include when connecting.</summary>
        private byte[] connectBytes;

        /// <inheritdoc cref="Server(IServer, string)"/>
        public Client(IClient transport, string logName = "CLIENT") : base(logName)
        {
            this.transport = transport;
        }

        /// <inheritdoc cref="Server(string)"/>
        public Client(string logName = "CLIENT") : base(logName)
        {
            transport = new Transports.Udp.UdpClient();
        }

        /// <summary>Disconnects the client if it's connected and swaps out the transport it's using.</summary>
        /// <param name="newTransport">The new transport to use for sending and receiving data.</param>
        /// <remarks>This method does not automatically reconnect to the server. To continue communicating with the server, <see cref="Connect(string, int, byte, Message)"/> must be called again.</remarks>
        public void ChangeTransport(IClient newTransport)
        {
            Disconnect();
            transport = newTransport;
        }

        /// <summary>Attempts to connect to a server at the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="maxConnectionAttempts">How many connection attempts to make before giving up.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        /// <param name="message">A message containing data that should be sent to the server with the connection attempt. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        /// <remarks>Riptide's default transport expects the host address to consist of an IP and port, separated by a colon. For example: <c>127.0.0.1:7777</c>. If you are using a different transport, check the relevant documentation for what information it requires in the host address.</remarks>
        /// <returns><see langword="true"/> if a connection attempt will be made. <see langword="false"/> if an issue occurred (such as <paramref name="hostAddress"/> being in an invalid format) and a connection attempt will <i>not</i> be made.</returns>
        public bool Connect(string hostAddress, int maxConnectionAttempts = 5, byte messageHandlerGroupId = 0, Message message = null)
        {
            Disconnect();

            SubToTransportEvents();

            if (!transport.Connect(hostAddress, out connection, out string connectError))
            {
                RiptideLogger.Log(LogType.error, LogName, connectError);
                UnsubFromTransportEvents();
                return false;
            }

            this.maxConnectionAttempts = maxConnectionAttempts;
            connection.Peer = this;
            IncreaseActiveCount();
            CreateMessageHandlersDictionary(messageHandlerGroupId);

            if (message != null)
            {
                connectBytes = message.GetBytes(message.WrittenLength);
                message.Release();
            }
            else
                connectBytes = null;

            RiptideLogger.Log(LogType.info, LogName, $"Connecting to {connection}...");
            return true;
        }

        /// <summary>Subscribes appropriate methods to the transport's events.</summary>
        private void SubToTransportEvents()
        {
            transport.Connected += TransportConnected;
            transport.ConnectionFailed += TransportConnectionFailed;
            transport.DataReceived += HandleData;
            transport.Disconnected += TransportDisconnected;
        }
        
        /// <summary>Unsubscribes methods from all of the transport's events.</summary>
        private void UnsubFromTransportEvents()
        {
            transport.Connected -= TransportConnected;
            transport.ConnectionFailed -= TransportConnectionFailed;
            transport.DataReceived -= HandleData;
            transport.Disconnected -= TransportDisconnected;
        }

        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(byte messageHandlerGroupId)
        {
            MethodInfo[] methods = FindMessageHandlers();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            for (int i = 0; i < methods.Length; i++)
            {
                MessageHandlerAttribute attribute = methods[i].GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    continue;

                if (!methods[i].IsStatic)
                    throw new NonStaticHandlerException(methods[i].DeclaringType, methods[i].Name);

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Client instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                    {
                        MethodInfo otherMethodWithId = messageHandlers[attribute.MessageId].GetMethodInfo();
                        throw new DuplicateHandlerException(attribute.MessageId, methods[i], otherMethodWithId);
                    }
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Client instances, but it might be one for Server instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Server.MessageHandler), methods[i], false);
                    if (serverMessageHandler == null)
                        throw new InvalidHandlerSignatureException(methods[i].DeclaringType, methods[i].Name);
                }
            }
        }

        /// <inheritdoc/>
        protected override void Heartbeat()
        {
            if (IsConnecting)
            {
                // If still trying to connect, send connect messages instead of heartbeats
                if (connectionAttempts < maxConnectionAttempts)
                {
                    Send(Message.Create(HeaderType.connect));
                    connectionAttempts++;
                }
                else
                    LocalDisconnect(DisconnectReason.neverConnected);
            }
            else if (IsConnected)
            {
                // If connected and not timed out, send heartbeats
                if (connection.HasTimedOut)
                {
                    LocalDisconnect(DisconnectReason.timedOut);
                    return;
                }

                connection.SendHeartbeat();
            }
        }

        /// <summary>Polls the transport for received messages and then handles them.</summary>
        public override void Tick()
        {
            base.Tick();
            transport.Tick();
            HandleMessages();
        }

        /// <inheritdoc/>
        protected override void Handle(Message message, HeaderType messageHeader, Connection connection)
        {
            switch (messageHeader)
            {
                // User messages
                case HeaderType.unreliable:
                case HeaderType.unreliableAutoRelay:
                case HeaderType.reliable:
                case HeaderType.reliableAutoRelay:
                    OnMessageReceived(message);
                    break;

                // Internal messages
                case HeaderType.ack:
                    connection.HandleAck(message);
                    break;
                case HeaderType.ackExtra:
                    connection.HandleAckExtra(message);
                    break;
                case HeaderType.connect:
                    // Handled by transport, if at all
                    break;
                case HeaderType.heartbeat:
                    connection.HandleHeartbeatResponse(message);
                    break;
                case HeaderType.welcome:
                    if (IsConnecting)
                    {
                        connection.HandleWelcome(message, connectBytes);
                        connectBytes = null;
                        OnConnected();
                    }
                    break;
                case HeaderType.clientConnected:
                    OnClientConnected(message.GetUShort());
                    break;
                case HeaderType.clientDisconnected:
                    OnClientDisconnected(message.GetUShort());
                    break;
                case HeaderType.disconnect:
                    LocalDisconnect((DisconnectReason)message.GetByte(), message.UnreadLength > 0 ? message.GetString() : "");
                    break;
                default:
                    RiptideLogger.Log(LogType.warning, LogName, $"Unknown message header type '{messageHeader}'! Discarding {message.WrittenLength} bytes.");
                    break;
            }

            message.Release();
        }

        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks><inheritdoc cref="Connection.Send(Message, bool)"/></remarks>
        public void Send(Message message, bool shouldRelease = true) => connection.Send(message, shouldRelease);

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            if (connection == null || IsNotConnected)
                return;

            Send(Message.Create(HeaderType.disconnect));
            LocalDisconnect(DisconnectReason.disconnected);
        }

        /// <summary>Cleans up the local side of the connection.</summary>
        /// <param name="reason">The reason why the client has disconnected.</param>
        /// <param name="customMessage">An optional custom message to display for the disconnection reason. Only used when <paramref name="reason"/> is set to <see cref="DisconnectReason.kicked"/>.</param>
        private void LocalDisconnect(DisconnectReason reason, string customMessage = "")
        {
            UnsubFromTransportEvents();
            DecreaseActiveCount();

            StopHeartbeat();
            transport.Disconnect();

            connection.LocalDisconnect();

            if (reason == DisconnectReason.neverConnected)
                OnConnectionFailed();
            else
            {
                string reasonString;
                switch (reason)
                {
                    case DisconnectReason.neverConnected:
                        reasonString = ReasonNeverConnected;
                        break;
                    case DisconnectReason.transportError:
                        reasonString = ReasonTransportError;
                        break;
                    case DisconnectReason.timedOut:
                        reasonString = ReasonTimedOut;
                        break;
                    case DisconnectReason.kicked:
                        reasonString = string.IsNullOrEmpty(customMessage) ? ReasonKicked : customMessage;
                        break;
                    case DisconnectReason.serverStopped:
                        reasonString = ReasonServerStopped;
                        break;
                    case DisconnectReason.disconnected:
                        reasonString = ReasonDisconnected;
                        break;
                    default:
                        reasonString = ReasonUnknown;
                        break;
                }

                OnDisconnected(reason, reasonString);
            }   
        }

        /// <summary>What to do when the transport establishes a connection.</summary>
        private void TransportConnected(object sender, EventArgs e)
        {
            StartHeartbeat();
        }

        /// <summary>What to do when the transport fails to connect.</summary>
        private void TransportConnectionFailed(object sender, EventArgs e)
        {
            LocalDisconnect(DisconnectReason.neverConnected);
        }

        /// <summary>What to do when the transport disconnects.</summary>
        private void TransportDisconnected(object sender, Transports.DisconnectedEventArgs e)
        {
            if (connection == e.Connection)
                LocalDisconnect(e.Reason);
        }

        #region Events
        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        protected virtual void OnConnected()
        {
            RiptideLogger.Log(LogType.info, LogName, "Connected successfully!");
            Connected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Invokes the <see cref="ConnectionFailed"/> event.</summary>
        protected virtual void OnConnectionFailed()
        {
            RiptideLogger.Log(LogType.info, LogName, "Connection to server failed!");
            ConnectionFailed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        /// <param name="message">The received message.</param>
        protected virtual void OnMessageReceived(Message message)
        {
            ushort messageId = message.GetUShort();
            MessageReceived?.Invoke(this, new ClientMessageReceivedEventArgs(messageId, message));

            if (messageHandlers.TryGetValue(messageId, out MessageHandler messageHandler))
                messageHandler(message);
            else
                RiptideLogger.Log(LogType.warning, $"No client message handler method found for message ID {messageId}!");
        }

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <param name="customMessage">The custom message to display for the disconnection reason.</param>
        protected virtual void OnDisconnected(DisconnectReason reason, string customMessage)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Disconnected from server: {customMessage}.");
            Disconnected?.Invoke(this, new DisconnectedEventArgs(reason, customMessage));
        }

        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        /// <param name="clientId">The numeric ID of the client that connected.</param>
        protected virtual void OnClientConnected(ushort clientId)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Client {clientId} connected.");
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientId));
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        /// <param name="clientId">The numeric ID of the client that disconnected.</param>
        protected virtual void OnClientDisconnected(ushort clientId)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Client {clientId} disconnected.");
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId));
        }
        #endregion
    }
}
