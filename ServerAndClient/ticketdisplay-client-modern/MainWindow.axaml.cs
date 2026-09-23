using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace RaffleDisplayApplication;

public partial class MainWindow : Window
{
    private readonly string ipAddress = "127.0.0.1";
    private readonly List<ToggleButton> customPrizeButtons = new();
    private readonly Dictionary<int, ContentControl> ticketSlots = new();
    private readonly Dictionary<int, StackPanel> ticketSlotPanels = new();
    private readonly Dictionary<int, Button> clearTicketButtons = new();
    private readonly List<DrawnTicketGroup> previouslyDrawnGroups = new();
    private bool isEditingPrizeInstructions;
    private bool isRedrawMode;
    private bool isInitialized;
    private int requiredTicketCount = 2;
    private int timeBetweenTicketDraw = 5000;
    private bool IsTicketModeActive =>
        ModeTabControl?.SelectedItem == TicketModeTab;

    private sealed class DrawnTicketGroup
    {
        public string PrizeName { get; init; } = string.Empty;
        public List<TicketInfo> Tickets { get; init; } = new();
    }

    public MainWindow()
    {
        InitializeComponent();
        Opened += MainWindow_Opened;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        var screens = Screens.All;

        if (screens.Count >= 2)
        {
            var primaryScreen = Screens.Primary;
            Position = new PixelPoint(primaryScreen.Bounds.X, primaryScreen.Bounds.Y);
            WindowState = WindowState.Maximized;
        }

        if (isInitialized)
            return;

        RegisterTicketSlots();
        InitializeTicketSlots();
        UpdateVisibleTicketSlots();
        UpdateModeState();
        UpdatePrizeInstructionsEditState();
        RefreshPreviouslyDrawnDisplay();
        UpdateShowOnTVButtonState();
        isInitialized = true;
    }

    private void RegisterTicketSlots()
    {
        if (TicketSlot1 != null) ticketSlots[1] = TicketSlot1;
        if (TicketSlot2 != null) ticketSlots[2] = TicketSlot2;
        if (TicketSlot3 != null) ticketSlots[3] = TicketSlot3;
        if (TicketSlot4 != null) ticketSlots[4] = TicketSlot4;

        if (TicketSlot1Panel != null) ticketSlotPanels[1] = TicketSlot1Panel;
        if (TicketSlot2Panel != null) ticketSlotPanels[2] = TicketSlot2Panel;
        if (TicketSlot3Panel != null) ticketSlotPanels[3] = TicketSlot3Panel;
        if (TicketSlot4Panel != null) ticketSlotPanels[4] = TicketSlot4Panel;

        if (ClearTicketSlot1Button != null) clearTicketButtons[1] = ClearTicketSlot1Button;
        if (ClearTicketSlot2Button != null) clearTicketButtons[2] = ClearTicketSlot2Button;
        if (ClearTicketSlot3Button != null) clearTicketButtons[3] = ClearTicketSlot3Button;
        if (ClearTicketSlot4Button != null) clearTicketButtons[4] = ClearTicketSlot4Button;
    }

    private void InitializeTicketSlots()
    {
        foreach (var slot in ticketSlots)
        {
            SetCreateTicketButton(slot.Value, slot.Key);
        }
    }

    private void ModeTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized)
            return;

        UpdateModeState();
        UpdateShowOnTVButtonState();
    }

    private void TicketCountComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox &&
            comboBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out int count))
        {
            requiredTicketCount = Math.Clamp(count, 1, 4);
            UpdateVisibleTicketSlots();
            UpdateShowOnTVButtonState();
        }
    }

    private void UpdateVisibleTicketSlots()
    {
        for (int slotNumber = 1; slotNumber <= 4; slotNumber++)
        {
            if (ticketSlotPanels.TryGetValue(slotNumber, out var panel))
            {
                panel.IsVisible = slotNumber <= requiredTicketCount;
            }
        }
    }

    private void UpdateModeState()
    {
        bool isTicketMode = IsTicketModeActive;

        if (ShowOnTVButton != null)
            ShowOnTVButton.IsVisible = isTicketMode;

        if (ClearScreenButton != null)
            ClearScreenButton.IsVisible = isTicketMode;

        if (RedrawToggleButton != null)
            RedrawToggleButton.IsVisible = isTicketMode;

        if (!isTicketMode)
        {
            if (RedrawToggleButton != null)
                RedrawToggleButton.IsChecked = false;
            isRedrawMode = false;
        }
    }

    private void RedrawToggleButton_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsTicketModeActive)
        {
            if (RedrawToggleButton != null)
                RedrawToggleButton.IsChecked = false;

            isRedrawMode = false;
            return;
        }

        isRedrawMode = RedrawToggleButton?.IsChecked == true;
    }

    private void SetCreateTicketButton(ContentControl slot, int slotNumber)
    {
        var button = new Button { Content = "Create Ticket" };
        button.Classes.Add("createTicket");
        button.Click += async (_, _) => await TicketSlot_ClickAsync(slot, slotNumber);

        slot.Content = button;
        slot.Tag = slotNumber;
        SetClearButtonVisibility(slotNumber, false);
    }

    private async Task TicketSlot_ClickAsync(ContentControl slot, int slotNumber)
    {
        if (!IsTicketModeActive || slotNumber > requiredTicketCount)
            return;

        if (isRedrawMode)
        {
            await RedrawTicketAsync(slot, slotNumber);
            if (RedrawToggleButton != null)
                RedrawToggleButton.IsChecked = false;
            return;
        }

        await CreateTicketAsync(slot, slotNumber);
    }

    private Task RedrawTicketAsync(ContentControl slot, int slotNumber)
    {
        return CreateTicketAsync(slot, slotNumber);
    }

    private async Task CreateTicketAsync(ContentControl slot, int slotNumber)
    {
        if (!IsTicketModeActive || slotNumber > requiredTicketCount)
            return;

        var inputWindow = new InputWindow
        {
            SlotNumber = slotNumber
        };

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
        if (!IsTicketModeActive)
            return;

        for (int i = 1; i <= requiredTicketCount; i++)
        {
            var slot = GetTicketSlot(i);
            if (slot == null || slot.Content is not TicketDisplay)
            {
                await ShowMessageAsync($"Please create tickets in all {requiredTicketCount} required slots before sending to TV.");
                return;
            }
        }
        await PiMessageClient.SendMessagePrizeAsync(ipAddress, 5000, CurrentPrizeValue.Text); //Sets prize text once
        
        for (int i = 1; i <= requiredTicketCount; i++)
        {
            var slot = GetTicketSlot(i);
            if (slot == null)
                continue;

            var ticket = GetTicketInfoFromSlot(slot, i);
            if (ticket == null)
                continue;

            try
            {
                await PiMessageClient.SendMessageAsync(ipAddress, 5000, ticket);
                await Task.Delay(timeBetweenTicketDraw);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync($"Error sending message: {ex.Message}");
                return;
            }
        }

        Debug.WriteLine("Showing on TV");
    }

    private ContentControl? GetTicketSlot(int slotNumber)
    {
        ticketSlots.TryGetValue(slotNumber, out var slot);
        return slot;
    }

    private static TicketInfo? GetTicketInfoFromSlot(ContentControl slot, int slotNumber)
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

    private void ClearTicketSlots_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var slot in ticketSlots.Values)
        {
            ClearTicketSlot(slot);
        }

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
    private async void ConfirmDraw_Click(object? sender, RoutedEventArgs e)
    {
        var confirmedTickets = new List<TicketInfo>();

        foreach (var slot in ticketSlots.Values)
        {
            if (slot.Tag is int slotNumber)
            {
                var ticket = GetTicketInfoFromSlot(slot, slotNumber);
                if (ticket != null)
                {
                    confirmedTickets.Add(ticket);
                }
            }

            ClearTicketSlot(slot);
        }
        try
        {
            await PiMessageClient.SendConfirmMessageAsync(ipAddress, 5000);
            await PiMessageClient.SendClearMessageAsync(ipAddress, 5000);
            await ShowMessageAsync("Confirm and clear commands sent.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"Error sending confirm/clear command: {ex.Message}");
        }

        if (confirmedTickets.Count > 0)
        {
            previouslyDrawnGroups.Add(new DrawnTicketGroup
            {
                PrizeName = string.IsNullOrWhiteSpace(CurrentPrizeValue.Text)
                    ? "Unspecified Prize"
                    : CurrentPrizeValue.Text ?? "Unspecified Prize",
                Tickets = confirmedTickets
            });
        }

        RefreshPreviouslyDrawnDisplay();
        UpdateShowOnTVButtonState();
    }

    private async void ConfirmPrizeInstructionsButton_Click(object? sender, RoutedEventArgs e)
    {
        var instructions = PrizeInstructionsTextBox.Text ?? string.Empty;
        try
        {
            await PiMessageClient.SendInstructionsAsync(ipAddress, 5000, instructions);
            isEditingPrizeInstructions = false;
            UpdatePrizeInstructionsEditState();
            EditPrizeInstructionsButton?.Focus();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"Error sending instructions command: {ex.Message}");
        }
    }
    private async void ClearScreenOnTV_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsTicketModeActive)
            return;

        try
        {
            await PiMessageClient.SendClearMessageAsync(ipAddress, 5000);
            await ShowMessageAsync("Clear screen command sent.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync($"Error sending clear command: {ex.Message}");
        }
    }

    private void PrizeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsTicketModeActive)
            return;

        if (BeerPrizeButton != null)
            BeerPrizeButton.IsChecked = false;

        if (GiftCardPrizeButton != null)
            GiftCardPrizeButton.IsChecked = false;

        if (SmallMeatPrizeButton != null)
            SmallMeatPrizeButton.IsChecked = false;

        if (LargeMeatPrizeButton != null)
            LargeMeatPrizeButton.IsChecked = false;

        foreach (var customPrizeButton in customPrizeButtons)
        {
            customPrizeButton.IsChecked = false;
        }

        if (sender is not ToggleButton selectedButton)
            return;

        selectedButton.IsChecked = true;
        CurrentPrizeValue.Text = selectedButton.Content?.ToString() ?? string.Empty;

        if (LargeMeatPrizeButton != null && selectedButton == LargeMeatPrizeButton)
        {
            requiredTicketCount = 1;

            if (TicketCountComboBox != null)
                TicketCountComboBox.SelectedIndex = 0;
        }

        UpdateVisibleTicketSlots();
        UpdateShowOnTVButtonState();
    }

    private async void AddPrizeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsTicketModeActive)
            return;

        var prizeName = await PromptForPrizeNameAsync();
        if (string.IsNullOrWhiteSpace(prizeName))
            return;

        AddCustomPrizeButton(prizeName.Trim());
    }

    private void AddCustomPrizeButton(string prizeName)
    {
        if (customPrizeButtons.Any(button =>
                string.Equals(button.Content?.ToString(), prizeName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (CustomPrizePanel == null)
            return;

        var button = new ToggleButton
        {
            Content = prizeName,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 58
        };

        button.Classes.Add("flatToggle");
        button.Click += PrizeButton_Click;

        customPrizeButtons.Add(button);
        CustomPrizePanel.Children.Add(button);
    }

    private async Task<string?> PromptForPrizeNameAsync()
    {
        var input = new TextBox
        {
            Width = 280,
            PlaceholderText = "Enter prize name"
        };

        var saveButton = CreateDialogButton("Add");
        var cancelButton = CreateDialogButton("Cancel");

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12,
            Children = { saveButton, cancelButton }
        };

        var dialog = new Window
        {
            Title = "Add Prize",
            Width = 420,
            MinHeight = 250,
            MaxHeight = 250,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#FEFBF3")),
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 18,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Enter a new prize option",
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        Foreground = new SolidColorBrush(Color.Parse("#79B4B7")),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    input,
                    buttonPanel
                }
            }
        };

        dialog.Styles.Add(CreateDialogButtonStyle("#357AB8", "#2F6EA6", "#285A99"));

        string? result = null;
        saveButton.Click += (_, _) => { result = input.Text; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
    }

    private static Style CreateDialogButtonStyle(string normalColor, string hoverColor, string pressedColor)
    {
        var normalBrush = new SolidColorBrush(Color.Parse(normalColor));
        var hoverBrush = new SolidColorBrush(Color.Parse(hoverColor));
        var pressedBrush = new SolidColorBrush(Color.Parse(pressedColor));

        var baseStyle = new Style(x => x.OfType<Button>().Class("dialogButton"));
        baseStyle.Setters.Add(new Setter(Button.BackgroundProperty, normalBrush));
        baseStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
        baseStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));

        var presenterStyle = new Style(x => x.OfType<Button>().Class("dialogButton").Template().OfType<ContentPresenter>());
        presenterStyle.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, normalBrush));
        presenterStyle.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, Brushes.White));

        var hoverStyle = new Style(x => x.OfType<Button>().Class("dialogButton").Class(":pointerover"));
        hoverStyle.Setters.Add(new Setter(Button.BackgroundProperty, hoverBrush));
        hoverStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));

        var hoverPresenterStyle = new Style(x => x.OfType<Button>().Class("dialogButton").Class(":pointerover").Template().OfType<ContentPresenter>());
        hoverPresenterStyle.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, hoverBrush));
        hoverPresenterStyle.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, Brushes.White));

        var pressedStyle = new Style(x => x.OfType<Button>().Class("dialogButton").Class(":pressed"));
        pressedStyle.Setters.Add(new Setter(Button.BackgroundProperty, pressedBrush));
        pressedStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));

        var pressedPresenterStyle = new Style(x => x.OfType<Button>().Class("dialogButton").Class(":pressed").Template().OfType<ContentPresenter>());
        pressedPresenterStyle.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, pressedBrush));
        pressedPresenterStyle.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, Brushes.White));

        var root = new Style();
        root.Children.Add(baseStyle);
        root.Children.Add(presenterStyle);
        root.Children.Add(hoverStyle);
        root.Children.Add(hoverPresenterStyle);
        root.Children.Add(pressedStyle);
        root.Children.Add(pressedPresenterStyle);
        return root;
    }

    private static Button CreateDialogButton(string content)
    {
        var normalBrush = new SolidColorBrush(Color.Parse("#357AB8"));
        var hoverBrush = new SolidColorBrush(Color.Parse("#2F6EA6"));
        var pressedBrush = new SolidColorBrush(Color.Parse("#285A99"));

        var button = new Button
        {
            Content = content,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = normalBrush,
            Foreground = Brushes.White,
            BorderBrush = normalBrush,
            BorderThickness = new Thickness(0),
            Transitions = new Transitions()
        };

        button.Classes.Add("dialogButton");

        button.PointerEntered += (_, _) =>
        {
            button.Background = hoverBrush;
            button.BorderBrush = hoverBrush;
            button.Foreground = Brushes.White;
        };

        button.PointerExited += (_, _) =>
        {
            button.Background = normalBrush;
            button.BorderBrush = normalBrush;
            button.Foreground = Brushes.White;
        };

        button.PointerPressed += (_, _) =>
        {
            button.Background = pressedBrush;
            button.BorderBrush = pressedBrush;
            button.Foreground = Brushes.White;
        };

        button.PointerReleased += (_, _) =>
        {
            button.Background = hoverBrush;
            button.BorderBrush = hoverBrush;
            button.Foreground = Brushes.White;
        };

        return button;
    }

    private async void TicketSlotBorder_Tapped(object? sender, TappedEventArgs e)
    {
        if (!isRedrawMode || !IsTicketModeActive)
            return;

        if (sender is not Border border ||
            border.Tag == null ||
            !int.TryParse(border.Tag.ToString(), out int slotNumber))
        {
            return;
        }

        var slot = GetTicketSlot(slotNumber);
        if (slot == null)
            return;

        await CreateTicketAsync(slot, slotNumber);
        if (RedrawToggleButton != null)
            RedrawToggleButton.IsChecked = false;
    }

    private void UpdateShowOnTVButtonState()
    {
        if (!IsTicketModeActive)
        {
            if (ShowOnTVButton != null)
                ShowOnTVButton.IsEnabled = false;
            return;
        }

        bool prizeSelected =
            (BeerPrizeButton?.IsChecked == true) ||
            (GiftCardPrizeButton?.IsChecked == true) ||
            (SmallMeatPrizeButton?.IsChecked == true) ||
            (LargeMeatPrizeButton?.IsChecked == true) ||
            customPrizeButtons.Any(button => button.IsChecked == true);

        bool allRequiredTicketsPresent = Enumerable
            .Range(1, requiredTicketCount)
            .All(slotNumber => GetTicketSlot(slotNumber)?.Content is TicketDisplay);

        if (ShowOnTVButton != null)
            ShowOnTVButton.IsEnabled = prizeSelected && allRequiredTicketsPresent;
    }

    private void ClearTicketSlot1_Click(object? sender, RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot1);
        UpdateShowOnTVButtonState();
    }

    private void ClearTicketSlot2_Click(object? sender, RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot2);
        UpdateShowOnTVButtonState();
    }

    private void ClearTicketSlot3_Click(object? sender, RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot3);
        UpdateShowOnTVButtonState();
    }

    private void ClearTicketSlot4_Click(object? sender, RoutedEventArgs e)
    {
        ClearTicketSlot(TicketSlot4);
        UpdateShowOnTVButtonState();
    }

    private void SetClearButtonVisibility(int slotNumber, bool isVisible)
    {
        if (clearTicketButtons.TryGetValue(slotNumber, out var button))
        {
            button.IsVisible = isVisible;
        }
    }

    private void RefreshPreviouslyDrawnDisplay()
    {
        if (PreviouslyDrawnPanel == null)
            return;

        PreviouslyDrawnPanel.Children.Clear();

        if (previouslyDrawnGroups.Count == 0)
        {
            PreviouslyDrawnPanel.Children.Add(new TextBlock
            {
                Text = "No confirmed tickets yet.",
                Foreground = new SolidColorBrush(Color.Parse("#6B7280")),
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap
            });

            return;
        }

        foreach (var group in previouslyDrawnGroups.AsEnumerable().Reverse())
        {
            var ticketWrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            foreach (var ticket in group.Tickets)
            {
                var ticketBrush = GetBrushForColor(ticket.Color);
                var ticketLetter = string.IsNullOrWhiteSpace(ticket.Letter)
                    ? "?"
                    : ticket.Letter!.Trim().ToUpperInvariant();
                var ticketNumber = string.IsNullOrWhiteSpace(ticket.Number)
                    ? "--"
                    : ticket.Number!.Trim();
                var ticketLabel = $"{ticketLetter}{ticketNumber}";
                var colorLabel = ticket.Color ?? string.Empty;

                var colorBadge = new Border
                {
                    Background = ticketBrush,
                    CornerRadius = new CornerRadius(999),
                    Padding = new Thickness(8, 3),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = colorLabel,
                        FontSize = 10,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brushes.White
                    }
                };

                var ticketContentPanel = new StackPanel
                {
                    Spacing = 4,
                    Margin = new Thickness(12, 10, 12, 10),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "DRAWN",
                            FontWeight = FontWeight.SemiBold,
                            FontSize = 9,
                            Foreground = new SolidColorBrush(Color.Parse("#7A7F87")),
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = ticketLabel,
                            FontWeight = FontWeight.Bold,
                            FontSize = 24,
                            Foreground = new SolidColorBrush(Color.Parse("#101B2D")),
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        colorBadge
                    }
                };

                Grid.SetColumn(ticketContentPanel, 1);

                ticketWrapPanel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#FFFDF8")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#D9CFBE")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    BoxShadow = BoxShadows.Parse("0 4 10 0 #12000000"),
                    ClipToBounds = true,
                    Margin = new Thickness(0, 0, 8, 8),
                    Width = 132,
                    MinHeight = 90,
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("8,*"),
                        Children =
                        {
                            new Border
                            {
                                CornerRadius = new CornerRadius(16, 0, 0, 16),
                                Background = ticketBrush,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                VerticalAlignment = VerticalAlignment.Stretch
                            },
                            ticketContentPanel
                        }
                    }
                });
            }

            PreviouslyDrawnPanel.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#D8CCB6")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = group.PrizeName,
                            FontSize = 17,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#101B2D"))
                        },
                        ticketWrapPanel
                    }
                }
            });
        }
    }
    private void EditPrizeInstructionsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (isEditingPrizeInstructions)
            return;

        isEditingPrizeInstructions = true;
        UpdatePrizeInstructionsEditState();

        PrizeInstructionsTextBox.Focus();
        PrizeInstructionsTextBox.CaretIndex = PrizeInstructionsTextBox.Text?.Length ?? 0;
    }

    private void UpdatePrizeInstructionsEditState()
    {
        if (PrizeInstructionsTextBox != null)
        {
            PrizeInstructionsTextBox.IsReadOnly = !isEditingPrizeInstructions;
            PrizeInstructionsTextBox.IsHitTestVisible = isEditingPrizeInstructions;
            PrizeInstructionsTextBox.Focusable = isEditingPrizeInstructions;
            PrizeInstructionsTextBox.SelectionBrush = new SolidColorBrush(Color.Parse("#357AB8"));
            PrizeInstructionsTextBox.SelectionForegroundBrush = Brushes.White;
            PrizeInstructionsTextBox.CaretBrush = new SolidColorBrush(Color.Parse("#101B2D"));
            PrizeInstructionsTextBox.Background = Brushes.White;
            PrizeInstructionsTextBox.Foreground = new SolidColorBrush(Color.Parse("#101B2D"));
            PrizeInstructionsTextBox.BorderBrush = new SolidColorBrush(Color.Parse(isEditingPrizeInstructions ? "#8FA0B2" : "#C8D2DD"));
        }

        if (EditPrizeInstructionsButton != null)
        {
            EditPrizeInstructionsButton.Content = "Edit";
            EditPrizeInstructionsButton.IsEnabled = !isEditingPrizeInstructions;
        }

        if (ConfirmPrizeInstructionsButton != null)
        {
            ConfirmPrizeInstructionsButton.IsEnabled = isEditingPrizeInstructions;
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
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#182544")),
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
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    okButton
                }
            }
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    public bool IsTicket1Present => TicketSlot1.Content is TicketDisplay;
    public bool IsTicket2Present => TicketSlot2.Content is TicketDisplay;
}
