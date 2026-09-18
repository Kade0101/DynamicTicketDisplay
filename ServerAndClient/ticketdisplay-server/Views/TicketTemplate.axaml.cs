using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using System;
using Avalonia;
using Avalonia.Styling;
using System.Threading.Tasks;
using System.Linq;

namespace TicketDisplayAppModified.Views
{
    public partial class TicketTemplate : UserControl
    {
        private const double BaseWidth = 240.0;

        public MainWindow? MainWindow { get; }

        public TicketTemplate()
        {
            InitializeComponent();
        }

        public TicketTemplate(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            InitializeComponent();

            AttachedToVisualTree += (_, __) =>
            {
                if (RootGrid != null)
                    RootGrid.Opacity = 1;

                ApplyScale(this.Width <= 0 ? 1 : this.Width / BaseWidth);
            };
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

            IsVisible = true;
        }

        private static Color MapColorNameToColor(string? colorName)
        {
            return (colorName ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "grey" => Colors.Gray,
                "blue" => Color.Parse("#4682B4"),
                "orange" => Colors.Orange,
                "yellow" => Color.Parse("#FFD700"),
                "tangerine" => Color.Parse("#FF4500"),
                "pink" => Color.Parse("#FF69B4"),
                "green" => Color.Parse("#2E8B57"),
                _ => Colors.LightGray
            };
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

        public async Task PlayShowAnimationAsync()
        {
            if (RootGrid is not Grid root)
                return;

            var group = root.RenderTransform as TransformGroup ?? new TransformGroup();
            var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            if (scale is null)
            {
                scale = new ScaleTransform(1, 1);
                group.Children.Add(scale);
            }

            root.RenderTransform = group;
            root.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            const int duration = 1000;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                root.Opacity = 0;
                scale.ScaleX = 0.4;
                scale.ScaleY = 0.4;

                EnsureTransition(root, Visual.OpacityProperty, duration, new SineEaseInOut());
                EnsureTransition(scale, ScaleTransform.ScaleXProperty, duration, new SineEaseInOut());
                EnsureTransition(scale, ScaleTransform.ScaleYProperty, duration, new SineEaseInOut());

                root.Opacity = 1;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }, DispatcherPriority.Render);

            await Task.Delay(duration + 16);
        }

        private double _lastColorNameFont = -1;
        private double _lastLetterFont = -1;
        private double _lastRaffleFont = -1;
        private double _lastStripWidth = -1;
        private double _lastBorderThickness = -1;

        public void ApplyScale(double scale)
        {
            SetFontScale(scale);
            SetColourStripScale(scale + 1);
        }

        public void SetFontScale()
        {
            SetFontScale(Width / BaseWidth);
        }

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
            if (RootGrid.Children[0] is not Grid innerGrid)
                return;

            double newWidth = Math.Round(100 * scale, 1);
            double borderThickness = Math.Round(2 * scale, 1);

            if (Math.Abs(newWidth - _lastStripWidth) > 0.05)
            {
                innerGrid.ColumnDefinitions[1].Width = new GridLength(newWidth, GridUnitType.Pixel);
                _lastStripWidth = newWidth;
            }

            if (Math.Abs(borderThickness - _lastBorderThickness) > 0.05)
            {
                OverlayBorder.BorderThickness = new Thickness(borderThickness);
                _lastBorderThickness = borderThickness;
            }
        }

        public RedrawTicketTemplate RedrawTicket()
        {
            Content = null;
            return new RedrawTicketTemplate();
        }
    }
}
