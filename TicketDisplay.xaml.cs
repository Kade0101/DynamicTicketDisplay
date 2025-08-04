using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RaffleDisplayApplication
{
    public partial class TicketDisplay : UserControl
    {
        public TicketDisplay()
        {
            InitializeComponent();
        }

        public string RaffleNumber
        {
            get => RaffleText.Text;
            set => RaffleText.Text = value;
        }

        public string Letter
        {
            get => LetterText.Text;
            set => LetterText.Text = value;
        }

        public string ColorName
        {
            get => ColorNameText.Text;
            set => ColorNameText.Text = value;
        }

        public Brush ColorStripBrush
        {
            get => ColorStrip.Background;
            set => ColorStrip.Background = value;
        }

        public Brush ColorNameBrush
        {
            get => ColorNameText.Foreground;
            set => ColorNameText.Foreground = value;
        }
    }
}
