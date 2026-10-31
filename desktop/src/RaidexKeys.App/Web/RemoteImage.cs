// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RaidexKeys.Core;

namespace RaidexKeys.App.Web;

/// <summary>
/// <c>web:RemoteImage.Url="https://…"</c> on an Image, or
/// <c>web:RemoteImage.Background="https://…"</c> on a Border: loads the picture
/// from the web once, keeps it on disk, and shows nothing while offline.
/// </summary>
/// <remarks>
/// As a background the picture never sizes the element: a row stays a row,
/// whatever the picture's own proportions - an Image inside it would stretch
/// the row to the picture's height.
/// </remarks>
public static class RemoteImage
{
    public static readonly AttachedProperty<string?> UrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Url", typeof(RemoteImage));

    public static readonly AttachedProperty<string?> BackgroundProperty =
        AvaloniaProperty.RegisterAttached<Border, string?>("Background", typeof(RemoteImage));

    static RemoteImage()
    {
        UrlProperty.Changed.AddClassHandler<Image>((image, e) => Show(image, e.NewValue as string));
        BackgroundProperty.Changed.AddClassHandler<Border>((border, e) => ShowBehind(border, e.NewValue as string));
    }

    public static string? GetBackground(Border border) => border.GetValue(BackgroundProperty);

    public static void SetBackground(Border border, string? value) => border.SetValue(BackgroundProperty, value);

    private static async void ShowBehind(Border border, string? url)
    {
        border.Background = null;
        if (string.IsNullOrEmpty(url))
        {
            return;
        }
        var bitmap = await ImageCache.GetAsync(url);
        if (bitmap is not null && GetBackground(border) == url)
        {
            await Dispatcher.UIThread.InvokeAsync(() => border.Background = new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Right,
                AlignmentY = AlignmentY.Center,
                Opacity = 0.6,
            });
        }
    }

    public static string? GetUrl(Image image) => image.GetValue(UrlProperty);

    public static void SetUrl(Image image, string? value) => image.SetValue(UrlProperty, value);

    private static async void Show(Image image, string? url)
    {
        image.Source = null;
        if (string.IsNullOrEmpty(url))
        {
            return;
        }
        var bitmap = await ImageCache.GetAsync(url);
        // The row may show another dungeon by now.
        if (GetUrl(image) == url)
        {
            await Dispatcher.UIThread.InvokeAsync(() => image.Source = bitmap);
        }
    }
}

/// <summary>Pictures from the web: in memory for the run, on disk across runs.</summary>
internal static class ImageCache
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "RaidexKeys (lars@lasse-tech.de)" } },
    };

    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Loaded = new(StringComparer.Ordinal);

    /// <summary>~/.cache/raidex-keys/images on Linux, %LOCALAPPDATA%\RaidexKeys\images on Windows.</summary>
    private static string Folder => Path.Combine(AppFolders.Cache, "images");

    public static Task<Bitmap?> GetAsync(string url) => Loaded.GetOrAdd(url, Load);

    /// <summary>
    /// Completes once no picture asked for is still on its way - those asked
    /// for while waiting included. Loaded or failed, either is done.
    /// </summary>
    public static async Task WhenIdle()
    {
        while (Loaded.Values.Where(t => !t.IsCompleted).ToArray() is { Length: > 0 } pending)
        {
            await Task.WhenAll(pending.Select(t => t.ContinueWith(_ => { }, TaskScheduler.Default))).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A screenshot never shows a picture out of the disk cache: what goes on
    /// the landing page is what the web serves that day (Lars, 2026-09-13).
    /// Nothing is written back either - the cache stays as the player left it.
    /// </summary>
    private static bool Fresh => Program.ScreenshotFile is not null;

    private static async Task<Bitmap?> Load(string url)
    {
        var file = Path.Combine(Folder, Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url))) + Path.GetExtension(new Uri(url).AbsolutePath));
        try
        {
            if (Fresh)
            {
                var served = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                return new Bitmap(new MemoryStream(served));
            }
            if (!File.Exists(file))
            {
                var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
                Directory.CreateDirectory(Folder);
                await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
            }
            return new Bitmap(file);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Offline or gone: the row goes without its picture - and may try again next run.
            Loaded.TryRemove(url, out _);
            return null;
        }
    }
}
