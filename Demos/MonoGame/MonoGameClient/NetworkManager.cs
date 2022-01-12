
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2022 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking;

namespace RiptideDemos.RudpTransport.MonoGame.TestClient
{
    internal class NetworkManager
    {
        internal static Client Client { get; set; }
        
        public static void Connect()
        {
            Client = new Client();
            Client.ClientDisconnected += (s, e) => Player.List.Remove(e.Id);

            Client.Connect("127.0.0.1:7777");
        }
    }
}
