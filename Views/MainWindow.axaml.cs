using Avalonia.Controls;
using Avalonia.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;


namespace TicketDisplayAppModified.Views;

public partial class MainWindow : Window
{
    private const int Port = 5000;
    private TicketTemplate? _mainView;

    public MainWindow()
    {
        InitializeComponent();
        StartTcpServer();
    }

    private async void StartTcpServer()
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            var buffer = new byte[1024];
            var stream = client.GetStream();

            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ShowMainView();

                    if (message.Equals("hide", StringComparison.OrdinalIgnoreCase))
                    {
                        HideMainView();
                    }
                    else
                    {
                        try
                        {
                            var ticket = JsonConvert.DeserializeObject<TicketInfo>(message);
                            if (ticket != null && _mainView != null)
                            {
                                // Update the UI with the ticket data
                                _mainView.UpdateTicket(ticket);
                            }
                        }
                        catch (JsonException)
                        {
                            // Invalid JSON - optionally handle this (log or ignore)
                            Console.WriteLine("Received invalid JSON.");
                        }
                    }
                });
            }


        }
    }

    private void ShowMainView()
    {
        if (_mainView == null)
        {
            _mainView = new TicketTemplate();
            MainViewContainer.Content = _mainView;
        }
    }

    private void HideMainView()
    {
        if (_mainView != null)
        {
            MainViewContainer.Content = null;
            _mainView = null;
        }
    }
    public class TicketInfo
    {
        public string? Letter { get; set; }
        public string? Number { get; set; }
        public string? Color { get; set; }

    }

}
