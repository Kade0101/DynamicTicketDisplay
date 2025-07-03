using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaffleDisplayApplication;

namespace RaffleDisplayApplication
{
    public partial class InputWindow : Window
    {
        public InputWindow()
        {
            InitializeComponent();
        }

        private async void ShowOnTV_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Clicked!");
            // Get values
            string raffle = RaffleInput.Text?.Trim();
            string letter = LetterInput.Text?.Trim();
            string color = (ColorInput.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim();

            TicketInfo ticket = new TicketInfo()
            {
                Number = raffle,
                Color = color,
                Letter = letter
            
            };


            // Check if any required field is empty
            if (string.IsNullOrEmpty(raffle) || string.IsNullOrEmpty(letter) || string.IsNullOrEmpty(color))
            {
                System.Windows.MessageBox.Show("Please fill in the raffle number, letter, and select a colour.", "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // All fields are filled — open display window

            string piIp = "172.16.0.71"; // Replace with your Pi's IP
            int port = 5000;
            //string message = "show";

            try
            {
                //MessageBox.Show("About to send message...");
                await PiMessageClient.SendMessageAsync(piIp, port, ticket);
                //MessageBox.Show("Message sent to Pi");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending message: {ex.Message}");
            }


            Debug.WriteLine("Showing on Tv");
            
        }


        private void ColorInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        }


    }
    public class TicketInfo
    {
        public string Letter { get; set; }
        public string Number { get; set; }
        public string Color { get; set; }
    }



