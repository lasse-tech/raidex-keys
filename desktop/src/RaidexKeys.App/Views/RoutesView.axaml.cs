// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace RaidexKeys.App.Views;

public partial class RoutesView : UserControl
{
    public RoutesView()
    {
        InitializeComponent();
        // --expanded: the first route starts slid out, and its map is what the
        // shot is for - so bring the map itself into view once the row has
        // finished sliding (Views/Collapsible tweens for 0.26 s).
        if (Program.Expanded)
        {
            Loaded += (_, _) => DispatcherTimer.RunOnce(Reveal, TimeSpan.FromMilliseconds(450));
        }
    }

    /// <summary>The map at the top of the page, with its row's line left above it.</summary>
    private void Reveal()
    {
        // The open row's map, not the first one in the tree: a folded row keeps
        // its map, at no height, and the row slid out may be further down.
        if (this.GetVisualDescendants().OfType<RouteMapView>().FirstOrDefault(m => m.Bounds.Height > 0)
            is not { } map)
        {
            return;
        }
        map.BringIntoView();
        if (this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } page)
        {
            page.Offset = page.Offset.WithY(Math.Max(0, page.Offset.Y - 52));
        }
    }
}
