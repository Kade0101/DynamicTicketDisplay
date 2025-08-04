using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System;
using Newtonsoft.Json;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using System.Linq;

namespace TicketDisplayAppModified.Views;

public partial class MainWindow : Window
{
    private const int Port = 5000;
    private TicketTemplate? _mainView1;
    private TicketTemplate? _mainView2;
    private TextBlock? _logTextBlock;

    // Rename one of the fields to resolve the ambiguity
    private ContentControl? _ticketSlot1;
    private ContentControl? _ticketSlot2;
    private Canvas? _overlayCanvas;

    public MainWindow()
    {
        InitializeComponent();
        this.WindowState = WindowState.FullScreen;
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                this.Close();
            }
        };
        StartTcpServer();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _logTextBlock = this.FindControl<TextBlock>("LogTextBlock");
        _ticketSlot1 = this.FindControl<ContentControl>("TicketSlot1");
        _ticketSlot2 = this.FindControl<ContentControl>("TicketSlot2");
        _overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
        DarkOverlay = this.FindControl<Border>("DarkOverlay");
    }

    public async Task AppendLogAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_logTextBlock != null)
                _logTextBlock.Text = message;
        });
    }

    private async void StartTcpServer()
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            await SetSshStatus(true);
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

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // Try to parse as JSON first
                    try
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(message);
                        var ticket = json.ToObject<TicketInfo>();
                        if (string.IsNullOrEmpty(ticket.Letter) || string.IsNullOrEmpty(ticket.Number) || string.IsNullOrEmpty(ticket.Color))
                        {
                            OnClearTicketsClick(null, null);
                            return;
                        }
                        int slot = json.Value<int?>("SlotNumber") ?? 1;

                        if (slot == 1)
                        {
                            ShowMainView(1);
                            _mainView1?.UpdateTicket(ticket);
                            await AppendLogAsync($"Received ticket for Slot 1: {ticket.Letter} {ticket.Number} {ticket.Color}");
                        }
                        else if (slot == 2)
                        {
                            ShowMainView(2);
                            _mainView2?.UpdateTicket(ticket);
                            await AppendLogAsync($"Received ticket for Slot 2: {ticket.Letter} {ticket.Number} {ticket.Color}");
                        }
                    }
                    catch (Newtonsoft.Json.JsonException)
                    {
                        // Not JSON, treat as plain string
                        await AppendLogAsync($"Received message: {message}");

                        // Optionally, update UI or handle the string as needed
                        Dispatcher.UIThread.Post(() =>
                        {
                            // Assuming PrizeText is a TextBlock in your XAML
                            var prizeText = this.FindControl<TextBlock>("PrizeText");
                            if (prizeText != null)
                            {
                                // Update the PrizeText with the received message
                                prizeText.Text = message;
                            }
                        });
                    }
                });
            }
        }
    }

    private async Task ShowMainView(int slot)
    {
        if (slot == 1 && !(_ticketSlot1.Content is TicketTemplate))
        {
            if (_ticketSlot1.Content is RedrawTicketTemplate) _ticketSlot1.Content = null;
            _mainView1 = new TicketTemplate(this);
            await AnimateTicketToSlot(_mainView1, 1);

            // 5 second delay after slot 1 animation
            await Task.Delay(5000);
        }
        else if (slot == 2 && !(_ticketSlot2.Content is TicketTemplate))
        {
            if (_ticketSlot2.Content is RedrawTicketTemplate) _ticketSlot2.Content = null;
            _mainView2 = new TicketTemplate(this);
            await AnimateTicketToSlot(_mainView2, 2);

            // 5 second delay after slot 1 animation
            await Task.Delay(5000);
        }
        _ = AppendLogAsync($"Showing main view for Slot {slot}");
    }
    
    private void HideMainView(int slot)
    {
        if (slot == 1 && _mainView1 != null)
        {
            _ticketSlot1.Content = null;
            _mainView1 = null;
        }
        else if (slot == 2 && _mainView2 != null)
        {
            _ticketSlot2.Content = null;
            _mainView2 = null;
        }
    }

    public async Task SetSshStatus(bool connected)
    {
        await Task.Delay(500); // 500 ms delay
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var indicator = this.FindControl<Ellipse>("SshStatusIndicator");
            if (indicator != null)
            {
                indicator.Fill = connected
                    ? new SolidColorBrush(Colors.Green)
                    : new SolidColorBrush(Colors.Red);
            }
        });
    }

    public class TicketInfo
    {
        public string? Letter { get; set; }
        public string? Number { get; set; }
        public string? Color { get; set; }
        public string? SlotNumber { get; set; }
    }
    public void UpdatePrize(string prize)
    {
        Dispatcher.UIThread.Post(() =>
        {
                PrizeText.Text = prize;
        });
    }
    public async Task SimulateWorkAsync()
    {
        await AppendLogAsync("Waiting...");
        await Task.Delay(1000); // Simulate work or waiting
        await AppendLogAsync("Done!");
    }

    public async void OnDebugTicketClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ticket = new TicketInfo
        {
            Letter = "A",
            Number = "13",
            Color = "#FF0000",
            SlotNumber = "1"
        };
        
        _mainView1?.UpdateTicket(ticket);
        ShowMainView(1);
        await AppendLogAsync("Debug ticket created in Slot 1.");
    }
    public async void OnDebugTicket2Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ticket = new TicketInfo
        {
            Letter = "B",
            Number = "42",
            Color = "#00FF00",
            SlotNumber = "2"
        };
        
        _mainView2?.UpdateTicket(ticket);
        ShowMainView(2);
        await AppendLogAsync("Debug ticket created in Slot 2.");
    }
    public async void OnClearTicketsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideMainView(1);
        HideMainView(2);
        await AppendLogAsync("Cleared all tickets.");
    }
    public async void OnRedrawTickets1Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RedrawTicket();
    }
    public async void RedrawTicket(int slot = 1)
    {
        if (_mainView1 == null) return;

        if (slot == 1)
        {
            _ticketSlot1.Content = null; // Clear the slot content
            _ticketSlot1.Content = _mainView1.RedrawTicket();
            await AppendLogAsync("Redrawing ticket in Slot 1.");
        }
        else if (slot == 2)
        {
            _ticketSlot2.Content = null; // Clear the slot content
            _ticketSlot2.Content = _mainView2.RedrawTicket();
            await AppendLogAsync("Redrawing ticket in Slot 2.");
        }


    }
    private void RemoveTicketFromParent(TicketTemplate ticket)
    {
        // Remove ticket from any previous parent before adding to overlay
        if (ticket.Parent is Panel oldPanel)
            oldPanel.Children.Remove(ticket);
        else if (ticket.Parent is ContentControl oldContent)
            oldContent.Content = null;
        else if (ticket.Parent is Decorator oldDecorator)
            oldDecorator.Child = null;
        else if (ticket.Parent != null)
            throw new InvalidOperationException("TicketTemplate is already attached to an unsupported parent type.");
    }

    private (double initialWidth, double initialHeight, double initialX, double initialY) GetInitialTicketParams(TicketTemplate ticket)
    {
        // Now get the correct initial size and position
        double initialWidth = ticket.Bounds.Width *2 ;
        double initialHeight = ticket.Bounds.Height *2 ;
        double initialX = (this.Bounds.Width - initialWidth) / 2;
        double initialY = ((this.Bounds.Height - initialHeight) / 2);
        ticket.Width = initialWidth;
        ticket.Height = initialHeight;
        Canvas.SetLeft(ticket, initialX);
        Canvas.SetTop(ticket, initialY);
        return (initialWidth, initialHeight, initialX, initialY);
    }

    private (double targetWidth, double targetHeight, double targetX, double targetY) GetTargetSlotParams(ContentControl slotControl)
    {
        if (slotControl.Parent is not Border targetBorder)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        var borderPos = targetBorder.TranslatePoint(new Point(0, 0), this);
        if (borderPos == null)
            throw new InvalidOperationException("Could not determine border position.");

        double targetWidth = slotControl.Bounds.Width;    // Use slot's actual width
        double targetHeight = slotControl.Bounds.Height;  // Use slot's actual height
        double targetX = borderPos.Value.X + (targetBorder.Bounds.Width - targetWidth) / 2;
        double targetY = borderPos.Value.Y + ((targetBorder.Bounds.Height - targetHeight) / 2);
        return (targetWidth, targetHeight, targetX, targetY);
    }

    private async Task FadeOverlayAsync(double from, double to, int duration)
    {
        if (DarkOverlay != null)
        {
            int steps = 30;
            double step = (to - from) / steps;
            for (int i = 0; i <= steps; i++)
            {
                double opacity = from + step * i;
                DarkOverlay.Opacity = opacity;
                await Task.Delay(duration / steps);
            }
            DarkOverlay.Opacity = to; // Ensure final value
        }
    }

    private async Task AnimateTicketAndSizeAsync(TicketTemplate ticket, Border shadow, double initialWidth, double initialHeight, double initialX, double initialY, double targetWidth, double targetHeight, double targetX, double targetY, int duration, int steps)
    {
        // Animate position and size (lerp)
        for (int i = 0; i <= steps; i++)
        {

            double t = (double)i / steps;
            double eased = new SineEaseInOut().Ease(t);

            double w = initialWidth + (targetWidth - initialWidth) * eased;
            double h = initialHeight + (targetHeight - initialHeight) * eased;

            double x = initialX + (targetX - initialX) * eased;
            double y = initialY + (targetY - initialY) * eased;

            ticket.Width = w;
            ticket.Height = h;
            Canvas.SetLeft(ticket, x);
            Canvas.SetTop(ticket, y);

            // Calculate scale based on width (or height)
            double fontScale = w / 240.0; // 240 is your base MinWidth
            ticket.SetFontScale(fontScale);
            ticket.SetColourStripScale((fontScale + 1));

            await Task.Delay(duration / steps);
        }
    }

    public async Task AnimateTicketToSlot(TicketTemplate ticket, int slot)
    {
        // Fade in overlay BEFORE ticket appears
        await FadeOverlayAsync(0, 1, 100);

        RemoveTicketFromParent(ticket);

        // Add ticket to overlay in the center
        ticket.Opacity = 0; // Start invisible
        _overlayCanvas.Children.Add(ticket);

        // Wait for layout to update so Bounds are valid
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        var (initialWidth, initialHeight, initialX, initialY) = GetInitialTicketParams(ticket);

        // Pause in the center
        ticket.Opacity = 1; // Show the ticket
        await Task.Delay(3000);

        // Find the target slot's ContentControl
        var slotControl = slot == 1 ? _ticketSlot1 : _ticketSlot2;
        if (slotControl == null)
            throw new InvalidOperationException("Slot control is not initialized.");

        // The border is the parent of the slot control
        if (slotControl.Parent is not Border targetBorder)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        var (targetWidth, targetHeight, targetX, targetY) = GetTargetSlotParams(slotControl);

        int duration = 600; // keep the same duration, or increase for slower animation
        int steps = 80;     // double the number of frames for smoother animation
        await AnimateTicketAndSizeAsync(ticket, targetBorder, initialWidth, initialHeight, initialX, initialY, targetWidth, targetHeight, targetX, targetY, duration, steps);

        // Remove from overlay and set as slot content
        _overlayCanvas.Children.Remove(ticket);



        // Set the ticket's Width/Height to match the slot's before adding to the slot
        slotControl = slot == 1 ? _ticketSlot1 : _ticketSlot2;
        if (slotControl != null)
        {
            ticket.Width = slotControl.Bounds.Width;
            ticket.Height = slotControl.Bounds.Height;
        }

        if (slot == 1)
        {
            _mainView1 = ticket;
            _ticketSlot1.Content = ticket;
        }
        else
        {
            _mainView2 = ticket;
            _ticketSlot2.Content = ticket;
        }

        // Hide the dark overlay after animation
        if (DarkOverlay != null)
        {
            int fadeSteps = 30;
            int fadeDuration = 500; // ms
            for (int i = 0; i <= fadeSteps; i++)
            {
                double t = (double)i / fadeSteps;
                double eased = new SineEaseInOut().Ease(1 - t); // 1 -> 0
                DarkOverlay.Opacity = eased;
                await Task.Delay(fadeDuration / fadeSteps);
            }
            DarkOverlay.Opacity = 0; // Ensure it's fully hidden
        }
        // Wait for layout to update in the new parent, then clear explicit size
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }
}
