// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia.Controls;
using Avalonia.Platform;

namespace RaidexKeys.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Each platform gets the tile the brand guide draws for it: the .ico
        // (R 12 %) on Windows, the hicolor tile (R 24 %) on Linux.
        var icon = OperatingSystem.IsWindows() ? "raidex-keys.ico" : "raidex-keys.png";
        Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://RaidexKeys/Assets/{icon}")));
        // Back where and as large as it was left - also after a language switch, which opens a new window.
        WindowPlacement.Keep(this, "main");
    }
}
