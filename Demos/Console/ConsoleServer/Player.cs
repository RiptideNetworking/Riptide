using RiptideNetworking;

namespace ConsoleServer
{
    class Player
    {
        internal readonly ServerClient client;

        internal Player(ServerClient client)
        {
            this.client = client;
        }
    }
}
