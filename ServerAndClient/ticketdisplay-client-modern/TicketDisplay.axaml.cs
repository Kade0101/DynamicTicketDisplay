using Avalonia.Controls;
using Avalonia.Media;

namespace RaffleDisplayApplication;

public partial class TicketDisplay : UserControl
{
    public TicketDisplay()
    {
        InitializeComponent();
    }

    public string RaffleNumber
    {
        get => RaffleText.Text ?? string.Empty;
        set => RaffleText.Text = value;
    }

    public string Letter
    {
        get => LetterText.Text ?? string.Empty;
        set => LetterText.Text = value;
    }

    public string ColorName
    {
        get => ColorNameText.Text ?? string.Empty;
        set => ColorNameText.Text = value;
    }

    public IBrush? ColorStripBrush
    {
        get => ColorStrip.Background;
        set => ColorStrip.Background = value;
    }

    public IBrush? ColorNameBrush
    {
        get => ColorNameText.Foreground;
        set => ColorNameText.Foreground = value;
    }
}
