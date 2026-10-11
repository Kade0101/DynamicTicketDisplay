using Newtonsoft.Json;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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

        public static async Task SendClearMessageAsync(string ipAddress, int port)
        {
            var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);

            var stream = client.GetStream();

            var message = new
            {
                Item1 = string.Empty,
                Item2 = "Clear"
            };

            string json = JsonConvert.SerializeObject(message);
            var data = Encoding.UTF8.GetBytes(json);

            await stream.WriteAsync(data, 0, data.Length);
            client.Close();
        }


        public static async Task SendMessagePrizeAsync(string ipAddress, int port, string message)
        {
            var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);

            var stream = client.GetStream();

            string json = JsonConvert.SerializeObject(message); // Convert to JSON
            var data = Encoding.UTF8.GetBytes(json);

            await stream.WriteAsync(data, 0, data.Length);
            client.Close(); // Cleanly close the connection
        }

        public static async Task SendInstructionsAsync(string ipAddress, int port, string instructions)
        {
            var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);

            var stream = client.GetStream();

            var message = new
            {
                Item1 = instructions,
                Item2 = "Instructions"
            };

            string json = JsonConvert.SerializeObject(message); // Convert to JSON
            var data = Encoding.UTF8.GetBytes(json);

            await stream.WriteAsync(data, 0, data.Length);
            client.Close(); // Cleanly close the connection
        }

        public static async Task SendConfirmMessageAsync(string ipAddress, int port)
        {
            var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port);

            var stream = client.GetStream();

            var message = new
            {
                Item1 = string.Empty,
                Item2 = "Confirm"
            };

            string json = JsonConvert.SerializeObject(message);
            var data = Encoding.UTF8.GetBytes(json);

            await stream.WriteAsync(data, 0, data.Length);
            client.Close();
        }
    }
}
