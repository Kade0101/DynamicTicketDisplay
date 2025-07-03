using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using static TicketDisplayAppModified.Views.MainWindow;

namespace TicketDisplayAppModified.Views
{
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        public void UpdateMessage(string message)
        {
            // For example, update the RaffleText TextBlock
            Dispatcher.UIThread.Post(() =>
            {
                RaffleText.Text = message;
            });
        }
        public void UpdateTicket(TicketInfo ticket)
        {
            // Example: Update UI elements with ticket data
            LetterText.Text = ticket.Letter;
            RaffleText.Text = ticket.Number;
            ColorStrip.BorderBrush = new SolidColorBrush(Color.Parse(ticket.Color));

            // Show the view if it's hidden
            this.IsVisible = true;
        }

    }
}
