using System.Collections.Generic;

namespace FlagDeck.Components;

/// <summary>
/// View model rendered by the dashboard Razor component. Public so the generated Razor
/// component (also public) can expose typed <c>[Parameter]</c> properties on it.
/// </summary>
public sealed class DashboardModel
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string EnvironmentName { get; init; }
    public required FlagDeckTheme Theme { get; init; }
    public required string PathPrefix { get; init; }
    public required IReadOnlyList<FlagRowModel> Flags { get; init; }
    public string? Search { get; init; }
    public int OverrideCount { get; init; }
    public string Version { get; init; } = "0.0.0";
}

/// <summary>
/// View model for a single row in the dashboard's flag list.
/// </summary>
public sealed class FlagRowModel
{
    public required string Name { get; init; }

    /// <summary>The value derived from configuration / underlying filters, ignoring developer overrides.</summary>
    public required bool ConfiguredValue { get; init; }

    /// <summary>The developer override for this flag, if one is set; <c>null</c> means "no override".</summary>
    public bool? OverrideValue { get; init; }

    /// <summary>The effective value after applying any override.</summary>
    public required bool EffectiveValue { get; init; }
}
