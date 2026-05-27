using System;
using System.Collections.Generic;

namespace FlagDeck;

/// <summary>
/// Configuration for the FlagDeck developer dashboard.
/// </summary>
public sealed class FlagDeckOptions
{
    /// <summary>
    /// Path prefix that the dashboard endpoints are mapped at. Defaults to <c>/_flagdeck</c>.
    /// The prefix MUST begin with a forward slash and MUST NOT end with one.
    /// </summary>
    public string PathPrefix { get; set; } = "/_flagdeck";

    /// <summary>
    /// Title shown in the dashboard header. Defaults to <c>"FlagDeck"</c>.
    /// </summary>
    public string DashboardTitle { get; set; } = "FlagDeck";

    /// <summary>
    /// Optional subtitle (often the application name or environment).
    /// If null, the dashboard renders the current <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/>.
    /// </summary>
    public string? DashboardSubtitle { get; set; }

    /// <summary>
    /// Environment names where the dashboard is allowed to be mapped.
    /// Comparison is case-insensitive. Defaults to every environment EXCEPT <c>Production</c>.
    /// To explicitly allow the dashboard in Production set <see cref="AllowInProduction"/> to <c>true</c>.
    /// </summary>
    public ISet<string> AllowedEnvironments { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When <c>true</c>, the dashboard is allowed to map in the <c>Production</c> environment.
    /// Defaults to <c>false</c>. Use with extreme care — typically only behind an authenticated admin policy.
    /// </summary>
    public bool AllowInProduction { get; set; }

    /// <summary>
    /// When set, the dashboard endpoint group is decorated with <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/>
    /// referencing this policy name. Use this in non-Development environments to gate access to the dashboard.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    /// Cookie name used to persist developer overrides. Defaults to <c>flagdeck.overrides</c>.
    /// </summary>
    public string OverrideCookieName { get; set; } = "flagdeck.overrides";

    /// <summary>
    /// Cookie name used to persist the user's preferred theme (<c>light</c> or <c>dark</c>).
    /// </summary>
    public string ThemeCookieName { get; set; } = "flagdeck.theme";

    /// <summary>
    /// Default theme when no preference has been recorded. Defaults to <see cref="FlagDeckTheme.Dark"/>.
    /// </summary>
    public FlagDeckTheme DefaultTheme { get; set; } = FlagDeckTheme.Dark;

    /// <summary>
    /// Override cookie lifetime. Defaults to 7 days. Set to <c>TimeSpan.Zero</c> for a session cookie.
    /// </summary>
    public TimeSpan CookieMaxAge { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Available dashboard themes.
/// </summary>
public enum FlagDeckTheme
{
    /// <summary>Light theme (executive, on white).</summary>
    Light = 0,

    /// <summary>Dark theme (executive, on charcoal).</summary>
    Dark = 1,
}
