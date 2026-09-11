using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RaffleDisplayApplication
{
    public partial class InputWindow : Window
    {
        public string TicketInfoString { get; private set; }
        public TicketInfo Ticket { get; private set; }
        public int SlotNumber { get; set; }

        public List<TicketInfo> TicketList { get; set; } = new List<TicketInfo>();
        public InputWindow()
        {
            InitializeComponent();
        }
        private void SaveTicket(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(LetterInput.Text) || string.IsNullOrWhiteSpace(RaffleInput.Text) || ColorInput.SelectedItem == null)
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }

            // Only allow one letter
            var letter = LetterInput.Text.Trim();
            if (letter.Length != 1 || !char.IsLetter(letter[0]))
            {
                MessageBox.Show("Please enter a single letter.");
                return;
            }

            // Parse and validate number
            if (!int.TryParse(RaffleInput.Text.Trim(), out int number))
            {
                MessageBox.Show("Please enter a valid number.");
                return;
            }
            if (number < 0 || number > 100)
            {
                MessageBox.Show("Number must be between 0 and 100.");
                return;
            }

            // Pad number with leading zero if less than 10
            string numberString = number < 10 ? $"0{number}" : number.ToString();

            // Create the ticket info object
            Ticket = new TicketInfo
            {
                Letter = letter.ToUpper(),
                Number = numberString,
                Color = (ColorInput.SelectedItem as ComboBoxItem)?.Content?.ToString(),
                SlotNumber = SlotNumber // Use the SlotNumber property
            };

            if (TicketList.Exists(t => t.SlotNumber == Ticket.SlotNumber))
            {
                MessageBox.Show($"Slot {Ticket.SlotNumber} already has a ticket.");
                return;
            }
            if (TicketList.Count >= 2)
                return;
            TicketList.Add(Ticket); // Add the ticket to the list
            // Create the ticket info string
            TicketInfoString = $"{Ticket.Letter} {Ticket.Number} {Ticket.Color}";
            Debug.WriteLine($"Ticket Info: {TicketInfoString}");
            DialogResult = true;
            Close();

        }
        

        private void ColorInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No logic needed unless you want to handle color changes
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public string GetTicketInfo()
        {
            return TicketInfoString;
        }

        public TicketInfo GetTicket()
        {
            return Ticket;
        }
    }

    public class TicketInfo
    {
        public string Letter { get; set; }
        public string Number { get; set; }
        public string Color { get; set; }
        public int SlotNumber { get; set; } // Added SlotNumber property
    }
}





