// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using RaidexKeys.App.Localization;
using RaidexKeys.App.ViewModels;
using Path = Avalonia.Controls.Shapes.Path;

namespace RaidexKeys.App.Views;

/// <summary>
/// A table's column header that sorts: the label as the other headers have
/// it, a click sorts the table by the column, the next one reverses it. The
/// column sorted by shows its label in the text colour and a small arrow -
/// up for the lowest first, down for the highest.
/// </summary>
public sealed class SortHeader : Button
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SortHeader, string?>(nameof(Text));

    /// <summary>The column's name in its table (SortColumn), as the settings keep it.</summary>
    public static readonly StyledProperty<string?> ColumnProperty =
        AvaloniaProperty.Register<SortHeader, string?>(nameof(Column));

    public static readonly StyledProperty<ITableSort?> TableProperty =
        AvaloniaProperty.Register<SortHeader, ITableSort?>(nameof(Table));

    private readonly TextBlock _label = new() { Classes = { "label" }, LetterSpacing = 1.2, VerticalAlignment = VerticalAlignment.Center };
    // Always there, shown only while sorted by: every header keeps its width,
    // so no column moves when the sort goes to another.
    private readonly Path _arrow = new() { Width = 7, Height = 4, Stretch = Stretch.Fill, VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };

    public SortHeader()
    {
        Classes.Add("sort");
        ToolTip.SetTip(this, Strings.SortTip);
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _label, _arrow } };
    }

    protected override Type StyleKeyOverride => typeof(Button);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Column
    {
        get => GetValue(ColumnProperty);
        set => SetValue(ColumnProperty, value);
    }

    public ITableSort? Table
    {
        get => GetValue(TableProperty);
        set => SetValue(TableProperty, value);
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Column is { } column)
        {
            Table?.SortBy(column);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            _label.Text = Text;
        }
        else if (change.Property == TableProperty)
        {
            if (change.OldValue is ITableSort old)
            {
                old.PropertyChanged -= OnTableChanged;
            }
            if (change.NewValue is ITableSort table)
            {
                table.PropertyChanged += OnTableChanged;
            }
            Show();
        }
        else if (change.Property == ColumnProperty)
        {
            Show();
        }
    }

    private void OnTableChanged(object? sender, PropertyChangedEventArgs e) => Show();

    private void Show()
    {
        var sorted = Table is { } table && Column is not null && table.Column == Column;
        _label.Classes.Set("sorted", sorted);
        _arrow.Opacity = sorted ? 1 : 0;
        _arrow.Data = Geometry.Parse(sorted && Table!.Descending ? "M0,0 L3.5,4 L7,0 Z" : "M0,4 L3.5,0 L7,4 Z");
    }
}
