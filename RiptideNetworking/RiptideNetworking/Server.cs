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
        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a message is received from a client.</summary>
        public event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>Whether or not the server is currently running.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>The local port that the server is running on.</summary>
        public ushort Port => server.Port;
        /// <summary>An array of all the currently connected clients.</summary>
        /// <remarks>The position of each <see cref="IConnectionInfo"/> instance in the array does <i>not</i> correspond to that client's numeric ID (except by coincidence).</remarks>
        public IConnectionInfo[] Clients => server.Clients;
        /// <summary>The maximum number of clients that can be connected at any time.</summary>
        public ushort MaxClientCount => server.MaxClientCount;
        /// <summary>The number of currently connected clients.</summary>
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

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Server instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                        RiptideLogger.Log("ERROR", $"Message handler method already exists for message ID {attribute.MessageId}! Only one handler method is allowed per ID!");
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Server instances, but it might be one for Client instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Client.MessageHandler), methods[i], false);
                    if (serverMessageHandler == null)
                        RiptideLogger.Log("ERROR", $"Method '{methods[i].Name}' didn't match a message handler signature!");
                }
            }
        }

        /// <inheritdoc/>
        public override void Tick() => server.Tick();

        /// <summary>Sends a message to a specific client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClientId">The numeric ID of the client to send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending <paramref name="message"/> before giving up. Only applies to messages with their <see cref="Message.SendMode"/> set to <see cref="MessageSendMode.reliable"/>.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        public void Send(Message message, ushort toClientId, byte maxSendAttempts = 15, bool shouldRelease = true) => server.Send(message, toClientId, maxSendAttempts, shouldRelease);

        /// <summary>Sends a message to all conected clients.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending <paramref name="message"/> before giving up. Only applies to messages with their <see cref="Message.SendMode"/> set to <see cref="MessageSendMode.reliable"/>.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        public void SendToAll(Message message, byte maxSendAttempts = 15, bool shouldRelease = true) => server.SendToAll(message, maxSendAttempts, shouldRelease);

        /// <summary>Sends a message to all connected clients except one.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="exceptToClientId">The numeric ID of the client to <i>not</i> send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending <paramref name="message"/> before giving up. Only applies to messages with their <see cref="Message.SendMode"/> set to <see cref="MessageSendMode.reliable"/>.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        public void SendToAll(Message message, ushort exceptToClientId, byte maxSendAttempts = 15, bool shouldRelease = true) => server.SendToAll(message, exceptToClientId, maxSendAttempts, shouldRelease);

        /// <summary>Kicks a specific client.</summary>
        /// <param name="clientId">The numeric ID of the client to kick.</param>
        public void DisconnectClient(ushort clientId) => server.DisconnectClient(clientId);

        /// <summary>Stops the server.</summary>
        public void Stop()
        {
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
                RiptideLogger.Log("ERROR", $"No handler method found for message ID {e.MessageId}!");
        }
    }
}
