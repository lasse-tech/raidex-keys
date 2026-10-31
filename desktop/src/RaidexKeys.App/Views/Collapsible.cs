// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RaidexKeys.App.Views;

/// <summary>
/// Shows its content when <see cref="IsExpanded"/> - sliding open and shut,
/// not jumping: the height eases from where it is to the content's height
/// (or to nothing) while the content fades in.
/// </summary>
/// <remarks>
/// A tween of Height rather than a MaxHeight transition: a MaxHeight to some
/// large number collapses late and opens too fast, since the real height is
/// only a part of it. Here the target is the content's measured height.
/// </remarks>
public sealed class Collapsible : Decorator
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<Collapsible, bool>(nameof(IsExpanded));

    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(260);

    private DispatcherTimer? _timer;

    static Collapsible()
    {
        ClipToBoundsProperty.OverrideDefaultValue<Collapsible>(true);
        IsExpandedProperty.Changed.AddClassHandler<Collapsible>((c, _) => c.Slide());
    }

    public Collapsible()
    {
        Height = 0;
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Appearing, it stands as it is - open or shut - without sliding: a row
    /// built open (a list rebuilt after a change, a pasted route) must not
    /// grow from nothing, which would pull the page's scroll position with it.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer?.Stop();
        Height = IsExpanded ? double.NaN : 0;
        if (Child is { } child)
        {
            child.Opacity = IsExpanded ? 1 : 0;
        }
    }

    private void Slide()
    {
        if (Child is not { } child || VisualRoot is null)
        {
            return;
        }
        var width = Bounds.Width > 0 ? Bounds.Width : double.PositiveInfinity;
        child.Measure(new Size(width, double.PositiveInfinity));
        var from = double.IsNaN(Height) ? Bounds.Height : Height;
        var to = IsExpanded ? child.DesiredSize.Height : 0;
        var expanding = IsExpanded;
        var started = DateTime.UtcNow;

        _timer?.Stop();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) =>
        {
            var t = Math.Min(1, (DateTime.UtcNow - started) / Duration);
            var eased = 1 - Math.Pow(1 - t, 3);   // ease out: fast start, soft landing
            Height = from + (to - from) * eased;
            child.Opacity = expanding ? eased : 1 - eased;
            if (t >= 1)
            {
                _timer?.Stop();
                // Open, the content sizes itself again (a window resized, text wrapped).
                if (expanding)
                {
                    Height = double.NaN;
                }
            }
        });
        _timer.Start();
    }
}
