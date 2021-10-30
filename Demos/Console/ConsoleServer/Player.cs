using RiptideNetworking.Transports;

namespace ConsoleServer
{
    class Player
    {
        internal readonly IConnectionInfo client;

        internal Player(IConnectionInfo client)
        {
            this.client = client;
        }
    }
}
