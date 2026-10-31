// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using RaidexKeys.App.ViewModels;

namespace RaidexKeys.App.Views;

/// <summary>
/// A click anywhere on a row's line slides its details in or out, as the
/// chevron does. Links and the chevron itself keep their own click.
/// </summary>
public static class RowToggle
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(RowToggle));

    static RowToggle() => IsEnabledProperty.Changed.AddClassHandler<Control>((control, e) =>
    {
        control.Tapped -= OnTapped;
        if (e.NewValue is true)
        {
            control.Tapped += OnTapped;
        }
    });

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: IRowDetails row } line)
        {
            return;
        }
        // Buttons (the links) and the chevron, a ToggleButton, are Buttons.
        for (var v = e.Source as Visual; v is not null && v != line; v = v.GetVisualParent())
        {
            if (v is Button)
            {
                return;
            }
        }
        row.IsExpanded = !row.IsExpanded;
    }
}
