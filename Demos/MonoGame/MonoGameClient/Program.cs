using System;

namespace Riptide.Demos.MGClient
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
