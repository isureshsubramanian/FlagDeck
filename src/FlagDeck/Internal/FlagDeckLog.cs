using Microsoft.Extensions.Logging;

namespace FlagDeck.Internal;

/// <summary>
/// Source-generated logging delegates. Centralizing them here keeps call sites cheap
/// (no boxing of args, no string formatting unless the level is enabled) and satisfies
/// the CA1848/CA1873 analyzer rules.
/// </summary>
internal static partial class FlagDeckLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "FlagDeck dashboard not mapped: environment '{Environment}' is not in the allow-list and AllowInProduction is false.")]
    public static partial void DashboardNotMapped(ILogger logger, string environment);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "FlagDeck dashboard is mapped in environment '{Environment}' without an authorization policy. Set FlagDeckOptions.AuthorizationPolicy to restrict access.")]
    public static partial void DashboardMissingAuth(ILogger logger, string environment);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "FlagDeck dashboard mapped at {Prefix} for environment '{Environment}'.")]
    public static partial void DashboardMapped(ILogger logger, string prefix, string environment);
}
