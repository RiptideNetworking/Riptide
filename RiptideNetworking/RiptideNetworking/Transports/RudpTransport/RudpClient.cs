
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;

namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>A client that can connect to an <see cref="RudpServer"/>.</summary>
    public class RudpClient : RudpListener, IClient
    {
        /// <inheritdoc/>
        public event EventHandler Connected;
        /// <inheritdoc/>
        public event EventHandler ConnectionFailed;
        /// <inheritdoc/>
        public event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        /// <inheritdoc/>
        public event EventHandler Disconnected;
        /// <inheritdoc/>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <inheritdoc/>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <inheritdoc/>
        public ushort Id { get; private set; }
        /// <inheritdoc/>
        public short RTT => peer.RTT;
        /// <inheritdoc/>
        public short SmoothRTT => peer.SmoothRTT;
        /// <inheritdoc/>
        public bool IsNotConnected => connectionState == ConnectionState.notConnected;
        /// <inheritdoc/>
        public bool IsConnecting => connectionState == ConnectionState.connecting;
        /// <inheritdoc/>
        public bool IsConnected => connectionState == ConnectionState.connected;
        /// <summary>The time (in milliseconds) after which to disconnect if there's no heartbeat from the server.</summary>
        public ushort TimeoutTime { get; set; } = 5000;
        /// <summary>The interval (in milliseconds) at which to send and expect heartbeats from the server.</summary>
        public ushort HeartbeatInterval
        {
            get => _heartbeatInterval;
            set
            {
                _heartbeatInterval = value;
                if (heartbeatTimer != null)
                    heartbeatTimer.Change(0, value);
            }
        }
        private ushort _heartbeatInterval;

        /// <summary>The client's <see cref="RudpPeer"/> instance.</summary>
        private RudpPeer peer;
        /// <summary>The connection's remote endpoint.</summary>
        private IPEndPoint remoteEndPoint;
        /// <summary>The client's current connection state.</summary>
        private ConnectionState connectionState = ConnectionState.notConnected;
        /// <summary>How many connection attempts have been made so far.</summary>
        private byte connectionAttempts;
        /// <summary>How many connection attempts to make before giving up.</summary>
        private readonly byte maxConnectionAttempts;

        /// <summary>Whether or not the client has timed out.</summary>
        private bool HasTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > TimeoutTime;
        /// <summary>The timer responsible for sending regular heartbeats.</summary>
        private Timer heartbeatTimer;
        /// <summary>The time at which the last heartbeat was received from the client.</summary>
        private DateTime lastHeartbeat;
        /// <summary>ID of the last ping that was sent.</summary>
        private byte lastPingId = 0;
        /// <summary>The ID of the currently pending ping.</summary>
        private byte pendingPingId;
        /// <summary>The stopwatch that tracks the time since the currently pending ping was sent.</summary>
        private readonly Stopwatch pendingPingStopwatch;
        /// <summary>An array of custom bytes to include when connecting.</summary>
        private byte[] connectBytes;

        /// <summary>Handles initial setup.</summary>
        /// <param name="timeoutTime">The time (in milliseconds) after which to disconnect if there's no heartbeat from the server.</param>
        /// <param name="heartbeatInterval">The interval (in milliseconds) at which heartbeats should be sent to the server.</param>
        /// <param name="maxConnectionAttempts">How many connection attempts to make before giving up.</param>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public RudpClient(ushort timeoutTime = 5000, ushort heartbeatInterval = 1000, byte maxConnectionAttempts = 5, string logName = "CLIENT") : base(logName)
        {
            TimeoutTime = timeoutTime;
            _heartbeatInterval = heartbeatInterval;
            this.maxConnectionAttempts = maxConnectionAttempts;
            pendingPingStopwatch = new Stopwatch();
        }

        /// <inheritdoc/>
        /// <remarks>Expects the host address to consist of an IP and port, separated by a colon. For example: <c>127.0.0.1:7777</c>.</remarks>
        public void Connect(string hostAddress, Message message)
        {
            if (!ParseHostAddress(hostAddress, out IPAddress ip, out ushort port))
                return;

            connectionAttempts = 0;
            remoteEndPoint = new IPEndPoint(ip.MapToIPv6(), port);
            peer = new RudpPeer(this);

            StartListening();
            connectionState = ConnectionState.connecting;

            if (message != null)
            {
                connectBytes = message.GetBytes(message.WrittenLength);
                message.Release();
            }
            else
                connectBytes = null;

            heartbeatTimer = new Timer((o) => Heartbeat(), null, 0, HeartbeatInterval);
            RiptideLogger.Log(LogType.info, LogName, $"Connecting to {remoteEndPoint.ToStringBasedOnIPFormat()}...");
        }

        /// <summary>Parses the <paramref name="hostAddress"/> and retrieves its <paramref name="ip"/> and <paramref name="port"/>, if possible.</summary>
        /// <param name="hostAddress">The host address to parse.</param>
        /// <param name="ip">The retrieved IP.</param>
        /// <param name="port">The retrieved port.</param>
        /// <returns>Whether or not the host address is valid.</returns>
        private bool ParseHostAddress(string hostAddress, out IPAddress ip, out ushort port)
        {
            string[] ipAndPort = hostAddress.Split(':');
            string ipString = "";
            string portString = "";
            if (ipAndPort.Length > 2)
            {
                // There was more than one ':' in the host address, might be IPv6
                ipString = string.Join(":", ipAndPort.Take(ipAndPort.Length - 1));
                portString = ipAndPort[ipAndPort.Length - 1];
            }
            else if (ipAndPort.Length == 2)
            {
                // IPv4
                ipString = ipAndPort[0];
                portString = ipAndPort[1];
            }

            if (!IPAddress.TryParse(ipString, out ip) || !ushort.TryParse(portString, out port))
            {
                RiptideLogger.Log(LogType.error, LogName, $"Invalid host address '{hostAddress}'! IP and port should be separated by a colon, for example: '127.0.0.1:7777'.");
                port = 0;
                return false;
            }
            return true;
        }

        /// <summary>Sends a connnect or heartbeat message. Called by <see cref="heartbeatTimer"/>.</summary>
        private void Heartbeat()
        {
            if (IsConnecting)
            {
                // If still trying to connect, send connect messages instead of heartbeats
                if (connectionAttempts < maxConnectionAttempts)
                {
                    SendConnect();
                    connectionAttempts++;
                }
                else
                    OnConnectionFailed();
            }
            else if (IsConnected)
            {
                // If connected and not timed out, send heartbeats
                if (HasTimedOut)
                {
                    OnDisconnected();
                    return;
                }

                SendHeartbeat();
            }
        }

        /// <inheritdoc/>
        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, HeaderType messageHeader) => endPoint.Equals(remoteEndPoint);

        /// <inheritdoc/>
        protected override void Handle(Message message, IPEndPoint fromEndPoint, HeaderType messageHeader)
        {
#if DETAILED_LOGGING
            if (messageHeader != HeaderType.reliable && messageHeader != HeaderType.unreliable)
                RiptideLogger.Log(LogName, $"Received {messageHeader} message from {fromEndPoint}.");
            
            ushort messageId = message.PeekUShort();
            if (messageHeader == HeaderType.reliable)
                RiptideLogger.Log(LogName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
            else if (messageHeader == HeaderType.unreliable)
                RiptideLogger.Log(LogName, $"Received unreliable message (ID: {messageId}) from {fromEndPoint}.");
#endif

            switch (messageHeader)
            {
                // User messages
                case HeaderType.unreliable:
                case HeaderType.unreliableAutoRelay:
                case HeaderType.reliable:
                case HeaderType.reliableAutoRelay:
                    receiveActionQueue.Add(() =>
                    {
                        OnMessageReceived(new ClientMessageReceivedEventArgs(message.GetUShort(), message));

                        message.Release();
                    });
                    return;

                // Internal messages
                case HeaderType.ack:
                    HandleAck(message);
                    break;
                case HeaderType.ackExtra:
                    HandleAckExtra(message);
                    break;
                case HeaderType.heartbeat:
                    HandleHeartbeat(message);
                    break;
                case HeaderType.welcome:
                    HandleWelcome(message);
                    break;
                case HeaderType.clientConnected:
                    HandleClientConnected(message);
                    break;
                case HeaderType.clientDisconnected:
                    HandleClientDisconnected(message);
                    break;
                case HeaderType.disconnect:
                    HandleDisconnect();
                    break;
                default:
                    RiptideLogger.Log(LogType.warning, LogName, $"Unknown message header type '{messageHeader}'! Discarding {message.WrittenLength} bytes.");
                    break;
            }

            message.Release();
        }

        /// <inheritdoc/>
        protected override void ReliableHandle(HeaderType messageHeader, ushort sequenceId, Message message, IPEndPoint fromEndPoint)
        {
            ReliableHandle(messageHeader, sequenceId, message, fromEndPoint, peer.SendLockables);
        }

        /// <inheritdoc/>
        public void Send(Message message, bool shouldRelease = true)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.Bytes, message.WrittenLength, remoteEndPoint);
            else
                SendReliable(message, remoteEndPoint, peer);

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void Disconnect()
        {
            if (IsNotConnected)
                return;

            SendDisconnect(); // This will just not send if already disconnected
            if (LocalDisconnect())
                RiptideLogger.Log(LogType.info, LogName, "Disconnected from server (initiated locally).");
        }

        /// <summary>Cleans up local objects on disconnection.</summary>
        /// <returns><see langword="true"/> if successfully disconnected.<br/><see langword="false"/> if it was already disconnected.</returns>
        private bool LocalDisconnect()
        {
            lock (peer.PendingMessages)
            {
                if (IsNotConnected)
                    return false;

                heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
                heartbeatTimer.Dispose();
                StopListening();
                connectionState = ConnectionState.notConnected;

                foreach (PendingMessage pendingMessage in peer.PendingMessages.Values)
                    pendingMessage.Clear(false);

                peer.PendingMessages.Clear();
            }
            return true;
        }

        #region Messages
        /// <summary>Sends a connect message.</summary>
        private void SendConnect()
        {
            Send(Message.Create(HeaderType.connect));
        }

        /// <inheritdoc/>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            Message message = Message.Create(forSeqId == peer.SendLockables.LastReceivedSeqId ? HeaderType.ack : HeaderType.ackExtra);
            message.Add(peer.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(peer.SendLockables.AcksBitfield); // Acks

            if (forSeqId == peer.SendLockables.LastReceivedSeqId)
                Send(message);
            else
            {
                message.Add(forSeqId);
                Send(message);
            }
        }

        /// <summary>Handles an ack message.</summary>
        /// <param name="message">The ack message to handle.</param>
        private void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            peer.AckMessage(remoteLastReceivedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            peer.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Handles an ack message for a sequence ID other than the last received one.</summary>
        /// <param name="message">The ack message to handle.</param>
        private void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            peer.AckMessage(ackedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            peer.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Sends a heartbeat message.</summary>
        private void SendHeartbeat()
        {
            pendingPingId = lastPingId++;
            pendingPingStopwatch.Restart();

            Message message = Message.Create(HeaderType.heartbeat);
            message.Add(pendingPingId);
            message.Add(peer.RTT);

            Send(message);
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        private void HandleHeartbeat(Message message)
        {
            byte pingId = message.GetByte();

            if (pendingPingId == pingId)
                peer.RTT = (short)Math.Max(1f, pendingPingStopwatch.ElapsedMilliseconds);

            lastHeartbeat = DateTime.UtcNow;
        }

        /// <summary>Handles a welcome message.</summary>
        /// <param name="message">The welcome message to handle.</param>
        private void HandleWelcome(Message message)
        {
            if (IsConnected)
                return;

            Id = message.GetUShort();
            connectionState = ConnectionState.connected;
            lastHeartbeat = DateTime.UtcNow;

            SendWelcomeReceived();
            OnConnected();
        }

        /// <summary>Sends a welcome (received) message.</summary>
        private void SendWelcomeReceived()
        {
            Message message = Message.Create(HeaderType.welcome, 25);
            message.Add(Id);
            if (connectBytes != null)
            {
                message.Add(connectBytes, false);
                connectBytes = null;
            }

            Send(message);
        }

        /// <summary>Handles a client connected message.</summary>
        /// <param name="message">The client connected message to handle.</param>
        private void HandleClientConnected(Message message)
        {
            OnClientConnected(new ClientConnectedEventArgs(message.GetUShort()));
        }

        /// <summary>Handles a client disconnected message.</summary>
        /// <param name="message">The client disconnected message to handle.</param>
        private void HandleClientDisconnected(Message message)
        {
            OnClientDisconnected(new ClientDisconnectedEventArgs(message.GetUShort()));
        }

        /// <summary>Sends a disconnect message.</summary>
        private void SendDisconnect()
        {
            Send(Message.Create(HeaderType.disconnect));
        }

        /// <summary>Handles a disconnect message.</summary>
        private void HandleDisconnect()
        {
            OnDisconnected();
        }
        #endregion

        #region Events
        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        private void OnConnected()
        {
            RiptideLogger.Log(LogType.info, LogName, "Connected successfully!");

            receiveActionQueue.Add(() => Connected?.Invoke(this, EventArgs.Empty));
        }

        /// <summary>Invokes the <see cref="ConnectionFailed"/> event.</summary>
        private void OnConnectionFailed()
        {
            if (LocalDisconnect())
            {
                RiptideLogger.Log(LogType.info, LogName, "Connection to server failed!");
                receiveActionQueue.Add(() => ConnectionFailed?.Invoke(this, EventArgs.Empty));
            }
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event.</summary>
        /// <param name="e">The event args to invoke the event with.</param>
        private void OnMessageReceived(ClientMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        private void OnDisconnected()
        {
            if (LocalDisconnect())
            {
                RiptideLogger.Log(LogType.info, LogName, "Disconnected from server (initiated remotely).");
                receiveActionQueue.Add(() => Disconnected?.Invoke(this, EventArgs.Empty));
            }
        }

        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        /// <param name="e">The event args to invoke the event with.</param>
        private void OnClientConnected(ClientConnectedEventArgs e)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Client {e.Id} connected.");

            receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        /// <param name="e">The event args to invoke the event with.</param>
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Client {e.Id} disconnected.");

            receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
        }
        #endregion
    }
}
