using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading;

namespace TicketDisplayAppModified.Views;

public partial class RedrawTicketTemplate : UserControl
{
    private DispatcherTimer? _ellipsisTimer;
    private int _dotCount = 0;
    private static readonly string[] _ellipsisStates = { "", ".", "..", "..." };
    private double scale = 2.0; // Default scale factor
    public RedrawTicketTemplate()
    {
        InitializeComponent();
        StartEllipsisAnimation();
        SetFontScale(scale);
    }

    private void StartEllipsisAnimation()
    {
        _ellipsisTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _ellipsisTimer.Tick += (s, e) =>
        {
            _dotCount = (_dotCount + 1) % _ellipsisStates.Length;
            if (this.FindControl<TextBlock>("EllipsisText") is { } ellipsisText)
            {
                ellipsisText.Text = _ellipsisStates[_dotCount];
            }
        };
        _ellipsisTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _ellipsisTimer?.Stop();
    }
    public void SetFontScale(double scale)
    {
        RedrawingText.FontSize = 24 * scale;
    }
}