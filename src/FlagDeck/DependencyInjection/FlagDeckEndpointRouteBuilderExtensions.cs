using System;
using FlagDeck.Internal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlagDeck;

/// <summary>
/// Extension methods that wire up the FlagDeck dashboard endpoints onto an
/// <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class FlagDeckEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the FlagDeck dashboard at the configured path prefix (default <c>/_flagdeck</c>).
    /// </summary>
    /// <remarks>
    /// Honors environment gating: if the current <see cref="IHostEnvironment"/> is Production
    /// and <see cref="FlagDeckOptions.AllowInProduction"/> is not set, no endpoints are mapped
    /// and the method becomes a no-op. The same applies to any environment not listed in
    /// <see cref="FlagDeckOptions.AllowedEnvironments"/>.
    /// </remarks>
    public static IEndpointConventionBuilder MapFlagDeck(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var env = endpoints.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<FlagDeckOptions>>().Value;
        var logger = endpoints.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("FlagDeck");

        if (!IsEnvironmentAllowed(env, options))
        {
            FlagDeckLog.DashboardNotMapped(logger, env.EnvironmentName);
            return new NullEndpointConventionBuilder();
        }

        var prefix = NormalizePrefix(options.PathPrefix);
        var group = endpoints.MapGroup(prefix).WithDisplayName("FlagDeck");

        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicy))
        {
            group.RequireAuthorization(new AuthorizeAttribute { Policy = options.AuthorizationPolicy });
        }
        else if (!env.IsDevelopment())
        {
            FlagDeckLog.DashboardMissingAuth(logger, env.EnvironmentName);
        }

        FlagDeckEndpoints.MapAll(group);

        FlagDeckLog.DashboardMapped(logger, prefix, env.EnvironmentName);
        return group;
    }

    internal static bool IsEnvironmentAllowed(IHostEnvironment env, FlagDeckOptions options)
    {
        if (env.IsProduction())
        {
            return options.AllowInProduction;
        }
        if (options.AllowedEnvironments.Count == 0)
        {
            // Empty allow-list means "everything except Production".
            return true;
        }
        return options.AllowedEnvironments.Contains(env.EnvironmentName);
    }

    internal static string NormalizePrefix(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "/_flagdeck";
        }
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }
        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed[..^1];
        }
        return trimmed;
    }

    /// <summary>
    /// Returned when the environment is not allowed — turns <c>MapFlagDeck</c> into a true no-op
    /// while still giving the caller a chainable convention builder.
    /// </summary>
    private sealed class NullEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention) { }

        public void Finally(Action<EndpointBuilder> finallyConvention) { }
    }
}
