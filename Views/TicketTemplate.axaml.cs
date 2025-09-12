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
using Avalonia.Markup.Xaml;
using System.Linq; // added

namespace TicketDisplayAppModified.Views
{
    public partial class TicketTemplate : UserControl
    {
        private const double BaseWidth = 240.0; // Set this to your design's base ticket width

        private bool _hasAnimated = false;

        public MainWindow MainWindow { get; }

        public TicketTemplate(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
            InitializeComponent();

            this.AttachedToVisualTree += async (_, __) =>
            {
                if (RootGrid != null)
                {
                    RootGrid.Opacity = 1;
                }

                if (!_hasAnimated)
                {
                    _hasAnimated = true;
                    await PlayShowAnimationAsync();
                }
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

        // Small helper to add/update a transition on an animatable target
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
            if (this.FindControl<Grid>("RootGrid") is not Grid root)
                return;

            // Transform-only intro (no per-frame loops)
            var group = root.RenderTransform as TransformGroup ?? new TransformGroup();
            var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
            if (scale is null)
            {
                scale = new ScaleTransform(1, 1);
                group.Children.Add(scale);
            }
            root.RenderTransform = group;
            root.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

            int duration = 250;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Start smaller and transparent
                root.Opacity = 0;
                scale.ScaleX = 0.7;
                scale.ScaleY = 0.7;

                // Animate to full size/opacity via transitions (render-loop driven)
                EnsureTransition(root, Visual.OpacityProperty, duration, new SineEaseInOut());
                EnsureTransition(scale, ScaleTransform.ScaleXProperty, duration, new SineEaseInOut());
                EnsureTransition(scale, ScaleTransform.ScaleYProperty, duration, new SineEaseInOut());

                root.Opacity = 1;
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }, DispatcherPriority.Render);

            // Let the transition finish without blocking UI each frame
            await Task.Delay(duration + 16);

            // Apply size-dependent layout once (avoid per-frame churn)
            SetFontScale();
            SetColourStripScale((this.Width <= 0 ? 1 : this.Width / BaseWidth) + 1);
        }

        private double _lastColorNameFont = -1;
        private double _lastLetterFont = -1;
        private double _lastRaffleFont = -1;

        // Call this whenever the ticket's size changes
        public void SetFontScale()
        {
            // Calculate scale based on current width
            double scale = this.Width / BaseWidth;

            // Use base font sizes and scale them
            double colorNameFont = Math.Round(24 * scale, 1);   // Example base size: 24
            double letterFont = Math.Round(54 * scale, 1);      // Example base size: 54
            double raffleFont = Math.Round(62 * scale, 1);      // Example base size: 62

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
