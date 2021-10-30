using RiptideNetworking.Transports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RiptideNetworking
{
    /// <summary>A client that can connect to a <see cref="Server"/>.</summary>
    public class Client : Common
    {
        /// <summary>Invoked when a connection to the server is established.</summary>
        public event EventHandler Connected;
        /// <summary>Invoked when a connection to the server fails to be established.</summary>
        /// <remarks>This occurs when a connection request times out, either because no server is listening on the expected IP and port, or because something (firewall, antivirus, no/poor internet access, etc.) is preventing the connection.</remarks>
        public event EventHandler ConnectionFailed;
        /// <summary>Invoked when a message is received from the server.</summary>
        public event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when disconnected by the server.</summary>
        public event EventHandler Disconnected;
        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>The numeric ID of the client.</summary>
        public ushort Id => client.Id;
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        public short RTT => client.RTT;
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        public short SmoothRTT => client.SmoothRTT;
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        public bool IsConnecting => client.IsConnecting;
        /// <summary>Whether or not the client is currently connected.</summary>
        public bool IsConnected => client.IsConnected;
        /// <inheritdoc/>
        public override bool ShouldOutputInfoLogs
        {
            get => client.ShouldOutputInfoLogs;
            set => client.ShouldOutputInfoLogs = value;
        }
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

        /// <summary>Attempts connect to the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        /// <remarks>
        ///   Riptide's default transport expects the host address to consist of an IP and port, separated by a colon. For example: <c>127.0.0.1:7777</c>.<br/>
        ///   If you are using a different transport, check the relevant documentation for what information it requires in the host address.
        /// </remarks>
        public void Connect(string hostAddress, byte messageHandlerGroupId = 0)
        {
            CreateMessageHandlersDictionary(Assembly.GetCallingAssembly(), messageHandlerGroupId);

            client.Connected += Connected;
            client.ConnectionFailed += ConnectionFailed;
            client.MessageReceived += OnMessageReceived;
            client.Disconnected += Disconnected;
            client.ClientConnected += ClientConnected;
            client.ClientDisconnected += ClientDisconnected;
            client.Connect(hostAddress);
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

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Client instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                        RiptideLogger.Log("ERROR", $"Message handler method already exists for message ID {attribute.MessageId}! Only one handler method is allowed per ID!");
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Client instances, but it might be one for Server instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Server.MessageHandler), methods[i], false);
                    if (serverMessageHandler == null)
                        RiptideLogger.Log("ERROR", $"Method '{methods[i].Name}' didn't match a message handler signature!");
                }
            }
        }

        /// <inheritdoc/>
        public override void Tick() => client.Tick();

        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending <paramref name="message"/> before giving up. Only applies to messages with their <see cref="Message.SendMode"/> set to <see cref="MessageSendMode.reliable"/>.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        public void Send(Message message, byte maxSendAttempts = 15, bool shouldRelease = true) => client.Send(message, maxSendAttempts, shouldRelease);

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            client.Disconnect();
            client.Connected -= Connected;
            client.ConnectionFailed -= ConnectionFailed;
            client.MessageReceived -= OnMessageReceived;
            client.Disconnected -= Disconnected;
            client.ClientConnected -= ClientConnected;
            client.ClientDisconnected -= ClientDisconnected;
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        private void OnMessageReceived(object s, ClientMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);

            if (messageHandlers.TryGetValue(e.MessageId, out MessageHandler messageHandler))
                messageHandler(e.Message);
            else
                RiptideLogger.Log("ERROR", $"No handler method found for message ID {e.MessageId}!");
        }
    }
}
