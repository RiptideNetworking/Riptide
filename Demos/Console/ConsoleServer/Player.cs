using RiptideNetworking;
using System;
using System.Collections.Generic;
using System.Text;

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
