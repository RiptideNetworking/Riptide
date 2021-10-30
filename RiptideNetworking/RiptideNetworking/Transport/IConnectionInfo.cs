namespace RiptideNetworking.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's connections must implement.</summary>
    public interface IConnectionInfo
    {
        /// <summary>The numeric ID of the client.</summary>
        ushort Id { get; }
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        short RTT { get; }
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        short SmoothRTT { get; }
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        bool IsConnecting { get; }
        /// <summary>Whether or not the client is currently connected.</summary>
        bool IsConnected { get; }
    }
}
