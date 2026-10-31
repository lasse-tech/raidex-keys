// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RaidexKeys.App.Localization;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App.ViewModels;

/// <summary>How the entered folder looks: fine, usable with a caveat, or wrong.</summary>
public enum FolderState
{
    Ok,
    Caveat,
    Wrong,
}

/// <summary>
/// The AddOns folder field - at first start and in the settings. It checks
/// every input as it is typed and says what it found.
/// </summary>
public sealed partial class AddOnsFolderViewModel : ViewModelBase
{
    /// <param name="current">The folder stored so far, or null to prefill with the first one found.</param>
    public AddOnsFolderViewModel(string? current)
    {
        Suggestions = WowLocator.FindAddOnsFolders();
        Path = current ?? Suggestions.FirstOrDefault() ?? "";
    }

    /// <summary>Retail AddOns folders found on this machine.</summary>
    public IReadOnlyList<string> Suggestions { get; }

    /// <summary>Other folders found besides the one in the field.</summary>
    public IReadOnlyList<string> OtherSuggestions => Suggestions.Where(s => s != Path).ToList();

    public bool HasOtherSuggestions => OtherSuggestions.Count > 0;

    [ObservableProperty]
    public partial string Path { get; set; }

    public AddOnsFolderCheck Check { get; private set; } = AddOnsFolder.Inspect(null);

    partial void OnPathChanged(string value)
    {
        Check = AddOnsFolder.Inspect(value);
        OnPropertyChanged(nameof(Check));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsOk));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(OtherSuggestions));
        OnPropertyChanged(nameof(HasOtherSuggestions));
    }

    public FolderState State => !Check.IsValid ? FolderState.Wrong
        : Check.HasAddon && Check.SavedVariables.Count > 0 ? FolderState.Ok
        : FolderState.Caveat;

    public bool IsOk => State == FolderState.Ok;

    /// <summary>A wrong folder cannot be applied; one without the addon can - it may come later.</summary>
    public bool CanApply => Check.IsValid;

    public string StatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                return Strings.FolderNothingFound;
            }
            if (!Check.IsValid)
            {
                return Strings.FolderWrong;
            }
            if (!Check.HasAddon)
            {
                return Strings.FolderNoAddon;
            }
            if (Check.SavedVariables.Count == 0)
            {
                return Strings.FolderNoData;
            }
            var accounts = string.Join(", ", Check.SavedVariables.Select(AddOnsFolder.AccountOf));
            return string.Format(CultureInfo.CurrentCulture, Strings.FolderOk, accounts);
        }
    }

    [RelayCommand]
    private void UseSuggestion(string folder) => Path = folder;

    [RelayCommand]
    private async Task Browse()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        // The picker belongs to the window in front: the setup dialog at first start.
        var window = lifetime?.Windows.LastOrDefault(w => w.IsActive) ?? lifetime?.MainWindow;
        if (window is null)
        {
            return;
        }
        IStorageFolder? start = null;
        if (Check.AddOnsDir is { } existing)
        {
            start = await window.StorageProvider.TryGetFolderFromPathAsync(existing);
        }
        var picked = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.FolderPickTitle,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
        {
            // Whatever was picked - WoW folder, _retail_, AddOns - the field shows the AddOns folder.
            Path = AddOnsFolder.Inspect(path).AddOnsDir ?? path;
        }
    }
}
