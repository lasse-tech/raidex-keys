// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RaidexKeys.Core;

namespace RaidexKeys.App.ViewModels;

/// <summary>A column a table sorts by: its rows' value, and whether a first click puts the highest first.</summary>
/// <param name="Key">The value; null for a row without one, which then goes to the end.</param>
/// <param name="DescendingFirst">For levels, ratings, forces: the highest first. Text and times start at the lowest.</param>
public sealed record SortColumn<T>(Func<T, IComparable?> Key, bool DescendingFirst = false);

/// <summary>What a column header needs of its table (Views/SortHeader).</summary>
public interface ITableSort : INotifyPropertyChanged
{
    /// <summary>The column sorted by; null while the table keeps its own order.</summary>
    string? Column { get; }

    bool Descending { get; }

    void SortBy(string column);
}

/// <summary>
/// A table's rows in the order a click on a column header chose: the first
/// click sorts by the column, every further one reverses it. The choice is
/// kept per table in the settings, so it outlives the page - rebuilt with
/// every save of the addon's file - and the app. Until the first click the
/// table keeps its own order, which also settles rows of equal value.
/// </summary>
/// <param name="table">The name the settings keep the choice under.</param>
public sealed class TableSort<T> : ObservableObject, ITableSort
{
    private readonly string _table;
    private readonly IReadOnlyList<T> _rows;
    private readonly IReadOnlyDictionary<string, SortColumn<T>> _columns;

    public TableSort(string table, IReadOnlyList<T> rows, IReadOnlyDictionary<string, SortColumn<T>> columns)
    {
        _table = table;
        _rows = rows;
        _columns = columns;
        // A column no longer there (renamed in an update) means the table's own
        // order; so do the AppStream screenshots, whoever's settings they run with.
        if (Program.ScreenshotFile is null && AppSettings.Load().TableSorts.TryGetValue(table, out var saved)
            && columns.ContainsKey(saved.Column))
        {
            Column = saved.Column;
            Descending = saved.Descending;
        }
        Rows = Order();
    }

    public IReadOnlyList<T> Rows { get; private set; }

    public string? Column { get; private set; }

    public bool Descending { get; private set; }

    public void SortBy(string column)
    {
        if (!_columns.TryGetValue(column, out var sort))
        {
            return;
        }
        Descending = column == Column ? !Descending : sort.DescendingFirst;
        Column = column;
        Rows = Order();
        OnPropertyChanged(nameof(Column));
        OnPropertyChanged(nameof(Descending));
        OnPropertyChanged(nameof(Rows));

        var settings = AppSettings.Load();
        settings.TableSorts[_table] = new TableSortChoice { Column = column, Descending = Descending };
        settings.Save();
    }

    private IReadOnlyList<T> Order() => Column is { } column
        ? TableOrder.Sort(_rows, _columns[column].Key, Descending)
        : _rows;
}
