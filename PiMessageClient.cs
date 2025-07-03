using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static RaffleDisplayApplication.InputWindow;

namespace RaffleDisplayApplication
{
    public static class PiMessageClient
    {
        public static async Task SendMessageAsync(string ipAddress, int port, TicketInfo message)
        {
            var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);

            var stream = client.GetStream();

            string json = JsonConvert.SerializeObject(message); // Convert to JSON
            var data = Encoding.UTF8.GetBytes(json);

            await stream.WriteAsync(data, 0, data.Length);
            client.Close(); // Cleanly close the connection
        }
    }
}
