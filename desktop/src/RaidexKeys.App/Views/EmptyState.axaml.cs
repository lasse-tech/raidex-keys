// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RaidexKeys.App.Views;

public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Body));

    public static readonly StyledProperty<string?> BodyLinkProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(BodyLink));

    public static readonly StyledProperty<string?> BodyLinkUrlProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(BodyLinkUrl));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Detail));

    public static readonly StyledProperty<bool> OffersSampleProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(OffersSample));

    public EmptyState() => InitializeComponent();

    /// <summary>Offers a window with the sample data, for whoever has no game to show.</summary>
    public bool OffersSample
    {
        get => GetValue(OffersSampleProperty);
        set => SetValue(OffersSampleProperty, value);
    }

    private void OnSampleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => App.OpenSample();

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The sentence; with <see cref="BodyLink"/> the part after the link.</summary>
    public string? Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    /// <summary>The words the sentence opens with, as a link to <see cref="BodyLinkUrl"/>; null for none.</summary>
    public string? BodyLink
    {
        get => GetValue(BodyLinkProperty);
        set => SetValue(BodyLinkProperty, value);
    }

    public string? BodyLinkUrl
    {
        get => GetValue(BodyLinkUrlProperty);
        set => SetValue(BodyLinkUrlProperty, value);
    }

    public string? Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>
    /// The pointer is over the link's words: the link opens with a click.
    /// A run takes no pointer of its own, so the text is asked which
    /// character lies under the pointer - the link is the sentence's start.
    /// </summary>
    private bool OverLink(PointerEventArgs e)
    {
        if (string.IsNullOrEmpty(BodyLink) || string.IsNullOrEmpty(BodyLinkUrl))
        {
            return false;
        }
        var hit = BodyText.TextLayout.HitTestPoint(e.GetPosition(BodyText));
        return hit.IsInside && hit.TextPosition < BodyLink.Length;
    }

    private void OnBodyPointerMoved(object? sender, PointerEventArgs e) => Hover(OverLink(e));

    private void OnBodyPointerExited(object? sender, PointerEventArgs e) => Hover(false);

    private void Hover(bool over)
    {
        BodyText.Cursor = over ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        LinkRun.TextDecorations = over ? TextDecorations.Underline : null;
    }

    private async void OnBodyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(BodyText).Properties.IsLeftButtonPressed && OverLink(e))
        {
            e.Handled = true;
            await Web.Browser.OpenAsync(BodyLinkUrl);
        }
    }
}
