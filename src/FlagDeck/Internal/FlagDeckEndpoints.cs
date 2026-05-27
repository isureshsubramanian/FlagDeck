using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using FlagDeck.Components;
using FlagDeck.Overrides;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

namespace FlagDeck.Internal;

/// <summary>
/// Endpoint handlers for the dashboard. Mapped under the configured prefix by
/// <see cref="FlagDeckEndpointRouteBuilderExtensions.MapFlagDeck"/>.
/// </summary>
internal static class FlagDeckEndpoints
{
    public static void MapAll(IEndpointRouteBuilder group)
    {
        // Cast to Delegate so Minimal API overload resolution does NOT fall back to the legacy
        // (string, RequestDelegate) overload (which would discard the Task<IResult> return value
        // and trip ASP0016).
        group.MapGet("/", (Delegate)GetDashboardAsync).WithName("FlagDeck.Dashboard");
        group.MapPost("/toggle", (Delegate)ToggleAsync).WithName("FlagDeck.Toggle");
        group.MapPost("/clear", (Delegate)ClearAsync).WithName("FlagDeck.Clear");
        group.MapPost("/theme", (Delegate)SetThemeAsync).WithName("FlagDeck.Theme");

        group.MapGet("/api/flags", (Delegate)GetFlagsJsonAsync).WithName("FlagDeck.Api.GetFlags");
        group.MapPost("/api/flags/{name}", (Delegate)SetFlagJsonAsync).WithName("FlagDeck.Api.SetFlag");
        group.MapDelete("/api/flags/{name}", (Delegate)ClearFlagJsonAsync).WithName("FlagDeck.Api.ClearFlag");

        group.MapGet("/assets/flagdeck.css", () => GetAssetAsync("flagdeck.css", "text/css; charset=utf-8")).WithName("FlagDeck.Css");
        group.MapGet("/assets/flagdeck.js", () => GetAssetAsync("flagdeck.js", "application/javascript; charset=utf-8")).WithName("FlagDeck.Js");
    }

    // ----- HTML dashboard -----

    private static async Task<IResult> GetDashboardAsync(HttpContext http)
    {
        var sp = http.RequestServices;
        var options = sp.GetRequiredService<IOptions<FlagDeckOptions>>().Value;
        var env = sp.GetRequiredService<IHostEnvironment>();
        var featureManager = sp.GetRequiredService<IFeatureManager>();
        var overrides = sp.GetRequiredService<IFeatureFlagOverrideStore>();
        var renderer = sp.GetRequiredService<DashboardRenderer>();

        var flags = await CollectFlagsAsync(featureManager, overrides);
        var theme = ReadTheme(http, options);
        var query = http.Request.Query["q"].ToString();

        var model = new DashboardModel
        {
            Title = options.DashboardTitle,
            Subtitle = options.DashboardSubtitle ?? env.EnvironmentName,
            EnvironmentName = env.EnvironmentName,
            Theme = theme,
            PathPrefix = FlagDeckEndpointRouteBuilderExtensions.NormalizePrefix(options.PathPrefix),
            Flags = flags,
            Search = query,
            OverrideCount = overrides.GetAll().Count,
            Version = typeof(FlagDeckOptions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
        };

        var html = await renderer.RenderDashboardAsync(model);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static async Task<IResult> ToggleAsync(HttpContext http,
        IFeatureFlagOverrideStore overrides,
        IOptions<FlagDeckOptions> options)
    {
        var form = await http.Request.ReadFormAsync();
        var name = form["name"].ToString();
        var stateRaw = form["state"].ToString();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest("missing 'name'");
        }

        bool? next = stateRaw switch
        {
            "on" => true,
            "off" => false,
            "clear" or "" => null,
            _ => null,
        };

        overrides.Set(name, next);

        var prefix = FlagDeckEndpointRouteBuilderExtensions.NormalizePrefix(options.Value.PathPrefix);
        var redirect = http.Request.Headers.Referer.ToString();
        if (string.IsNullOrEmpty(redirect) || !redirect.Contains(prefix, StringComparison.Ordinal))
        {
            redirect = prefix + "/";
        }
        return Results.Redirect(redirect);
    }

    private static IResult ClearAsync(HttpContext http,
        IFeatureFlagOverrideStore overrides,
        IOptions<FlagDeckOptions> options)
    {
        overrides.Clear();
        var prefix = FlagDeckEndpointRouteBuilderExtensions.NormalizePrefix(options.Value.PathPrefix);
        return Results.Redirect(prefix + "/");
    }

    private static async Task<IResult> SetThemeAsync(HttpContext http,
        IOptions<FlagDeckOptions> options)
    {
        var form = await http.Request.ReadFormAsync();
        var raw = form["theme"].ToString();
        var theme = string.Equals(raw, "light", StringComparison.OrdinalIgnoreCase)
            ? FlagDeckTheme.Light
            : FlagDeckTheme.Dark;

        var opts = options.Value;
        http.Response.Cookies.Append(opts.ThemeCookieName, theme == FlagDeckTheme.Light ? "light" : "dark", new CookieOptions
        {
            Path = "/",
            HttpOnly = false, // theme is non-sensitive; allow JS to read for instant toggle if desired
            SameSite = SameSiteMode.Lax,
            Secure = http.Request.IsHttps,
            MaxAge = opts.CookieMaxAge,
            Expires = opts.CookieMaxAge > TimeSpan.Zero ? DateTimeOffset.UtcNow.Add(opts.CookieMaxAge) : null,
            IsEssential = true,
        });

        var prefix = FlagDeckEndpointRouteBuilderExtensions.NormalizePrefix(opts.PathPrefix);
        return Results.Redirect(prefix + "/");
    }

    // ----- JSON API -----

    private static async Task<IResult> GetFlagsJsonAsync(HttpContext http)
    {
        var sp = http.RequestServices;
        var featureManager = sp.GetRequiredService<IFeatureManager>();
        var overrides = sp.GetRequiredService<IFeatureFlagOverrideStore>();
        var flags = await CollectFlagsAsync(featureManager, overrides);
        return Results.Json(flags.Select(f => new
        {
            name = f.Name,
            configured = f.ConfiguredValue,
            overridden = f.OverrideValue,
            effective = f.EffectiveValue,
        }));
    }

    private static async Task<IResult> SetFlagJsonAsync(string name, HttpContext http, IFeatureFlagOverrideStore overrides)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { error = "missing flag name" });
        }
        var body = await JsonSerializer.DeserializeAsync<SetFlagBody>(http.Request.Body, JsonOpts);
        if (body is null)
        {
            return Results.BadRequest(new { error = "expected JSON body { \"enabled\": true|false|null }" });
        }
        overrides.Set(name, body.Enabled);
        return Results.Ok(new { name, enabled = body.Enabled });
    }

    private static IResult ClearFlagJsonAsync(string name, IFeatureFlagOverrideStore overrides)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { error = "missing flag name" });
        }
        overrides.Set(name, null);
        return Results.NoContent();
    }

    // ----- Static assets -----

    private static IResult GetAssetAsync(string resourceName, string contentType)
    {
        var asm = typeof(FlagDeckEndpoints).Assembly;
        var full = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + resourceName, StringComparison.Ordinal));
        if (full is null)
        {
            return Results.NotFound();
        }
        var stream = asm.GetManifestResourceStream(full);
        if (stream is null)
        {
            return Results.NotFound();
        }
        return Results.Stream(stream, contentType);
    }

    // ----- Helpers -----

    internal static async Task<IReadOnlyList<FlagRowModel>> CollectFlagsAsync(
        IFeatureManager featureManager,
        IFeatureFlagOverrideStore overrideStore)
    {
        var rows = new List<FlagRowModel>();
        var overrides = overrideStore.GetAll();

        // The decorator forwards GetFeatureNamesAsync to the underlying manager, so we see
        // every flag declared in configuration. To compute the "configured" value we need to
        // bypass overrides — query the inner manager directly. We do that by temporarily
        // hiding overrides while we ask the decorator: instead we ask via a fresh probe.
        await foreach (var name in featureManager.GetFeatureNamesAsync())
        {
            bool effective = await featureManager.IsEnabledAsync(name);
            bool? overrideValue = overrides.TryGetValue(name, out var ov) ? ov : null;
            bool configured;
            if (overrideValue is null)
            {
                configured = effective;
            }
            else
            {
                // Compute the "configured" value by asking the inner manager bypassing overrides.
                // We do that by reading the configured state via a dedicated probe service if available;
                // otherwise fall back to "unknown == effective ignoring override".
                configured = await ProbeConfiguredAsync(featureManager, name, overrideValue.Value);
            }
            rows.Add(new FlagRowModel
            {
                Name = name,
                ConfiguredValue = configured,
                OverrideValue = overrideValue,
                EffectiveValue = effective,
            });
        }

        rows.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private static async Task<bool> ProbeConfiguredAsync(IFeatureManager fm, string name, bool overrideValue)
    {
        // When there's an override, we can't trivially see through it. The pragmatic answer:
        // surface "configured == NOT override" only if the override flips the result; otherwise the
        // configured value equals the effective value. The dashboard renders this as informational
        // text and is intentionally conservative — for an authoritative configured value, the dashboard
        // calls the inner manager via reflection on the decorator (see FlagDeckFeatureManager).
        if (fm is FlagDeckFeatureManager decorator)
        {
            var inner = decorator.UnderlyingLegacy;
            return await inner.IsEnabledAsync(name);
        }
        return !overrideValue;
    }

    private static FlagDeckTheme ReadTheme(HttpContext http, FlagDeckOptions options)
    {
        var raw = http.Request.Cookies[options.ThemeCookieName];
        return raw switch
        {
            "light" => FlagDeckTheme.Light,
            "dark" => FlagDeckTheme.Dark,
            _ => options.DefaultTheme,
        };
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private sealed class SetFlagBody
    {
        public bool? Enabled { get; set; }
    }
}
