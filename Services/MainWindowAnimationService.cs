using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TicketDisplayAppModified.Views;

// Handles all ticket / overlay animations for MainWindow.
internal sealed class MainWindowAnimationService
{
    public readonly MainWindow _window;

    public MainWindowAnimationService(MainWindow window) => _window = window;

    // Public entry point used by MainWindow.ShowMainView
    public async Task AnimateTicketToSlotAsync(TicketTemplate ticket, int slot)
    {
        await FadeOverlayAsync(0, 0.3, 100);

        RemoveTicketFromParent(ticket);

        var overlay = _window.OverlayCanvasRef;
        if (overlay is null)
            throw new InvalidOperationException("Overlay canvas not initialized.");

        ticket.Opacity = 0;
        overlay.Children.Add(ticket);

        // Ensure layout pass
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        var (initialWidth, initialHeight, initialX, initialY) = GetInitialTicketParams(ticket);

        ticket.Opacity = 1;
        await EnlargeInCenterAsync(ticket, 250, 0.7);

        var slotControl = slot == 1 ? _window.TicketSlot1Ref : _window.TicketSlot2Ref;
        if (slotControl == null)
            throw new InvalidOperationException("Slot control is not initialized.");
        if (slotControl.Parent is not Border targetBorder)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        var (targetWidth, targetHeight, targetX, targetY) = GetTargetSlotParams(slotControl);

        int duration = 600;
        await AnimateTicketAndSizeAsync(
            ticket,
            targetBorder,
            initialWidth, initialHeight, initialX, initialY,
            targetWidth, targetHeight, targetX, targetY,
            duration);

        overlay.Children.Remove(ticket);
        ticket.RenderTransform = null;

        if (slotControl != null)
        {
            ticket.Width = slotControl.Bounds.Width;
            ticket.Height = slotControl.Bounds.Height;
            ticket.SetFontScale();
            ticket.SetColourStripScale((ticket.Width / 240.0) + 1);
        }

        if (slot == 1)
        {
            _window.SetMainView1(ticket);
            slotControl.Content = ticket;
        }
        else
        {
            _window.SetMainView2(ticket);
            slotControl.Content = ticket;
        }

        await FadeOverlayAsync(0.3, 0, 120);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    #region Internal Helpers

    private void RemoveTicketFromParent(TicketTemplate ticketTemplate)
    {
        if (ticketTemplate.Parent is Panel oldPanel)
            oldPanel.Children.Remove(ticketTemplate);
        else if (ticketTemplate.Parent is ContentControl oldContent)
            oldContent.Content = null;
        else if (ticketTemplate.Parent is Decorator oldDecorator)
            oldDecorator.Child = null;
        else if (ticketTemplate.Parent != null)
            throw new InvalidOperationException("TicketTemplate attached to unsupported parent type.");
    }

    private (double initialWidth, double initialHeight, double initialX, double initialY)
        GetInitialTicketParams(TicketTemplate ticket)
    {
        double initialWidth = _window.Bounds.Width * 0.3;
        double initialHeight = _window.Bounds.Height * 0.5;
        double initialX = (_window.Bounds.Width - initialWidth) / 2;
        double initialY = (_window.Bounds.Height - initialHeight) / 2;

        ticket.Width = initialWidth;
        ticket.Height = initialHeight;
        Canvas.SetLeft(ticket, initialX);
        Canvas.SetTop(ticket, initialY);

        double fontScale = initialWidth / 240.0;
        ticket.SetColourStripScale(fontScale + 1);

        return (initialWidth, initialHeight, initialX, initialY);
    }

    private (double targetWidth, double targetHeight, double targetX, double targetY)
        GetTargetSlotParams(ContentControl slotControl)
    {
        if (slotControl.Parent is not Border targetBorder)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        var borderPos = targetBorder.TranslatePoint(new Point(0, 0), _window);
        if (borderPos == null)
            throw new InvalidOperationException("Could not determine border position.");

        double targetWidth = slotControl.Bounds.Width;
        double targetHeight = slotControl.Bounds.Height;
        double targetX = borderPos.Value.X + (targetBorder.Bounds.Width - targetWidth) / 2;
        double targetY = borderPos.Value.Y + ((targetBorder.Bounds.Height - targetHeight) / 2);
        return (targetWidth, targetHeight, targetX, targetY);
    }

    private static void EnsureTransition(Animatable target, AvaloniaProperty<double> prop, int durationMs, Easing? easing = null)
    {
        target.Transitions ??= new Transitions();
        var existing = target.Transitions.OfType<DoubleTransition>().FirstOrDefault(x => x.Property == prop);
        if (existing == null)
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
            existing.Duration = TimeSpan.FromMilliseconds(durationMs);
            existing.Easing = easing ?? new SineEaseInOut();
        }
    }

    public async Task FadeOverlayAsync(double from, double to, int durationMs)
    {
        var dark = _window.DarkOverlayRef;
        if (dark is null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            dark.Opacity = from;
            EnsureTransition(dark, Visual.OpacityProperty, durationMs, new SineEaseInOut());
            dark.Opacity = to;
        }, DispatcherPriority.Render);

        await Task.Delay(durationMs + 16).ConfigureAwait(false);
    }

    public async Task EnlargeInCenterAsync(Control target, int durationMs, double startScale)
    {
        var group = target.RenderTransform as TransformGroup ?? new TransformGroup();
        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault();
        if (scale == null)
        {
            scale = new ScaleTransform(1, 1);
            group.Children.Add(scale);
        }
        target.RenderTransform = group;
        target.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            scale.ScaleX = startScale;
            scale.ScaleY = startScale;

            EnsureTransition(scale, ScaleTransform.ScaleXProperty, durationMs, new SineEaseInOut());
            EnsureTransition(scale, ScaleTransform.ScaleYProperty, durationMs, new SineEaseInOut());

            scale.ScaleX = 1d;
            scale.ScaleY = 1d;
        }, DispatcherPriority.Render);

        await Task.Delay(durationMs + 16).ConfigureAwait(false);
    }

    private async Task AnimateTransformToSlotAsync(
        Control target,
        double initialWidth, double initialHeight, double initialX, double initialY,
        double targetWidth, double targetHeight, double targetX, double targetY,
        int durationMs)
    {
        var group = target.RenderTransform as TransformGroup ?? new TransformGroup();
        var scale = group.Children.OfType<ScaleTransform>().FirstOrDefault() ?? new ScaleTransform(1, 1);
        var translate = group.Children.OfType<TranslateTransform>().FirstOrDefault() ?? new TranslateTransform(0, 0);
        if (!group.Children.Contains(scale)) group.Children.Insert(0, scale);
        if (!group.Children.Contains(translate)) group.Children.Add(translate);
        target.RenderTransform = group;
        target.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

        target.Width = initialWidth;
        target.Height = initialHeight;
        Canvas.SetLeft(target, initialX);
        Canvas.SetTop(target, initialY);

        double sx = targetWidth / initialWidth;
        double sy = targetHeight / initialHeight;
        double dx = targetX - initialX;
        double dy = targetY - initialY;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            scale.ScaleX = 1d;
            scale.ScaleY = 1d;
            translate.X = 0d;
            translate.Y = 0d;

            EnsureTransition(scale, ScaleTransform.ScaleXProperty, durationMs, new SineEaseInOut());
            EnsureTransition(scale, ScaleTransform.ScaleYProperty, durationMs, new SineEaseInOut());
            EnsureTransition(translate, TranslateTransform.XProperty, durationMs, new SineEaseInOut());
            EnsureTransition(translate, TranslateTransform.YProperty, durationMs, new SineEaseInOut());

            scale.ScaleX = sx;
            scale.ScaleY = sy;
            translate.X = dx;
            translate.Y = dy;
        }, DispatcherPriority.Render);

        await Task.Delay(durationMs + 16).ConfigureAwait(false);
    }

    private Task AnimateTicketAndSizeAsync(
        TicketTemplate ticket,
        Border _,
        double initialWidth, double initialHeight, double initialX, double initialY,
        double targetWidth, double targetHeight, double targetX, double targetY,
        int duration) =>
        AnimateTransformToSlotAsync(ticket, initialWidth, initialHeight, initialX, initialY,
                                    targetWidth, targetHeight, targetX, targetY, duration);

    #endregion
}