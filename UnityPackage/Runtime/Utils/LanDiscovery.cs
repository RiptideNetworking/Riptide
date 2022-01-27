
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RiptideNetworking.Utils
{
    /// <summary>Defines broadcast modes.</summary>
    public enum BroadcastMode
    {
        /// <summary>Not currently broadcasting or listening for broadcasts.</summary>
        none,
        /// <summary>Currently broadcasting.</summary>
        broadcasting,
        /// <summary>Currently listening for broadcasts.</summary>
        listening
    }

    /// <summary>Provides functionality for discovering game hosts over LAN.</summary>
    public class LanDiscovery
    {
        /// <summary>Invoked when a host is found on the LAN.</summary>
        public event EventHandler<HostDiscoveredEventArgs> HostDiscovered;

        /// <summary>This app's unique key, used to determine whether to handle or ignore received data.</summary>
        public long UniqueKey { get; set; }
        /// <summary>The current broadcast mode.</summary>
        public BroadcastMode Mode { get; set; }
        /// <summary>The port to send broadcasts to/listen for broadcasts on.</summary>
        public ushort BroadcastPort
        {
            get => _broadcastPort;
            set
            {
                if (value == 0)
                    throw new ArgumentOutOfRangeException("Broadcast port cannot be set to 0!");

                if (value != _broadcastPort)
                {
                    _broadcastPort = value;
                    
                    endPoint = new IPEndPoint(IPAddress.Any, value);
                    try
                    {
                        socket.Bind(endPoint);
                    }
                    catch (SocketException ex)
                    {
                        RiptideLogger.Log(LogType.error, $"Failed to bind broadcast socket! Make sure port {_broadcastPort} isn't being used by another {nameof(LanDiscovery)} insance or by another application on this machine, or use a different port for broadcasting. Error: {ex}");
                    }
                    broadcastEndPoint.Port = value;
                }
            }
        }
        private ushort _broadcastPort;
        /// <summary>The IP to broadcast.</summary>
        public IPAddress HostIP { set; protected get; }
        /// <summary>The port to broadcast.</summary>
        public ushort HostPort { set; protected get; }

        /// <summary>The current machine's local IP.</summary>
        protected IPAddress localIPAdress;
        /// <summary>The subnet mask for <see cref="localIPAdress"/>.</summary>
        protected IPAddress subnetMask;
        /// <summary>The endpoint to which to send data in order to broadcast it to all machines on the LAN.</summary>
        protected IPEndPoint broadcastEndPoint;
        /// <summary>The socket used to send and listen for broadcasted data.</summary>
        protected Socket socket;
        /// <summary>A reusable <see cref="EndPoint"/> instance.</summary>
        protected EndPoint endPoint;
        /// <summary>The array used to broadcast data.</summary>
        protected byte[] broadcastSendBytes;
        /// <summary>The array into which broadcasted data is received.</summary>
        protected byte[] broadcastReceiveBytes;
        /// <summary>The <see cref="ActionQueue"/> to use when invoking events.</summary>
        protected ActionQueue actionQueue;

        /// <summary>Handles initial setup.</summary>
        /// <param name="uniqueKey">This app's unique key, used to determine whether to handle or ignore received data.</param>
        /// <param name="broadcastPort">The port to send broadcasts to/listen for broadcasts on.</param>
        public LanDiscovery(long uniqueKey, ushort broadcastPort)
        {
            actionQueue = new ActionQueue();
            UniqueKey = uniqueKey;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            
            localIPAdress = GetLocalIPAddress();
            subnetMask = GetBroadcastAddress(localIPAdress, GetSubnetMask(localIPAdress));
            broadcastEndPoint = new IPEndPoint(subnetMask, broadcastPort);

            BroadcastPort = broadcastPort;
            broadcastSendBytes = new byte[14];
            broadcastReceiveBytes = new byte[14];
        }

        /// <summary>Sends a broadcast to all machines on the LAN.</summary>
        public void SendBroadcast()
        {
            if (Mode == BroadcastMode.listening)
            {
                RiptideLogger.Log(LogType.error, "LAN Discovery is in listen mode. Change the mode before broadcasting!");
                return;
            }

            if (Mode == BroadcastMode.none)
                socket.BeginReceiveFrom(broadcastReceiveBytes, 0, broadcastReceiveBytes.Length, SocketFlags.None, ref endPoint, ReceiveCallback, null);

            Mode = BroadcastMode.broadcasting;

            SetBroadcastData();
            socket.SendTo(broadcastSendBytes, broadcastEndPoint);
        }

        /// <summary>Sends a response to a broadcast.</summary>
        /// <param name="toEndPoint">The endpoint to send the response to.</param>
        protected void SendBroadcastResponse(IPEndPoint toEndPoint)
        {
            SetBroadcastResponseData();
            socket.SendTo(broadcastSendBytes, toEndPoint);
        }

        /// <summary>Begins listening for broadcasted data.</summary>
        public void StartListening()
        {
            if (Mode == BroadcastMode.broadcasting)
            {
                RiptideLogger.Log(LogType.error, "LAN Discovery is in broadcast mode. Change the mode before listening!");
                return;
            }
            
            Mode = BroadcastMode.listening;
            socket.BeginReceiveFrom(broadcastReceiveBytes, 0, broadcastReceiveBytes.Length, SocketFlags.None, ref endPoint, ReceiveCallback, null);
        }

        /// <summary>Receives data.</summary>
        private void ReceiveCallback(IAsyncResult result)
        {
            int bytesReceived = socket.EndReceiveFrom(result, ref endPoint);
            if (bytesReceived < 1)
                return; // If there's no data
            else if (((IPEndPoint)endPoint).Address.Equals(localIPAdress))
            {
                // If the packet came from this machine we don't want to handle it
                socket.BeginReceiveFrom(broadcastReceiveBytes, 0, broadcastReceiveBytes.Length, SocketFlags.None, ref endPoint, ReceiveCallback, null);
                return;
            }

            if (Mode == BroadcastMode.broadcasting)
                HandleBroadcastResponseData(bytesReceived);
            else if (Mode == BroadcastMode.listening)
                HandleBroadcastData(bytesReceived);

            socket.BeginReceiveFrom(broadcastReceiveBytes, 0, broadcastReceiveBytes.Length, SocketFlags.None, ref endPoint, ReceiveCallback, null);
        }

        /// <summary>Initiates execution of any queued event invocations.</summary>
        /// <remarks>This should generally be called from within a regularly executed update loop (like FixedUpdate in Unity). Broadcasts will continue to discover hosts on the LAN in between calls, but the <see cref="HostDiscovered"/> event won't be invoked until this method is executed.</remarks>
        public virtual void Tick()
        {
            if (Mode == BroadcastMode.broadcasting)
                actionQueue.ExecuteAll();
        }

        /// <summary>Sets the data that will be sent as part of a broadcast.</summary>
        protected virtual void SetBroadcastData()
        {
            RiptideConverter.FromLong(UniqueKey, broadcastSendBytes, 0);
        }

        /// <summary>Sets the data that will be sent in response to a broadcast.</summary>
        protected virtual void SetBroadcastResponseData()
        {
            int ipBytes = RiptideConverter.ToInt(HostIP.GetAddressBytes(), 0);

            RiptideConverter.FromLong(UniqueKey, broadcastSendBytes, 0);
            RiptideConverter.FromInt(ipBytes, broadcastSendBytes, 8);
            RiptideConverter.FromUShort(HostPort, broadcastSendBytes, 12);
        }

        /// <summary>Handles the data received as part of a broadcast.</summary>
        /// <param name="bytesReceived">The number of bytes that were received.</param>
        protected virtual void HandleBroadcastData(int bytesReceived)
        {
            if (bytesReceived < 8)
                return; // Not enough bytes to read the expected data, presumably not a broadcast packet from our program

            long key = RiptideConverter.ToLong(broadcastReceiveBytes, 0);
            if (key != UniqueKey)
                return; // Key doesn't match, broadcast packet is not from our program

            SendBroadcastResponse((IPEndPoint)endPoint);
        }

        /// <summary>Handles the data received in response to a broadcast.</summary>
        /// <param name="bytesReceived">The number of bytes that were received.</param>
        protected virtual void HandleBroadcastResponseData(int bytesReceived)
        {
            if (bytesReceived < 14)
                return; // Not enough bytes to read the data, presumably not a response to a broadcast packet from our program

            long key = RiptideConverter.ToLong(broadcastReceiveBytes, 0);
            if (key != UniqueKey)
                return; // Key doesn't match, broadcast response packet is not from our program

            byte[] hostIPBytes = new byte[4];
            Array.Copy(broadcastReceiveBytes, 8, hostIPBytes, 0, 4);
            ushort hostPort = RiptideConverter.ToUShort(broadcastReceiveBytes, 12);

            OnHostDiscovered(new IPAddress(hostIPBytes), hostPort);
        }

        /// <summary>Stops all broadcast activities and prepares this <see cref="LanDiscovery"/> instance for reuse.</summary>
        public void Restart()
        {
            Stop();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            endPoint = new IPEndPoint(IPAddress.Any, BroadcastPort);
            socket.Bind(endPoint);
        }

        /// <summary>Stops all broadcast activities.</summary>
        public void Stop()
        {
            if (Mode == BroadcastMode.none)
                return;

            socket.Close();
            Mode = BroadcastMode.none;
        }

        /// <summary>Retrieves the current machine's local IP.</summary>
        /// <returns>The current machine's local IP.</returns>
        public IPAddress GetLocalIPAddress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;

            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>Calculates the broadcast address, given an IP and its subnet mask.</summary>
        /// <param name="address">The IP to use.</param>
        /// <param name="subnetMask">The subnet mask to use.</param>
        /// <returns>The calculated broadcast address.</returns>
        protected IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            // From https://www.medo64.com/2014/12/determining-ipv4-broadcast-address-in-c/
            int addressInt = RiptideConverter.ToInt(address.GetAddressBytes(), 0);
            int maskInt = RiptideConverter.ToInt(subnetMask.GetAddressBytes(), 0);
            int broadcastInt = addressInt | ~maskInt;

            byte[] broadcastIPBytes = new byte[4];
            RiptideConverter.FromInt(broadcastInt, broadcastIPBytes, 0);
            return new IPAddress(broadcastIPBytes);
        }

        /// <summary>Takes an IP and retrieves its subnet mask.</summary>
        /// <param name="address">The IP for which to retrieve the subnet mask.</param>
        /// <returns>The retrieved subnet mask.</returns>
        protected IPAddress GetSubnetMask(IPAddress address)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
                    if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork && address.Equals(unicastIPAddressInformation.Address))
                        return unicastIPAddressInformation.IPv4Mask;
            
            throw new ArgumentException($"Can't find subnet mask for IP address '{address}'!");
        }

        /// <summary>Invokes the <see cref="HostDiscovered"/>.</summary>
        /// <param name="ip">The IP of the discovered host.</param>
        /// <param name="port">The port of the discovered host.</param>
        protected void OnHostDiscovered(IPAddress ip, ushort port)
        {
            actionQueue.Add(() => HostDiscovered?.Invoke(this, new HostDiscoveredEventArgs(ip, port)));
        }
    }

    /// <summary>Contains event data for when a host is discovered on the LAN.</summary>
    public class HostDiscoveredEventArgs : EventArgs
    {
        /// <summary>The IP of the discovered host.</summary>
        public IPAddress HostIP { get; private set; }
        /// <summary>The port of the discovered host.</summary>
        public ushort HostPort { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="hostIP">The IP of the discovered host.</param>
        /// <param name="hostPort">The port of the discovered host.</param>
        public HostDiscoveredEventArgs(IPAddress hostIP, ushort hostPort)
        {
            HostIP = hostIP;
            HostPort = hostPort;
        }
    }
}
