using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using System;
using Avalonia;
using Avalonia.Styling;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TicketDisplayAppModified.Views
{
    public partial class TicketTemplate : UserControl
    {
        private bool _hasAnimated = false;

        public MainWindow MainWindow { get; }

        public TicketTemplate(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            InitializeComponent();

            this.AttachedToVisualTree += async (_, __) =>
            {
                if (!_hasAnimated)
                {
                    _hasAnimated = true;
                    await PlayShowAnimationAsync();
                }
            };
            RootGrid.Opacity = 1;
        }

        public void UpdateMessage(string message)
        {
            Dispatcher.UIThread.Post(() =>
            {
                RaffleText.Text = message;
            });
        }

        public void UpdateTicket(MainWindow.TicketInfo ticket)
        {
            var letter = ticket.Letter ?? string.Empty;
            var number = ticket.Number ?? string.Empty;
            var color = ticket.Color ?? "grey";

            LetterText.Text = letter.ToUpperInvariant();
            RaffleText.Text = number;

            var mappedColor = MapColorNameToColor(color);
            ColorStrip.Background = new SolidColorBrush(mappedColor);

            ColorNameText.Text = color;
            ColorNameText.Foreground = new SolidColorBrush(mappedColor);

            this.IsVisible = true;
        }

        private static Avalonia.Media.Color MapColorNameToColor(string? colorName)
        {
            return (colorName ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "grey" => Avalonia.Media.Colors.Gray,
                "blue" => Avalonia.Media.Color.Parse("#4682B4"), // SteelBlue
                "orange" => Avalonia.Media.Colors.Orange,
                "yellow" => Avalonia.Media.Color.Parse("#FFD700"), // Gold
                "tangerine" => Avalonia.Media.Color.Parse("#FF4500"), // OrangeRed
                "pink" => Avalonia.Media.Color.Parse("#FF69B4"), // HotPink
                "green" => Avalonia.Media.Color.Parse("#2E8B57"), // SeaGreen
                _ => Avalonia.Media.Colors.LightGray
            };
        }

        public async Task PlayShowAnimationAsync()
        {
            if (this.FindControl<Grid>("RootGrid") is not Grid root)
                return;

            // Always assign a ScaleTransform if not present
            if (root.RenderTransform is not ScaleTransform scale)
            {
                scale = new ScaleTransform(0, 0); // Start at 0
                root.RenderTransform = scale;
                root.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }
            else
            {
                scale.ScaleX = scale.ScaleY = 0; // Reset to 0 if already present
            }

            var duration = 400;
            var steps = 90; // smoother

            // Set initial state
            root.Opacity = 0;
            scale.ScaleX = scale.ScaleY = 0; // Start at 0

            // Animate: simple ease (0 -> 1)
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double eased = new SineEaseInOut().Ease(t);
                root.Opacity = eased;
                scale.ScaleX = scale.ScaleY = eased;
                SetColourStripScale((scale.ScaleX + 2)); // Optional
                SetFontScale(scale.ScaleX + 1); // Optional
                await Task.Delay(duration / steps);
            }

            // Ensure final state
            root.Opacity = 1;
            scale.ScaleX = scale.ScaleY = 1;
        }

        private double _lastColorNameFont = -1;
        private double _lastLetterFont = -1;
        private double _lastRaffleFont = -1;

        public void SetFontScale(double scale)
        {
            double colorNameFont = Math.Round(24 * scale, 1);
            double letterFont = Math.Round(54 * scale, 1);
            double raffleFont = Math.Round(62 * scale, 1);

            if (Math.Abs(colorNameFont - _lastColorNameFont) > 0.05)
            {
                ColorNameText.FontSize = colorNameFont;
                _lastColorNameFont = colorNameFont;
            }
            if (Math.Abs(letterFont - _lastLetterFont) > 0.05)
            {
                LetterText.FontSize = letterFont;
                _lastLetterFont = letterFont;
            }
            if (Math.Abs(raffleFont - _lastRaffleFont) > 0.05)
            {
                RaffleText.FontSize = raffleFont;
                _lastRaffleFont = raffleFont;
            }
        }

        public void SetColourStripScale(double scale)
        {

            // Assuming your grid is named "RootGrid" and the inner grid is the first child
            var innerGrid = RootGrid.Children[0] as Grid;
            var newWidth = 40 * scale;
            if (innerGrid != null)
            {
                // Change the width of the color strip column (column 1)
                innerGrid.ColumnDefinitions[1].Width = new GridLength(newWidth, GridUnitType.Pixel);
            }
            OverlayBorder.BorderThickness = new Thickness(2 * scale);
        }
        public RedrawTicketTemplate RedrawTicket()
        {
            this.Content = null; // Clear the current content
            RedrawTicketTemplate ticketPlaceHolder = new RedrawTicketTemplate();
            return ticketPlaceHolder;
        }
    }
}
