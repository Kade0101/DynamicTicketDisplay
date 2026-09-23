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
using Avalonia;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TicketDisplayAppModified.Views;

public partial class MainWindow : Window
{
    internal sealed record LiveSlotSnapshot(Point Center, double Width, double Height);
    private sealed class PersistedState
    {
        public string Instructions { get; set; } = string.Empty;
    }
    private const int Port = 5000;
    private static readonly string PersistedStateFilePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DynamicTicketDisplay",
        "server-state.json");
    private readonly Dictionary<int, TicketTemplate?> _mainViews = new();
    private readonly Dictionary<int, TicketInfo?> _currentTickets = new();
    private readonly List<TicketInfo> previouslyDrawnTickets = new();
    private TextBlock? _logTextBlock;
    private Border? _appBackground;
    private Border? _headerPanel;
    private Border? _displayBadge;
    private Border? _ticketFrame1;
    private Border? _ticketFrame2;
    private Border? _infoPanel;
    private Border? _footerPanel;
    private Border? _liveTicketHost1;
    private Border? _liveTicketHost2;
    private Border? _liveTicketHost3;
    private Border? _liveTicketHost4;
    private TextBlock? _prizeLabel;
    private TextBlock? _displayBadgeText;
    private TextBlock? _ticketSectionLabel;
    private TextBlock? _infoPanelTitle;
    private TextBlock? _infoPanelLine1;
    private TextBlock? _infoPanelLine2;
    private TextBlock? _infoPanelLine3;
    private Control? _emptySlot1Placeholder;
    private Control? _emptySlot2Placeholder;
    private StackPanel? _liveDrawSlotsPanel;
    private WrapPanel? _previouslyDrawnTicketsPanel;
    private ScrollViewer? _previouslyDrawnTicketsScrollViewer;
    private TextBlock? _emptySlot1Title;
    private TextBlock? _emptySlot1Body;
    private TextBlock? _emptySlot2Title;
    private TextBlock? _emptySlot2Body;
    private bool _isDebugVisible;

    private ContentControl? _ticketSlot1;
    private ContentControl? _ticketSlot2;
    private ContentControl? _ticketSlot3;
    private ContentControl? _ticketSlot4;
    private Canvas? _overlayCanvas;
    private TextBlock? _prizeText;

    private MainWindowAnimationService _animations = null!;

    public MainWindow()
    {
        InitializeComponent();
        for (int slot = 1; slot <= 4; slot++)
        {
            _mainViews[slot] = null;
            _currentTickets[slot] = null;
        }

        ApplyLiveDisplayLayout();
        LoadPersistedState();
        UpdateSlotPlaceholders();

        Opened += MainWindow_Opened;

        KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
                Close();

            if (e.Key == Avalonia.Input.Key.D)
                ToggleDebugVisibility();
        };
        _animations = new MainWindowAnimationService(this);
        StartTcpServer();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _appBackground = this.FindControl<Border>("AppBackground");
        _headerPanel = this.FindControl<Border>("HeaderPanel");
        _displayBadge = this.FindControl<Border>("DisplayBadge");
        _ticketFrame1 = this.FindControl<Border>("TicketFrame1");
        _ticketFrame2 = this.FindControl<Border>("TicketFrame2");
        _infoPanel = this.FindControl<Border>("InfoPanel");
        _footerPanel = this.FindControl<Border>("FooterPanel");
        _liveTicketHost1 = this.FindControl<Border>("LiveTicketHost1");
        _liveTicketHost2 = this.FindControl<Border>("LiveTicketHost2");
        _liveTicketHost3 = this.FindControl<Border>("LiveTicketHost3");
        _liveTicketHost4 = this.FindControl<Border>("LiveTicketHost4");
        _logTextBlock = this.FindControl<TextBlock>("LogTextBlock");
        _prizeLabel = this.FindControl<TextBlock>("PrizeLabel");
        _displayBadgeText = this.FindControl<TextBlock>("DisplayBadgeText");
        _ticketSectionLabel = this.FindControl<TextBlock>("TicketSectionLabel");
        _infoPanelTitle = this.FindControl<TextBlock>("InfoPanelTitle");
        _infoPanelLine1 = this.FindControl<TextBlock>("InfoPanelLine1");
        _infoPanelLine2 = this.FindControl<TextBlock>("InfoPanelLine2");
        _infoPanelLine3 = this.FindControl<TextBlock>("InfoPanelLine3");
        _liveDrawSlotsPanel = this.FindControl<StackPanel>("LiveDrawSlotsPanel");
        _previouslyDrawnTicketsPanel = this.FindControl<WrapPanel>("PreviouslyDrawnTicketsPanel");
        _previouslyDrawnTicketsScrollViewer = this.FindControl<ScrollViewer>("PreviouslyDrawnTicketsScrollViewer");
        _ticketSlot1 = this.FindControl<ContentControl>("TicketSlot1");
        _ticketSlot2 = this.FindControl<ContentControl>("TicketSlot2");
        _ticketSlot3 = this.FindControl<ContentControl>("TicketSlot3");
        _ticketSlot4 = this.FindControl<ContentControl>("TicketSlot4");
        _emptySlot1Placeholder = this.FindControl<Control>("EmptySlot1Placeholder");
        _emptySlot2Placeholder = this.FindControl<Control>("EmptySlot2Placeholder");
        _emptySlot1Title = this.FindControl<TextBlock>("EmptySlot1Title");
        _emptySlot1Body = this.FindControl<TextBlock>("EmptySlot1Body");
        _emptySlot2Title = this.FindControl<TextBlock>("EmptySlot2Title");
        _emptySlot2Body = this.FindControl<TextBlock>("EmptySlot2Body");
        _overlayCanvas = this.FindControl<Canvas>("OverlayCanvas");
        DarkOverlay = this.FindControl<Border>("DarkOverlay");
        _prizeText = this.FindControl<TextBlock>("PrizeText");

        if (_ticketFrame1 != null)
        {
            _ticketFrame1.PropertyChanged += (_, e) =>
            {
                if (e.Property == BoundsProperty)
                    UpdateLiveDrawSlotLayout();
            };
        }
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        var screens = Screens.All;

        if (screens.Count >= 2)
        {
            // Find the monitor that is NOT the primary monitor.
            var secondScreen = screens.FirstOrDefault(screen => !screen.IsPrimary);

            if (secondScreen != null)
            {
                WindowState = WindowState.Normal;

                Position = new PixelPoint(
                    secondScreen.Bounds.X,
                    secondScreen.Bounds.Y);

                WindowState = WindowState.FullScreen;
                return;
            }
        }

        // Only one monitor connected: just use it normally.
        WindowState = WindowState.FullScreen;
    }
    // INTERNAL ACCESSORS for animation service
    internal Border? DarkOverlayRef => DarkOverlay;
    internal Canvas? OverlayCanvasRef => _overlayCanvas;
    internal ContentControl? GetTicketSlotRef(int slot) => GetLiveTicketSlot(slot);
    internal void SetMainView(int slot, TicketTemplate? ticketTemplate) => _mainViews[slot] = ticketTemplate;
    internal void RefreshLayoutChrome() => Dispatcher.UIThread.Post(() =>
    {
        UpdateSlotPlaceholders();
        UpdateLiveDrawSlotLayout();
    });

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
                                _prizeText.Text = message.Trim('"'); ;
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

                    int slotIndex = ParseSlotNumber(ticket.SlotNumber);
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
            UpdatePrize(prize.Trim('"'));
            await AppendLogAsync($"Prize updated: {prize}");
            return;
        }
        else if (context == "Clear")
        {
            for (int slot = 1; slot <= 4; slot++)
                HideMainView(slot);

            await AppendLogAsync("All tickets cleared.");
            return;
        }
        else if (context == "Confirm")
        {
            SaveCurrentTicketsToPreviouslyDrawnList();
            RefreshPreviouslyDrawnTicketsDisplay();
            await AppendLogAsync($"Saved {previouslyDrawnTickets.Count} confirmed tickets to history.");
            return;
        }
        else if (context == "Instructions")
        {
            var instructions = json["Item1"]?.ToString() ?? string.Empty;
            if (_infoPanelLine1 != null)
                _infoPanelLine1.Text = instructions;

            SavePersistedState();
            await AppendLogAsync("Instructions updated and saved.");
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

                int slotIndex = ParseSlotNumber(ticket.SlotNumber);
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
        var slotControl = GetLiveTicketSlot(slot);
        if (slotControl == null || slotControl.Content is TicketTemplate)
            return;

        if (slotControl.Content is RedrawTicketTemplate)
            slotControl.Content = null;

        var previousLayout = CaptureActiveTicketLayout();
        var mainView = new TicketTemplate(this);
        mainView.UpdateTicket(ticket);
        _mainViews[slot] = mainView;
        _currentTickets[slot] = CloneTicketInfo(ticket);
        UpdateLiveDrawSlotLayout();
        await _animations.AnimateLiveSlotReflowAsync(previousLayout);
        await _animations.AnimateTicketToSlotAsync(mainView, slot);
        await Task.Delay(5000);
    }

    private void HideMainView(int slot)
    {
        var slotControl = GetLiveTicketSlot(slot);
        if (slotControl == null)
            return;

        if (_mainViews.TryGetValue(slot, out var mainView) && mainView != null)
        {
            var previousLayout = CaptureActiveTicketLayout();
            slotControl.Content = null;
            _mainViews[slot] = null;
            _currentTickets[slot] = null;
            UpdateSlotPlaceholders();
            UpdateLiveDrawSlotLayout();
            _ = _animations.AnimateLiveSlotReflowAsync(previousLayout);
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

    private void UpdateSlotPlaceholders()
    {
        if (_emptySlot1Placeholder != null)
            _emptySlot1Placeholder.IsVisible = GetActiveLiveTicketCount() == 0;

        if (_emptySlot2Placeholder != null)
            _emptySlot2Placeholder.IsVisible = previouslyDrawnTickets.Count == 0;

        if (_previouslyDrawnTicketsScrollViewer != null)
            _previouslyDrawnTicketsScrollViewer.IsVisible = previouslyDrawnTickets.Count > 0;

        UpdateLiveDrawSlotLayout();
    }

    private void SaveCurrentTicketsToPreviouslyDrawnList()
    {
        foreach (var ticket in _currentTickets
                     .OrderBy(entry => entry.Key)
                     .Select(entry => entry.Value)
                     .Where(ticket => ticket != null))
        {
            previouslyDrawnTickets.Add(CloneTicketInfo(ticket!));
        }
    }

    private void RefreshPreviouslyDrawnTicketsDisplay()
    {
        if (_previouslyDrawnTicketsPanel == null)
        {
            UpdateSlotPlaceholders();
            return;
        }

        _previouslyDrawnTicketsPanel.Children.Clear();

        foreach (var ticket in previouslyDrawnTickets
                     .AsEnumerable()
                     .Reverse()
                     .Take(8))
        {
            var row = new Border
            {
                Width = 250,
                CornerRadius = new CornerRadius(16),
                Background = Brush("#F8FAFC"),
                BorderBrush = Brush("#DBEAFE"),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(14, 10)
            };

            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            layout.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
            layout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var colorBand = new Border
            {
                Width = 12,
                CornerRadius = new CornerRadius(6),
                Background = Brush(MapTicketColor(ticket.Color))
            };

            var details = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            details.Children.Add(new TextBlock
            {
                Text = $"{ticket.Letter?.ToUpperInvariant()} {ticket.Number}",
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = Brush("#0F172A")
            });

            details.Children.Add(new TextBlock
            {
                Text = (ticket.Color ?? string.Empty).ToUpperInvariant(),
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(MapTicketColor(ticket.Color))
            });

            Grid.SetColumn(colorBand, 0);
            Grid.SetColumn(details, 2);
            layout.Children.Add(colorBand);
            layout.Children.Add(details);
            row.Child = layout;
            _previouslyDrawnTicketsPanel.Children.Add(row);
        }

        UpdateSlotPlaceholders();
    }

    private static TicketInfo CloneTicketInfo(TicketInfo ticket) => new()
    {
        Letter = ticket.Letter,
        Number = ticket.Number,
        Color = ticket.Color,
        SlotNumber = ticket.SlotNumber
    };

    private static string MapTicketColor(string? colorName)
    {
        return (colorName ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "grey" => "#9CA3AF",
            "blue" => "#4682B4",
            "orange" => "#F97316",
            "yellow" => "#FACC15",
            "tangerine" => "#FF4500",
            "pink" => "#EC4899",
            "green" => "#2E8B57",
            _ => "#D1D5DB"
        };
    }

    private void LoadPersistedState()
    {
        try
        {
            if (!File.Exists(PersistedStateFilePath))
                return;

            var json = File.ReadAllText(PersistedStateFilePath);
            var state = JsonConvert.DeserializeObject<PersistedState>(json);
            if (_infoPanelLine1 != null)
                _infoPanelLine1.Text = state?.Instructions ?? string.Empty;
        }
        catch
        {
        }
    }

    private void SavePersistedState()
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(PersistedStateFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var state = new PersistedState
            {
                Instructions = _infoPanelLine1?.Text ?? string.Empty
            };

            File.WriteAllText(PersistedStateFilePath, JsonConvert.SerializeObject(state, Formatting.Indented));
        }
        catch
        {
        }
    }

    private int ParseSlotNumber(string? slotNumber)
    {
        return int.TryParse(slotNumber, out var parsed)
            ? Math.Clamp(parsed, 1, 4)
            : 1;
    }

    private ContentControl? GetLiveTicketSlot(int slot) => slot switch
    {
        1 => _ticketSlot1,
        2 => _ticketSlot2,
        3 => _ticketSlot3,
        4 => _ticketSlot4,
        _ => null
    };

    private Border? GetLiveTicketHost(int slot) => slot switch
    {
        1 => _liveTicketHost1,
        2 => _liveTicketHost2,
        3 => _liveTicketHost3,
        4 => _liveTicketHost4,
        _ => null
    };

    private int GetActiveLiveTicketCount()
    {
        int count = 0;
        for (int slot = 1; slot <= 4; slot++)
        {
            if ((_mainViews.TryGetValue(slot, out var mainView) && mainView != null) ||
                GetLiveTicketSlot(slot)?.Content is TicketTemplate)
                count++;
        }

        return count;
    }

    private Dictionary<int, LiveSlotSnapshot> CaptureActiveTicketLayout()
    {
        var snapshots = new Dictionary<int, LiveSlotSnapshot>();
        var relativeTo = (Visual?)_overlayCanvas ?? this;

        for (int slot = 1; slot <= 4; slot++)
        {
            var slotControl = GetLiveTicketSlot(slot);
            if (slotControl?.Content is not TicketTemplate ticket)
                continue;

            var center = slotControl.TranslatePoint(
                new Point(slotControl.Bounds.Width / 2, slotControl.Bounds.Height / 2),
                relativeTo);

            if (center.HasValue)
            {
                double width = ticket.Bounds.Width > 0 ? ticket.Bounds.Width : slotControl.Bounds.Width;
                double height = ticket.Bounds.Height > 0 ? ticket.Bounds.Height : slotControl.Bounds.Height;
                snapshots[slot] = new LiveSlotSnapshot(center.Value, width, height);
            }
        }

        return snapshots;
    }

    private void UpdateLiveDrawSlotLayout()
    {
        if (_ticketFrame1 == null || _liveDrawSlotsPanel == null)
            return;

        var activeHosts = new List<Border>();
        for (int slot = 1; slot <= 4; slot++)
        {
            var host = GetLiveTicketHost(slot);
            var slotControl = GetLiveTicketSlot(slot);
            if (host == null || slotControl == null)
                continue;

            bool isActive = (_mainViews.TryGetValue(slot, out var mainView) && mainView != null) ||
                            slotControl.Content is TicketTemplate;
            host.IsVisible = isActive;
            if (isActive)
                activeHosts.Add(host);
        }

        _liveDrawSlotsPanel.IsVisible = activeHosts.Count > 0;
        if (activeHosts.Count == 0)
            return;

        const double baseWidth = 240d;
        const double baseHeight = 260d;
        double spacing = _liveDrawSlotsPanel.Spacing;
        double availableWidth = Math.Max(0, _ticketFrame1.Bounds.Width - _ticketFrame1.Padding.Left - _ticketFrame1.Padding.Right - 24);
        double availableHeight = Math.Max(0, _ticketFrame1.Bounds.Height - _ticketFrame1.Padding.Top - _ticketFrame1.Padding.Bottom - 24);
        double widthFromRow = (availableWidth - (spacing * (activeHosts.Count - 1))) / activeHosts.Count;
        double widthFromHeight = availableHeight * (baseWidth / baseHeight);
        double targetWidth = Math.Max(180, Math.Min(widthFromRow, widthFromHeight));
        double targetHeight = targetWidth * (baseHeight / baseWidth);

        foreach (var host in activeHosts)
        {
            host.Width = targetWidth;
            host.Height = targetHeight;
        }
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

    private void ApplyLiveDisplayLayout()
    {
        Background = Brush("#F8FAFC");

        if (_appBackground != null)
            _appBackground.Background = Brush("#EFF6FF");

        if (_headerPanel != null)
        {
            _headerPanel.Margin = new Thickness(70, 24, 70, 14);
            _headerPanel.Padding = new Thickness(24, 18);
            _headerPanel.CornerRadius = new CornerRadius(22);
            _headerPanel.Background = Brush("#FFFFFF");
            _headerPanel.BorderBrush = Brush("#BFDBFE");
            _headerPanel.BorderThickness = new Thickness(2);
        }

        if (_footerPanel != null)
            _footerPanel.Background = Brush("#DBEAFE");

        if (_ticketFrame1 != null)
            ApplyFrameStyle(_ticketFrame1, "#E5E7EB", "#BFDBFE", 24, 16, 3);

        if (_ticketFrame2 != null)
            ApplyFrameStyle(_ticketFrame2, "#FFFFFF", "#BFDBFE", 24, 16, 3);

        if (_infoPanel != null)
        {
            _infoPanel.IsVisible = true;
            _infoPanel.Background = Brush("#FFFFFF");
            _infoPanel.BorderBrush = Brush("#BFDBFE");
            _infoPanel.BorderThickness = new Thickness(3);
            _infoPanel.CornerRadius = new CornerRadius(24);
            _infoPanel.Padding = new Thickness(24, 20);
        }

        if (_prizeLabel != null) _prizeLabel.Foreground = Brush("#2563EB");
        if (_prizeText != null) _prizeText.Foreground = Brush("#0F172A");
        if (_ticketSectionLabel != null)
        {
            _ticketSectionLabel.Text = "LIVE TICKET BOARD";
            _ticketSectionLabel.Foreground = Brush("#2563EB");
        }
        if (_displayBadge != null)
        {
            _displayBadge.Background = Brush("#E0F2FE");
            _displayBadge.BorderBrush = Brush("#7DD3FC");
        }
        if (_displayBadgeText != null)
        {
            _displayBadgeText.Text = "LIVE DISPLAY";
            _displayBadgeText.Foreground = Brush("#0369A1");
        }

        SetPlaceholderCopy("LIVE DRAW SLOT", "Current ticket fills this board", "PREVIOUS TICKET", "Most recent ticket stays here");
        SetPlaceholderTextColors("#334155", "#64748B");

        if (_infoPanelTitle != null) _infoPanelTitle.Text = "HOW TO CLAIM";
        if (_infoPanelLine1 != null) _infoPanelLine1.Text = "Present your ticket at the counter";
        if (_infoPanelLine2 != null) _infoPanelLine2.Text = "Keep the colour and number visible";
        if (_infoPanelLine3 != null) _infoPanelLine3.Text = "Must be present when called";

        UpdateSlotPlaceholders();
    }

    private static void ApplyFrameStyle(Border frame, string background, string borderBrush, double radius, double padding, double borderThickness)
    {
        frame.Background = Brush(background);
        frame.BorderBrush = Brush(borderBrush);
        frame.CornerRadius = new CornerRadius(radius);
        frame.Padding = new Thickness(padding);
        frame.BorderThickness = new Thickness(borderThickness);
    }

    private void SetPlaceholderTextColors(string titleColor, string bodyColor)
    {
        if (_emptySlot1Title != null) _emptySlot1Title.Foreground = Brush(titleColor);
        if (_emptySlot1Body != null) _emptySlot1Body.Foreground = Brush(bodyColor);
        if (_emptySlot2Title != null) _emptySlot2Title.Foreground = Brush(titleColor);
        if (_emptySlot2Body != null) _emptySlot2Body.Foreground = Brush(bodyColor);
    }

    private void SetPlaceholderCopy(string slot1Title, string slot1Body, string slot2Title, string slot2Body)
    {
        if (_emptySlot1Title != null) _emptySlot1Title.Text = slot1Title;
        if (_emptySlot1Body != null) _emptySlot1Body.Text = slot1Body;
        if (_emptySlot2Title != null) _emptySlot2Title.Text = slot2Title;
        if (_emptySlot2Body != null) _emptySlot2Body.Text = slot2Body;
    }

    private void ToggleDebugVisibility()
    {
        _isDebugVisible = !_isDebugVisible;

        if (_footerPanel != null)
            _footerPanel.IsVisible = _isDebugVisible;
    }

    public async void OnDebugTicketClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ticket = new TicketInfo { Letter = "A", Number = "13", Color = "#FF0000", SlotNumber = "1" };
        ShowMainView(1, ticket);
        await AppendLogAsync("Debug ticket created in Slot 1.");
    }

    public async void OnDebugTicket2Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var ticket = new TicketInfo { Letter = "B", Number = "42", Color = "#00FF00", SlotNumber = "2" };
        ShowMainView(2, ticket);
        await AppendLogAsync("Debug ticket created in Slot 2.");
    }

    public async void OnClearTicketsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideMainView(1);
        HideMainView(2);
        HideMainView(3);
        HideMainView(4);
        await AppendLogAsync("Cleared all tickets.");
    }

    public async void OnRedrawTickets1Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RedrawTicket();
    }

    public async void RedrawTicket(int slot = 1)
    {
        if (!_mainViews.TryGetValue(slot, out var mainView) || mainView == null) return;

        var slotControl = GetLiveTicketSlot(slot);
        if (slotControl == null) return;

        if (slot == 1)
        {
            slotControl.Content = null;
            slotControl.Content = mainView.RedrawTicket();
            UpdateSlotPlaceholders();
            await AppendLogAsync("Redrawing ticket in Slot 1.");
        }
        else if (slot == 2)
        {
            slotControl.Content = null;
            slotControl.Content = mainView.RedrawTicket();
            UpdateSlotPlaceholders();
            await AppendLogAsync("Redrawing ticket in Slot 2.");
        }
    }

}
