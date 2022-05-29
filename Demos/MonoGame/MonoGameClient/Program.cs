using System;

namespace RiptideDemos.RudpTransport.MonoGame.TestClient
{
    public static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var game = new ExampleGame();
            game.Run();
        }
    }
}
