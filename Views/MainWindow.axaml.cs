using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Threading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TicketDisplayAppModified.Views;

public partial class MainWindow : Window
{
    private const int Port = 5000;
    private TicketTemplate? _mainView1;
    private TicketTemplate? _mainView2;
    private TextBlock? _logTextBlock;

    private ContentControl? _ticketSlot1;
    private ContentControl? _ticketSlot2;
    private Canvas? _overlayCanvas;
    private TextBlock? _prizeText;

    private readonly List<double> _frameDtsMs = new(180);

    private readonly string _logFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TicketApp", "ticketapp.log");

    private const bool RequireGpu = true;

    // NEW: animation service
    private MainWindowAnimationService _animations = null!;

    public MainWindow()
    {
        InitializeComponent();
        WindowState = WindowState.FullScreen;
        KeyDown += (s, e) => { if (e.Key == Avalonia.Input.Key.Escape) Close(); };
        _animations = new MainWindowAnimationService(this);
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
        _prizeText = this.FindControl<TextBlock>("PrizeText");
        _mainView2 = this.FindControl<TicketTemplate>("MainView2");
        _mainView1 = this.FindControl<TicketTemplate>("MainView1");
    }

    // INTERNAL ACCESSORS for animation service
    internal Border? DarkOverlayRef => DarkOverlay;
    internal Canvas? OverlayCanvasRef => _overlayCanvas;
    internal ContentControl? TicketSlot1Ref => _ticketSlot1;
    internal ContentControl? TicketSlot2Ref => _ticketSlot2;
    internal void SetMainView1(TicketTemplate t) => _mainView1 = t;
    internal void SetMainView2(TicketTemplate t) => _mainView2 = t;

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
                await AppendLogAsync(message);

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(message);
                        await RecogniseMessage(json);
                    }
                    catch (Newtonsoft.Json.JsonException ex)
                    {
                        await AppendLogAsync($"Invalid JSON Exception: {ex.Message}");

                        Dispatcher.UIThread.Post(() =>
                        {
                            if (_prizeText != null)
                            {
                                _prizeText.Text = message;
                            }
                        });
                    }
                });
            }
        }
    }

    private async Task RecogniseMessage(Newtonsoft.Json.Linq.JObject json)
    {
        // 1. Check for the tuple-style "Context" field
        string context = json["Item2"]?.ToString() ?? "";
        await AppendLogAsync($"Context: {context}");

        // FALLBACK: If Item2 is missing, check if this is a direct, raw TicketInfo message
        if (string.IsNullOrEmpty(context) && json["SlotNumber"] != null)
        {
            try
            {
                var ticket = json.ToObject<TicketInfo>();
                if (ticket != null)
                {
                    // Ensure SlotNumber is stored as a string internally, even if sent as a number
                    ticket.SlotNumber = json["SlotNumber"]?.ToString();

                    int slotIndex = ticket.SlotNumber == "1" ? 1 : 2;
                    await ShowMainView(slotIndex, ticket);
                    await AppendLogAsync($"Direct ticket info received for Slot {ticket.SlotNumber}: {ticket.Letter} {ticket.Number} {ticket.Color}");
                    return;
                }
            }
            catch (Exception ex)
            {
                await AppendLogAsync($"Direct Ticket parsing exception: {ex.Message}");
                return;
            }
        }

        // 2. Handle original wrapped Tuple formats ("Item1" & "Item2")
        if (context == "Prize")
        {
            string prize = json["Item1"]?.ToString() ?? "";
            UpdatePrize(prize);
            await AppendLogAsync($"Prize updated: {prize}");
            return;
        }
        else if (context == "Clear")
        {
            HideMainView(1);
            HideMainView(2);
            await AppendLogAsync("All tickets cleared.");
            return;
        }
        else if (context == "TicketInfo")
        {
            try
            {
                var ticketToken = json["Item1"];
                if (ticketToken == null)
                {
                    await AppendLogAsync("TicketInfo: Item1 is missing in JSON.");
                    return;
                }

                var ticket = ticketToken.ToObject<TicketInfo>();
                if (ticket == null)
                {
                    await AppendLogAsync("TicketInfo: Failed to parse Item1 as TicketInfo.");
                    return;
                }

                // Sync structural difference: force SlotNumber to string representation
                ticket.SlotNumber = ticketToken["SlotNumber"]?.ToString();

                int slotIndex = ticket.SlotNumber == "1" ? 1 : 2;
                await ShowMainView(slotIndex, ticket);
                await AppendLogAsync($"Ticket info received for Slot {ticket.SlotNumber}: {ticket.Letter} {ticket.Number} {ticket.Color}");
            }
            catch (Exception ex)
            {
                await AppendLogAsync($"TicketInfo: Exception - {ex.Message}");
            }
            return;
        }

        await AppendLogAsync($"Unrecognized message format: {json}");
    }


    private async Task ShowMainView(int slot, TicketInfo ticket)
    {
        if (slot == 1 && !(_ticketSlot1.Content is TicketTemplate))
        {
            if (_ticketSlot1.Content is RedrawTicketTemplate) _ticketSlot1.Content = null;
            _mainView1 = new TicketTemplate(this);
            _mainView1?.UpdateTicket(ticket);
            await _animations.AnimateTicketToSlotAsync(_mainView1, 1);
            await Task.Delay(5000);
        }
        else if (slot == 2 && !(_ticketSlot2.Content is TicketTemplate))
        {
            if (_ticketSlot2.Content is RedrawTicketTemplate) _ticketSlot2.Content = null;
            _mainView2 = new TicketTemplate(this);
            _mainView2?.UpdateTicket(ticket);
            await _animations.AnimateTicketToSlotAsync(_mainView2, 2);
            await Task.Delay(5000);
        }
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
        await Task.Delay(200);
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
            if (_prizeText != null)
                _prizeText.Text = prize;
        });
    }



    public async void OnDebugTicketClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ticket = new TicketInfo { Letter = "A", Number = "13", Color = "#FF0000", SlotNumber = "1" };
        _mainView1?.UpdateTicket(ticket);
        ShowMainView(1, ticket);
        await AppendLogAsync("Debug ticket created in Slot 1.");
    }

    public async void OnDebugTicket2Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ticket = new TicketInfo { Letter = "B", Number = "42", Color = "#00FF00", SlotNumber = "2" };
        _mainView2?.UpdateTicket(ticket);
        ShowMainView(2, ticket);
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
            _ticketSlot1.Content = null;
            _ticketSlot1.Content = _mainView1.RedrawTicket();
            await AppendLogAsync("Redrawing ticket in Slot 1.");
        }
        else if (slot == 2)
        {
            _ticketSlot2.Content = null;
            _ticketSlot2.Content = _mainView2.RedrawTicket();
            await AppendLogAsync("Redrawing ticket in Slot 2.");
        }
    }

    private void RemoveTicketFromParent(TicketTemplate ticketTemplate)
    {
        if (ticketTemplate.Parent is Panel oldPanel)
            oldPanel.Children.Remove(ticketTemplate);
        else if (ticketTemplate.Parent is ContentControl oldContent)
            oldContent.Content = null;
        else if (ticketTemplate.Parent is Decorator oldDecorator)
            oldDecorator.Child = null;
        else if (ticketTemplate.Parent != null)
            throw new InvalidOperationException("TicketTemplate is already attached to an unsupported parent type.");
    }

    private (double initialWidth, double initialHeight, double initialX, double initialY) GetInitialTicketParams(TicketTemplate ticket)
    {
        double initialWidth = this.Bounds.Width * 0.3;
        double initialHeight = this.Bounds.Height * 0.5;
        double initialX = (this.Bounds.Width - initialWidth) / 2;
        double initialY = (this.Bounds.Height - initialHeight) / 2;

        ticket.Width = initialWidth;
        ticket.Height = initialHeight;
        Canvas.SetLeft(ticket, initialX);
        Canvas.SetTop(ticket, initialY);

        // One-time sizing for internals
        double fontScale = initialWidth / 240.0;
        ticket.SetColourStripScale(fontScale + 1);

        return (initialWidth, initialHeight, initialX, initialY);
    }

    private (double targetWidth, double targetHeight, double targetX, double targetY) GetTargetSlotParams(ContentControl slotControl)
    {
        if (slotControl.Parent is not Border targetBorder)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        var borderPos = targetBorder.TranslatePoint(new Point(0, 0), this);
        if (borderPos == null)
            throw new InvalidOperationException("Could not determine border position.");

        double targetWidth = slotControl.Bounds.Width;
        double targetHeight = slotControl.Bounds.Height;
        double targetX = borderPos.Value.X + (targetBorder.Bounds.Width - targetWidth) / 2;
        double targetY = borderPos.Value.Y + ((targetBorder.Bounds.Height - targetHeight) / 2);
        return (targetWidth, targetHeight, targetX, targetY);
    }

    private static void EnsureTransition(Animatable target, AvaloniaProperty<double> prop, int durationMs, Easing? easing = null)
    {
        target.Transitions ??= new Transitions();
        var t = target.Transitions.OfType<DoubleTransition>().FirstOrDefault(x => x.Property == prop);
        if (t == null)
        {
            target.Transitions.Add(new DoubleTransition
            {
                Property = prop,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Easing = easing ?? new SineEaseInOut()
            });
        }
        else
        {
            t.Duration = TimeSpan.FromMilliseconds(durationMs);
            t.Easing = easing ?? new SineEaseInOut();
        }
    }

    private async Task AnimateTransformToSlotAsync(Control target,
        double initialWidth, double initialHeight, double initialX, double initialY,
        double targetWidth, double targetHeight, double targetX, double targetY,
        int durationMs)
    {
        // Ensure transform group with scale + translate
        var group = target.RenderTransform as TransformGroup ?? new TransformGroup();
        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault() ?? new ScaleTransform(1, 1);
        var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault() ?? new TranslateTransform(0, 0);
        if (!group.Children.Contains(scale)) group.Children.Insert(0, scale);
        if (!group.Children.Contains(translate)) group.Children.Add(translate);
        target.RenderTransform = group;
        target.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

        // Set initial layout once
        target.Width = initialWidth;
        target.Height = initialHeight;
        Canvas.SetLeft(target, initialX);
        Canvas.SetTop(target, initialY);

        double sx = targetWidth / initialWidth;
        double sy = targetHeight / initialHeight;
        double dx = targetX - initialX;
        double dy = targetY - initialY;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Start state
            scale.ScaleX = 1d;
            scale.ScaleY = 1d;
            translate.X = 0d;
            translate.Y = 0d;

            // Transitions
            EnsureTransition(scale, ScaleTransform.ScaleXProperty, durationMs, new SineEaseInOut());
            EnsureTransition(scale, ScaleTransform.ScaleYProperty, durationMs, new SineEaseInOut());
            EnsureTransition(translate, TranslateTransform.XProperty, durationMs, new SineEaseInOut());
            EnsureTransition(translate, TranslateTransform.YProperty, durationMs, new SineEaseInOut());

            // final
            scale.ScaleX = sx;
            scale.ScaleY = sy;
            translate.X = dx;
            translate.Y = dy;
        }, DispatcherPriority.Render);

        await Task.Delay(durationMs + 16).ConfigureAwait(false);
    }

    // Old method kept for signature compatibility but now calls the transition-based path
    private async Task AnimateTicketAndSizeAsync(
        TicketTemplate ticket,
        Border _,
        double initialWidth, double initialHeight, double initialX, double initialY,
        double targetWidth, double targetHeight, double targetX, double targetY,
        int duration, int steps)
    {
        await AnimateTransformToSlotAsync(ticket, initialWidth, initialHeight, initialX, initialY, targetWidth, targetHeight, targetX, targetY, duration);
    }

    public async Task AnimateTicketToSlot(TicketTemplate ticket, int slot)
    {
        // darkens background
        await _animations.FadeOverlayAsync(0, 0.3, 100);

        // Detach from any existing parent
        RemoveTicketFromParent(ticket);

        // Add ticket to overlay in the center (changes the parent)
        ticket.Opacity = 0;
        if (_overlayCanvas != null)
        {
            _overlayCanvas.Children.Add(ticket);
        }
        else
        {
            throw new InvalidOperationException("_overlayCanvas is not initialized.");
        }

        // Layout ready
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render); // These two lines lets the UI thread finish its cycle before it measures positions
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render); // Second pass to ensure layout is stable

        var (initialWidth, initialHeight, initialX, initialY) = GetInitialTicketParams(ticket);

        // Show it and play enlarge-in-center
        ticket.Opacity = 1;
        await _animations.EnlargeInCenterAsync(ticket, 250, 0.7);

        // Resolve target
        var slotControl = slot == 1 ? _ticketSlot1 : _ticketSlot2;
        if (slotControl == null)
            throw new InvalidOperationException("Slot control is not initialized.");
        if (slotControl.Parent is not Border targetBorder)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        var (targetWidth, targetHeight, targetX, targetY) = GetTargetSlotParams(slotControl);

        // Travel to slot via transform animation
        int duration = 600;
        await AnimateTicketAndSizeAsync(ticket, targetBorder, initialWidth, initialHeight, initialX, initialY, targetWidth, targetHeight, targetX, targetY, duration, 0);

        // Remove from overlay and set as slot content
        _overlayCanvas.Children.Remove(ticket);

        // Reset transform before attaching to the slot
        ticket.RenderTransform = null;

        // Match the slot size and update visuals once
        if (slotControl != null)
        {
            ticket.Width = slotControl.Bounds.Width;
            ticket.Height = slotControl.Bounds.Height;
            ticket.SetFontScale();
            ticket.SetColourStripScale((ticket.Width / 240.0) + 1);
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

        // Fade out overlay
        await _animations.FadeOverlayAsync(0.3, 0, 120);

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }
}
