
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

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
