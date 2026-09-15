using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using System.Linq;

namespace RaffleDisplayApplication;

public partial class MainWindow : Window
{
    private readonly string ipAddress = "127.0.0.1";
    private bool isRedrawMode;

public MainWindow()
{
    InitializeComponent();

    Opened += MainWindow_Opened;

    SetCreateTicketButton(TicketSlot1, 1);
    SetCreateTicketButton(TicketSlot2, 2);
    UpdateShowOnTVButtonState();
}

    private void RedrawToggleButton_IsCheckedChanged(
        object? sender,
        RoutedEventArgs e)
    {
        isRedrawMode = RedrawToggleButton.IsChecked == true;
    }
    private void MainWindow_Opened(object? sender, EventArgs e)
{
    var screens = Screens.All;

    if (screens.Count >= 2)
    {
        // Put the client on the primary monitor.
        var primaryScreen = Screens.Primary;

        Position = new PixelPoint(
            primaryScreen.Bounds.X,
            primaryScreen.Bounds.Y);

        WindowState = WindowState.Maximized;
    }
}

    private void SetCreateTicketButton(ContentControl slot, int slotNumber)
    {
        var button = new Button
        {
            Content = "Create Ticket"
        };

        button.Classes.Add("createTicket");

        button.Click += async (_, _) =>
            await TicketSlot_ClickAsync(slot, slotNumber);

        slot.Content = button;
        slot.Tag = slotNumber;

        SetClearButtonVisibility(slotNumber, false);
    }

    private async Task TicketSlot_ClickAsync(
        ContentControl slot,
        int slotNumber)
    {
        if (isRedrawMode)
        {
            await RedrawTicketAsync(slot, slotNumber);
            RedrawToggleButton.IsChecked = false;
            return;
        }

        await CreateTicketAsync(slot, slotNumber);
    }

    private Task RedrawTicketAsync(
        ContentControl slot,
        int slotNumber)
    {
        return CreateTicketAsync(slot, slotNumber);
    }

    private async Task CreateTicketAsync(
        ContentControl slot,
        int slotNumber)
    {
        var inputWindow = new InputWindow
        {
            SlotNumber = slotNumber
        };

        // The converted Avalonia InputWindow should call Close(true)
        // when the user confirms and Close(false) when they cancel.
        bool accepted = await inputWindow.ShowDialog<bool>(this);

        if (!accepted)
            return;

        var ticket = inputWindow.GetTicket();

        if (ticket == null)
            return;

        var display = new TicketDisplay
        {
            RaffleNumber = ticket.Number ?? string.Empty,
            Letter = ticket.Letter?.ToUpperInvariant() ?? string.Empty,
            ColorName = ticket.Color ?? string.Empty,
            ColorNameBrush = GetBrushForColor(ticket.Color),
            ColorStripBrush = GetBrushForColor(ticket.Color)
        };

        slot.Content = display;

        SetClearButtonVisibility(slotNumber, true);
        UpdateShowOnTVButtonState();
    }

    private static IBrush GetBrushForColor(string? colorName)
    {
        return colorName?.ToLowerInvariant() switch
        {
            "grey" => Brushes.Gray,
            "blue" => Brushes.SteelBlue,
            "orange" => Brushes.Orange,
            "yellow" => Brushes.Gold,
            "tangerine" => Brushes.OrangeRed,
            "pink" => Brushes.HotPink,
            "green" => Brushes.SeaGreen,
            _ => Brushes.LightGray
        };
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ShowOnTV_Click(object? sender, RoutedEventArgs e)
    {
        string piIp = ipAddress;
        const int port = 5000;

        bool singleTicketPrize =
            LargeMeatPrizeButton.IsChecked == true;

        if (TicketSlot1.Content is not TicketDisplay ||
            (!singleTicketPrize &&
             TicketSlot2.Content is not TicketDisplay))
        {
            await ShowMessageAsync(
                singleTicketPrize
                    ? "Please create a ticket before sending to TV."
                    : "Please create tickets in both slots before sending to TV.");

            return;
        }

        int ticketCount = singleTicketPrize ? 1 : 2;

        for (int i = 0; i < ticketCount; i++)
        {
            ContentControl slot =
                i == 0 ? TicketSlot1 : TicketSlot2;

            TicketInfo? ticket =
                GetTicketInfoFromSlot(slot, i + 1);

            if (ticket == null)
                continue;

            try
            {
                await PiMessageClient.SendMessageAsync(
                    piIp,
                    port,
                    ticket);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(
                    $"Error sending message: {ex.Message}");

                return;
            }
        }

        Debug.WriteLine("Showing on TV");
    }

    private static TicketInfo? GetTicketInfoFromSlot(
        ContentControl slot,
        int slotNumber)
    {
        if (slot.Content is not TicketDisplay ticketDisplay)
            return null;

        return new TicketInfo
        {
            Number = ticketDisplay.RaffleNumber,
            Letter = ticketDisplay.Letter,
            Color = ticketDisplay.ColorNameText.Text,
            SlotNumber = slotNumber
        };
    }

    private void ClearTicketSlots_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot1);
        ClearTicketSlot(TicketSlot2);
        UpdateShowOnTVButtonState();
    }

    private void ClearTicketSlot(ContentControl slot)
    {
        if (slot.Tag is not int slotNumber)
            return;

        slot.Content = null;
        SetCreateTicketButton(slot, slotNumber);
        SetClearButtonVisibility(slotNumber, false);
    }

    private async void ClearScreenOnTV_Click(
        object? sender,
        RoutedEventArgs e)
    {
        string piIp = ipAddress;
        const int port = 5000;

        var clearCommand = new TicketInfo
        {
            Number = null,
            Letter = null,
            Color = null,
            SlotNumber = 0
        };

        try
        {
            await PiMessageClient.SendMessageAsync(
                piIp,
                port,
                clearCommand);

            await ShowMessageAsync(
                "Clear screen command sent.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Error sending clear command: {ex.Message}");
        }
    }

    private void PrizeButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BeerPrizeButton.IsChecked = false;
        GiftCardPrizeButton.IsChecked = false;
        SmallMeatPrizeButton.IsChecked = false;
        LargeMeatPrizeButton.IsChecked = false;

        if (sender is not ToggleButton selectedButton)
            return;

        selectedButton.IsChecked = true;
        CurrentPrizeValue.Text =
            selectedButton.Content?.ToString() ?? string.Empty;

        if (LargeMeatPrizeButton.IsChecked == true)
        {
            TicketSlot1Panel.IsVisible = true;
            TicketSlot2Panel.IsVisible = false;

            TicketSlotsGrid.ColumnDefinitions[0].Width =
                new GridLength(1, GridUnitType.Star);

            TicketSlotsGrid.ColumnDefinitions[1].Width =
                new GridLength(0);

            TicketSlot1Panel.HorizontalAlignment =
                HorizontalAlignment.Center;
        }
        else
        {
            TicketSlot1Panel.IsVisible = true;
            TicketSlot2Panel.IsVisible = true;

            TicketSlotsGrid.ColumnDefinitions[0].Width =
                new GridLength(1, GridUnitType.Star);

            TicketSlotsGrid.ColumnDefinitions[1].Width =
                new GridLength(1, GridUnitType.Star);

            TicketSlot1Panel.HorizontalAlignment =
                HorizontalAlignment.Stretch;

            if (TicketSlot2.Content is not TicketDisplay)
            {
                SetCreateTicketButton(TicketSlot2, 2);
            }
        }

        UpdateShowOnTVButtonState();
    }

    private async void TicketSlotBorder_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        if (!isRedrawMode)
            return;

        if (sender is not Border border ||
            border.Tag == null ||
            !int.TryParse(border.Tag.ToString(), out int slotNumber))
        {
            return;
        }

        ContentControl slot =
            slotNumber == 1 ? TicketSlot1 : TicketSlot2;

        await CreateTicketAsync(slot, slotNumber);

        RedrawToggleButton.IsChecked = false;
    }

    private void UpdateShowOnTVButtonState()
    {
        bool prizeSelected =
            BeerPrizeButton.IsChecked == true ||
            GiftCardPrizeButton.IsChecked == true ||
            SmallMeatPrizeButton.IsChecked == true ||
            LargeMeatPrizeButton.IsChecked == true;

        if (LargeMeatPrizeButton.IsChecked == true)
        {
            ShowOnTVButton.IsEnabled =
                TicketSlot1.Content is TicketDisplay &&
                prizeSelected;
        }
        else
        {
            ShowOnTVButton.IsEnabled =
                TicketSlot1.Content is TicketDisplay &&
                TicketSlot2.Content is TicketDisplay &&
                prizeSelected;
        }
    }

    private void ClearTicketSlot1_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot1);
        UpdateShowOnTVButtonState();
    }

    private void ClearTicketSlot2_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot2);
        UpdateShowOnTVButtonState();
    }

    private void SetClearButtonVisibility(
        int slotNumber,
        bool isVisible)
    {
        if (slotNumber == 1)
        {
            ClearTicketSlot1Button.IsVisible = isVisible;
        }
        else if (slotNumber == 2)
        {
            ClearTicketSlot2Button.IsVisible = isVisible;
        }
    }

    private async Task ShowMessageAsync(string message)
    {
        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        okButton.Classes.Add("flat");

        var dialog = new Window
        {
            Title = "Raffle Display",
            Width = 420,
            MinHeight = 180,
            CanResize = false,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(
                Color.Parse("#182544")),
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 22,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        FontSize = 17,
                        HorizontalAlignment =
                            HorizontalAlignment.Center
                    },
                    okButton
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    public bool IsTicket1Present =>
        TicketSlot1.Content is TicketDisplay;

    public bool IsTicket2Present =>
        TicketSlot2.Content is TicketDisplay;
}
