using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RaffleDisplayApplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetCreateTicketButton(TicketSlot1, 1);
            SetCreateTicketButton(TicketSlot2, 2);
            UpdateShowOnTVButtonState();
        }

        private bool isRedrawMode = false;

        private void RedrawToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            isRedrawMode = true;
        }

        private void RedrawToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            isRedrawMode = false;
        }

        // Modify SetCreateTicketButton to use a new handler
        private void SetCreateTicketButton(ContentControl slot, int slotNumber)
        {
            var btn = new Button
            {
                Content = "Create Ticket",
                Style = (Style)FindResource("CreateTicketButtonStyle")
            };
            btn.Click += (s, e) => TicketSlot_Click(slot, slotNumber);
            slot.Content = btn;
            slot.Tag = slotNumber;
        }

        private void TicketSlot_Click(ContentControl slot, int slotNumber)
        {
            if (isRedrawMode)
            {
                // Redraw logic here
                RedrawTicket(slot, slotNumber);
                RedrawToggleButton.IsChecked = false; // Exit redraw mode after one use
                return;
            }
            CreateTicket(slot, slotNumber);
        }

        private void RedrawTicket(ContentControl slot, int slotNumber)
        {
            // You can reuse your CreateTicket logic, or show a different dialog if needed
            CreateTicket(slot, slotNumber);
        }

        private void CreateTicket(ContentControl slot, int slotNumber)
        {
            var inputWindow = new InputWindow();
            inputWindow.SlotNumber = slotNumber;

            if (inputWindow.ShowDialog() == true)
            {
                var ticket = inputWindow.GetTicket();
                if (ticket != null)
                {
                    var display = new TicketDisplay
                    {
                        RaffleNumber = ticket.Number,
                        Letter = ticket.Letter.ToUpper(),
                        ColorName = ticket.Color,
                    };

                    display.ColorNameBrush = GetBrushForColor(ticket.Color);
                    display.ColorStripBrush = GetBrushForColor(ticket.Color);

                    slot.Content = display;
                }
            }
            UpdateShowOnTVButtonState();
        }

        // Helper method to map color names to brushes
        private Brush GetBrushForColor(string colorName)
        {
            switch (colorName?.ToLower())
            {
                case "grey": return Brushes.Gray;
                case "blue": return Brushes.SteelBlue;
                case "orange": return Brushes.Orange;
                case "yellow": return Brushes.Gold;
                case "tangerine": return Brushes.OrangeRed;
                case "pink": return Brushes.HotPink;
                case "green": return Brushes.SeaGreen;
                default: return Brushes.LightGray;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private async void ShowOnTV_Click(object sender, RoutedEventArgs e)
        {
            string piIp = "192.168.1.80"; // Replace with your Pi's IP
            int port = 5000;

            if (TicketSlot1.Content == null || TicketSlot2.Content == null)
            {
                MessageBox.Show("Please create tickets in both slots before sending to TV.");
                return;
            }

            var ticketSlot1 = TicketSlot1.Content as TicketDisplay;
            var ticketSlot2 = TicketSlot2.Content as TicketDisplay;
            for (int i = 0; i < 2; i++)
            {
                TicketInfo Ticket = GetTicketInfoFromSlot(i == 0 ? TicketSlot1 : TicketSlot2, i + 1);
                try
                {
                    await PiMessageClient.SendMessageAsync(piIp, port, Ticket);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error sending message: {ex.Message}");
                }

                Debug.WriteLine("Showing on TV");
                
            }
        }
        private TicketInfo GetTicketInfoFromSlot(ContentControl slot, int slotNumber)
        {
            var ticketDisplay = slot.Content as TicketDisplay;
            if (ticketDisplay == null)
                return null;

            return new TicketInfo
            {
                Number = ticketDisplay.RaffleNumber,
                Letter = ticketDisplay.Letter,
                Color = ticketDisplay.ColorNameText.Text,
                SlotNumber = slotNumber
            };
        }

        private void ClearTicketSlots_Click(object sender, RoutedEventArgs e)
        {
            ClearTicketSlot(TicketSlot1);
            ClearTicketSlot(TicketSlot2);
        }

        private void ClearTicketSlot(ContentControl slot)
        {
            // Reset the content of the slot to null
            slot.Content = null;

            // Reapply the "Create Ticket" button
            var slotNumber = (int)slot.Tag;
            SetCreateTicketButton(slot, slotNumber);
        }

        private async void ClearScreenOnTV_Click(object sender, RoutedEventArgs e)
        {
            string piIp = "192.168.1.80"; // Use your Pi's IP
            int port = 5000;

            // Option 1: If your protocol supports a special TicketInfo for clearing
            var clearCommand = new TicketInfo
            {
                Number = null,
                Letter = null,
                Color = null,
                SlotNumber = 0 // Or use a special value if needed
            };

            try
            {
                await PiMessageClient.SendMessageAsync(piIp, port, clearCommand);
                MessageBox.Show("Clear screen command sent.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending clear command: {ex.Message}");
            }
        }

        private void PrizeButton_Click(object sender, RoutedEventArgs e)
        {
            BeerPrizeButton.IsChecked = false;
            GiftCardPrizeButton.IsChecked = false;
            SmallMeatPrizeButton.IsChecked = false;
            LargeMeatPrizeButton.IsChecked = false;
            ((ToggleButton)sender).IsChecked = true;
            CurrentPrizeValue.Text = ((ToggleButton)sender).Content.ToString();
            if (LargeMeatPrizeButton.IsChecked == true)
            {
                TicketSlot1Panel.Visibility = Visibility.Visible;
                TicketSlot2Panel.Visibility = Visibility.Collapsed;

                // Make first column fill all space, second column zero width
                TicketSlotsGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                TicketSlotsGrid.ColumnDefinitions[1].Width = new GridLength(0);
                // Center the panel
                TicketSlot1Panel.HorizontalAlignment = HorizontalAlignment.Center;
            }
            else
            {
                TicketSlot1Panel.Visibility = Visibility.Visible;
                TicketSlot2Panel.Visibility = Visibility.Visible;

                // Restore equal columns
                TicketSlotsGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                TicketSlotsGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                TicketSlot1Panel.HorizontalAlignment = HorizontalAlignment.Stretch;

                if (TicketSlot2.Content == null)
                {
                    SetCreateTicketButton(TicketSlot2, 2);
                }
            }
            UpdateShowOnTVButtonState();
        }

        private void TicketSlotBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isRedrawMode)
                return;

            var border = sender as Border;
            if (border?.Tag == null)
                return;

            int slotNumber = int.Parse(border.Tag.ToString());
            ContentControl slot = slotNumber == 1 ? TicketSlot1 : TicketSlot2;

            // Redraw logic (reuse your CreateTicket logic or a custom one)
            CreateTicket(slot, slotNumber);

            // Optionally, exit redraw mode after one use
            RedrawToggleButton.IsChecked = false;
        }

        private void UpdateShowOnTVButtonState()
        {

            ShowOnTVButton.IsEnabled = false;
            // Check if a prize is selected
            bool prizeSelected = BeerPrizeButton.IsChecked == true
                || GiftCardPrizeButton.IsChecked == true
                || SmallMeatPrizeButton.IsChecked == true
                || LargeMeatPrizeButton.IsChecked == true;

            // If LargeMeatPrizeButton is selected, only TicketSlot1 must be filled
            if (LargeMeatPrizeButton.IsChecked == true)
            {
                ShowOnTVButton.IsEnabled = TicketSlot1.Content is TicketDisplay && prizeSelected;
            }
            else
            {
                ShowOnTVButton.IsEnabled = TicketSlot1.Content is TicketDisplay
                    && TicketSlot2.Content is TicketDisplay
                    && prizeSelected;
            }
        }

        private void ClearTicketSlot1_Click(object sender, RoutedEventArgs e)
        {
            ClearTicketSlot(TicketSlot1);
            UpdateShowOnTVButtonState();
        }

        private void ClearTicketSlot2_Click(object sender, RoutedEventArgs e)
        {
            ClearTicketSlot(TicketSlot2);
            UpdateShowOnTVButtonState();
        }

        public bool IsTicket1Present => TicketSlot1.Content is TicketDisplay;

        public bool IsTicket2Present => TicketSlot2.Content is TicketDisplay;
    }
}
