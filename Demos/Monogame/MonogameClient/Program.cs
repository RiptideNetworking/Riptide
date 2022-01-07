// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Monogame.TestClient
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            using ExampleGame game = new ExampleGame();
            game.Run();
        }
    }
}
