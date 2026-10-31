// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using RaidexKeys.Core.Rating;

namespace RaidexKeys.App.Views;

/// <summary>The role a character plays, as a small glyph: shield, cross, sword.</summary>
public partial class RoleIcon : UserControl
{
    public static readonly StyledProperty<GroupRole> RoleProperty =
        AvaloniaProperty.Register<RoleIcon, GroupRole>(nameof(Role));

    /// <summary>True when the bound role is the one the parameter names.</summary>
    public static readonly IValueConverter Is =
        new FuncValueConverter<GroupRole, object?, bool>((role, wanted) => Equals(role, wanted));

    public RoleIcon() => InitializeComponent();

    public GroupRole Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    /// <summary>The role's name for a tooltip, in the interface's language.</summary>
    public static string Label(GroupRole role) => role switch
    {
        GroupRole.Tank => Localization.Strings.RoleTank,
        GroupRole.Healer => Localization.Strings.RoleHealer,
        GroupRole.Damage => Localization.Strings.RoleDamage,
        _ => Localization.Strings.RoleUnknown,
    };
}
