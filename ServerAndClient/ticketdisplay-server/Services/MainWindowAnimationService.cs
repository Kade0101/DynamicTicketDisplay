using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace TicketDisplayAppModified.Views;

internal sealed class MainWindowAnimationService
{
    public readonly MainWindow _window;

    public MainWindowAnimationService(MainWindow window) => _window = window;

    public async Task AnimateTicketToSlotAsync(TicketTemplate ticket, int slot)
    {
        try
        {
            await FadeOverlayAsync(0, 0.3, 140);

            await Dispatcher.UIThread.InvokeAsync(() => RemoveTicketFromParent(ticket));

            var overlay = _window.OverlayCanvasRef;
            if (overlay is null)
                throw new InvalidOperationException("Overlay canvas not initialized.");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ticket.Opacity = 0;
                overlay.Children.Add(ticket);
            }, DispatcherPriority.Render);

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var (initialWidth, initialHeight, initialX, initialY) = GetInitialTicketParams(ticket);

            await EnlargeInCenterAsync(ticket, 900, 0.4);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ticket.RenderTransform = null;
                ticket.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
                ticket.Width = initialWidth;
                ticket.Height = initialHeight;
                Canvas.SetLeft(ticket, initialX);
                Canvas.SetTop(ticket, initialY);
                ticket.Opacity = 1;
            }, DispatcherPriority.Render);

            await Task.Delay(850).ConfigureAwait(false);

            var slotControl = _window.GetTicketSlotRef(slot);
            if (slotControl == null)
                throw new InvalidOperationException("Slot control is not initialized.");

            var targetBorder = FindContainingBorder(slotControl);
            if (targetBorder == null)
                throw new InvalidOperationException("Slot control is not inside a Border.");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                targetBorder.InvalidateMeasure();
                targetBorder.InvalidateArrange();
                slotControl.InvalidateMeasure();
                slotControl.InvalidateArrange();
            }, DispatcherPriority.Render);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var (targetWidth, targetHeight, targetX, targetY) = GetTargetSlotParams(slotControl);

            if (targetWidth <= 0 || targetHeight <= 0)
                throw new InvalidOperationException("Target slot has invalid size for animation.");

            await AnimateTicketAndSizeAsync(ticket, initialWidth, initialHeight, initialX, initialY,
                targetWidth, targetHeight, targetX, targetY, 700);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                overlay.Children.Remove(ticket);

                ticket.RenderTransform = null;
                Canvas.SetLeft(ticket, 0);
                Canvas.SetTop(ticket, 0);

                ticket.Width = slotControl.Bounds.Width;
                ticket.Height = slotControl.Bounds.Height;
                ticket.ApplyScale(ticket.Width / 240.0);

                _window.SetMainView(slot, ticket);

                slotControl.Content = ticket;
                _window.RefreshLayoutChrome();
            }, DispatcherPriority.Render);

            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        }
        finally
        {
            await FadeOverlayAsync(0.3, 0, 140);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        }
    }

    public async Task AnimateLiveSlotReflowAsync(Dictionary<int, MainWindow.LiveSlotSnapshot> previousLayout, int durationMs = 520)
    {
        if (previousLayout.Count == 0)
            return;

        var animations = new List<(TicketTemplate ticket, TranslateTransform transform, double startX, double startY, double startWidth, double startHeight, double targetWidth, double targetHeight)>();
        var relativeTo = (Visual?)_window.OverlayCanvasRef ?? _window;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var entry in previousLayout)
            {
                var slotControl = _window.GetTicketSlotRef(entry.Key);
                if (slotControl?.Content is TicketTemplate ticket)
                    ticket.Opacity = 0;
            }

            _window.InvalidateMeasure();
            _window.InvalidateArrange();
        }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var entry in previousLayout)
            {
                var slotControl = _window.GetTicketSlotRef(entry.Key);
                if (slotControl?.Content is not TicketTemplate ticket)
                    continue;

                var currentCenter = slotControl.TranslatePoint(
                    new Point(slotControl.Bounds.Width / 2, slotControl.Bounds.Height / 2),
                    relativeTo);

                if (!currentCenter.HasValue)
                    continue;

                double deltaX = entry.Value.Center.X - currentCenter.Value.X;
                double deltaY = entry.Value.Center.Y - currentCenter.Value.Y;
                double targetWidth = slotControl.Bounds.Width;
                double targetHeight = slotControl.Bounds.Height;

                if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5 &&
                    Math.Abs(entry.Value.Width - targetWidth) < 0.5 && Math.Abs(entry.Value.Height - targetHeight) < 0.5)
                {
                    ticket.Opacity = 1;
                    continue;
                }

                var transform = EnsureTranslateTransform(ticket);
                transform.X = deltaX;
                transform.Y = deltaY;
                ticket.Width = entry.Value.Width;
                ticket.Height = entry.Value.Height;
                ticket.ApplyScale(ticket.Width / 240.0);
                ticket.Opacity = 1;
                animations.Add((ticket, transform, deltaX, deltaY, entry.Value.Width, entry.Value.Height, targetWidth, targetHeight));
            }
        }, DispatcherPriority.Render);

        if (animations.Count == 0)
            return;

        var easing = new SineEaseInOut();
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / durationMs, 0, 1);
            double eased = easing.Ease(progress);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var animation in animations)
                {
                    animation.ticket.Width = animation.startWidth + ((animation.targetWidth - animation.startWidth) * eased);
                    animation.ticket.Height = animation.startHeight + ((animation.targetHeight - animation.startHeight) * eased);
                    animation.ticket.ApplyScale(animation.ticket.Width / 240.0);
                    animation.transform.X = animation.startX * (1 - eased);
                    animation.transform.Y = animation.startY * (1 - eased);
                }
            }, DispatcherPriority.Render);

            if (progress >= 1)
                break;

            await Task.Delay(8).ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var animation in animations)
            {
                animation.ticket.Width = animation.targetWidth;
                animation.ticket.Height = animation.targetHeight;
                animation.ticket.ApplyScale(animation.ticket.Width / 240.0);
                animation.transform.X = 0;
                animation.transform.Y = 0;
            }
        }, DispatcherPriority.Render);
    }

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

    private (double initialWidth, double initialHeight, double initialX, double initialY) GetInitialTicketParams(TicketTemplate ticket)
    {
        double initialWidth = _window.Bounds.Width * 0.3;
        double initialHeight = _window.Bounds.Height * 0.5;

        var overlay = _window.OverlayCanvasRef;
        double overlayWidth = overlay?.Bounds.Width > 0 ? overlay.Bounds.Width : _window.Bounds.Width;
        double overlayHeight = overlay?.Bounds.Height > 0 ? overlay.Bounds.Height : _window.Bounds.Height;

        double initialX = (overlayWidth - initialWidth) / 2;
        double initialY = ((overlayHeight - initialHeight) / 2) - (overlayHeight * 0.05);

        Dispatcher.UIThread.Invoke(() =>
        {
            ticket.Width = initialWidth;
            ticket.Height = initialHeight;
            Canvas.SetLeft(ticket, initialX);
            Canvas.SetTop(ticket, initialY);
            ticket.ApplyScale(initialWidth / 240.0);
        });

        return (initialWidth, initialHeight, initialX, initialY);
    }

    private static Border? FindContainingBorder(Control control)
    {
        var current = control.Parent as Control;
        while (current != null)
        {
            if (current is Border border)
                return border;

            current = current.Parent as Control;
        }

        return null;
    }

    private static TranslateTransform EnsureTranslateTransform(Control control)
    {
        switch (control.RenderTransform)
        {
            case TranslateTransform translate:
                return translate;
            case TransformGroup group:
                {
                    var existingTranslate = group.Children.Count > 0
                        ? group.Children[group.Children.Count - 1] as TranslateTransform
                        : null;

                    if (existingTranslate != null)
                        return existingTranslate;

                    var newTranslate = new TranslateTransform();
                    group.Children.Add(newTranslate);
                    control.RenderTransform = group;
                    return newTranslate;
                }
            case null:
                {
                    var newTranslate = new TranslateTransform();
                    control.RenderTransform = newTranslate;
                    return newTranslate;
                }
            case Transform existingTransform:
                {
                    var group = new TransformGroup();
                    group.Children.Add(existingTransform);
                    var newTranslate = new TranslateTransform();
                    group.Children.Add(newTranslate);
                    control.RenderTransform = group;
                    return newTranslate;
                }
            default:
                {
                    var newTranslate = new TranslateTransform();
                    control.RenderTransform = newTranslate;
                    return newTranslate;
                }
        }
    }

    private (double targetWidth, double targetHeight, double targetX, double targetY) GetTargetSlotParams(ContentControl slotControl)
    {
        var targetBorder = FindContainingBorder(slotControl);
        if (targetBorder == null)
            throw new InvalidOperationException("Slot control is not inside a Border.");

        var overlay = _window.OverlayCanvasRef;
        if (overlay is null)
            throw new InvalidOperationException("Overlay canvas not initialized.");

        Point? borderPos = null;
        double targetWidth = 0;
        double targetHeight = 0;
        double borderWidth = 0;
        double borderHeight = 0;

        Dispatcher.UIThread.Invoke(() =>
        {
            borderPos = targetBorder.TranslatePoint(new Point(0, 0), overlay);
            targetWidth = slotControl.Bounds.Width;
            targetHeight = slotControl.Bounds.Height;
            borderWidth = targetBorder.Bounds.Width;
            borderHeight = targetBorder.Bounds.Height;
        });

        if (borderPos == null)
            throw new InvalidOperationException("Could not determine border position.");

        double targetX = borderPos.Value.X + (borderWidth - targetWidth) / 2;
        double targetY = borderPos.Value.Y + (borderHeight - targetHeight) / 2;
        return (targetWidth, targetHeight, targetX, targetY);
    }

    public async Task FadeOverlayAsync(double from, double to, int durationMs)
    {
        var dark = _window.DarkOverlayRef;
        if (dark is null) return;

        var easing = new SineEaseInOut();
        var stopwatch = Stopwatch.StartNew();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            dark.IsVisible = true;
            dark.Opacity = from;
        }, DispatcherPriority.Render);

        while (true)
        {
            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / durationMs, 0, 1);
            double eased = easing.Ease(progress);
            double opacity = from + ((to - from) * eased);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                dark.Opacity = opacity;
            }, DispatcherPriority.Render);

            if (progress >= 1)
                break;

            await Task.Delay(8).ConfigureAwait(false);
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            dark.Opacity = to;
            if (to <= 0)
            {
                dark.IsVisible = false;
                dark.Opacity = 0;
            }
        }, DispatcherPriority.Render);
    }

    public async Task EnlargeInCenterAsync(Control target, int durationMs, double startScale)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var group = new TransformGroup();
            var scale = new ScaleTransform(startScale, startScale);
            group.Children.Add(scale);
            target.RenderTransform = group;
            target.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            target.Opacity = 0;
        }, DispatcherPriority.Render);

        var easing = new SineEaseInOut();
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / durationMs, 0, 1);
            double eased = easing.Ease(progress);
            double currentScale = startScale + ((1d - startScale) * eased);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (target.RenderTransform is TransformGroup group && group.Children.Count > 0 && group.Children[0] is ScaleTransform scale)
                {
                    target.Opacity = eased;
                    scale.ScaleX = currentScale;
                    scale.ScaleY = currentScale;
                }
            }, DispatcherPriority.Render);

            if (progress >= 1)
                break;

            await Task.Delay(8).ConfigureAwait(false);
        }
    }

    private async Task AnimateTicketAndSizeAsync(
        TicketTemplate ticket,
        double initialWidth, double initialHeight, double initialX, double initialY,
        double targetWidth, double targetHeight, double targetX, double targetY,
        int durationMs)
    {
        var easing = new SineEaseInOut();
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / durationMs, 0, 1);
            double eased = easing.Ease(progress);

            double currentWidth = initialWidth + ((targetWidth - initialWidth) * eased);
            double currentHeight = initialHeight + ((targetHeight - initialHeight) * eased);
            double currentX = initialX + ((targetX - initialX) * eased);
            double currentY = initialY + ((targetY - initialY) * eased);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ticket.Width = currentWidth;
                ticket.Height = currentHeight;
                Canvas.SetLeft(ticket, currentX);
                Canvas.SetTop(ticket, currentY);
                ticket.ApplyScale(currentWidth / 240.0);
            }, DispatcherPriority.Render);

            if (progress >= 1)
                break;

            await Task.Delay(8).ConfigureAwait(false);
        }
    }
}