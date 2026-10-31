// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia.Controls;
using Avalonia.Interactivity;
using RaidexKeys.App.ViewModels;

namespace RaidexKeys.App.Views;

/// <summary>Asks for the AddOns folder at first start.</summary>
/// <remarks>Closes with the folder to use, or null for "later" - then it asks again next start.</remarks>
public partial class SetupWindow : Window
{
    public SetupWindow() => InitializeComponent();

    public SetupWindow(AddOnsFolderViewModel folder) : this() => DataContext = folder;

    private void ApplyClicked(object? sender, RoutedEventArgs e) =>
        Close((DataContext as AddOnsFolderViewModel)?.Check.AddOnsDir);

    private void LaterClicked(object? sender, RoutedEventArgs e) => Close(null);
}
