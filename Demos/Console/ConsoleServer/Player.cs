using RiptideNetworking;
using RiptideNetworking.Transports;

namespace ConsoleServer
{
    class Player
    {
        internal readonly IServerClient client;

        internal Player(IServerClient client)
        {
            this.client = client;
        }
    }
}
