// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using RaidexKeys.App.ViewModels;

namespace RaidexKeys.App.Views;

public partial class KeyTable : UserControl
{
    /// <summary>The rows, in the order their column headers chose.</summary>
    public static readonly StyledProperty<TableSort<KeyRowViewModel>?> TableProperty =
        AvaloniaProperty.Register<KeyTable, TableSort<KeyRowViewModel>?>(nameof(Table));

    /// <summary>The vault column: on for the own characters, off for the guild.</summary>
    public static readonly StyledProperty<bool> ShowVaultProperty =
        AvaloniaProperty.Register<KeyTable, bool>(nameof(ShowVault));

    public KeyTable() => InitializeComponent();

    public bool ShowVault
    {
        get => GetValue(ShowVaultProperty);
        set => SetValue(ShowVaultProperty, value);
    }

    public TableSort<KeyRowViewModel>? Table
    {
        get => GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }
}
