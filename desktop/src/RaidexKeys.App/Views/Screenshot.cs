// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RaidexKeys.App.Web;

namespace RaidexKeys.App.Views;

/// <summary>
/// <c>--preview … --screenshot file.png</c>: the window rendered into a PNG,
/// the way the AppStream screenshots are made.
/// </summary>
/// <remarks>
/// Rendered from the window's own visual tree, not grabbed from the screen:
/// the same pixels on every desktop, without title bar, shadow or whatever
/// lies behind, at the size set here whatever the screen's scaling.
/// </remarks>
internal static class Screenshot
{
    /// <summary>16:9, as software centres show screenshots.</summary>
    public const int Width = 1280, Height = 720;

    public static async Task TakeAsync(Window window, string file, double scale)
    {
        // The rows ask for their dungeon pictures once laid out; wait for them.
        // For a screenshot they come from the web, never the disk cache
        // (ImageCache), and offline a row goes without.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        try
        {
            await ImageCache.WhenIdle().WaitAsync(TimeSpan.FromSeconds(20));
        }
        catch (TimeoutException)
        {
            // Taken without the pictures still missing.
        }
        // The pictures' brushes are set on the UI thread; one more layout pass.
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            using var bitmap = new RenderTargetBitmap(
                new PixelSize((int)Math.Round(Width * scale), (int)Math.Round(Height * scale)),
                new Vector(96 * scale, 96 * scale));
            bitmap.Render(window);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            bitmap.Save(file, new PngBitmapEncoderOptions());
        });
    }
}
