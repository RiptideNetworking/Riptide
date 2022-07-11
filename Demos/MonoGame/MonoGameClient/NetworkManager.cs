namespace Riptide.Demos.MGClient
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
