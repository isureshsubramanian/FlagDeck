using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FlagDeck.Overrides;

/// <summary>
/// Default <see cref="IFeatureFlagOverrideStore"/> backed by a per-browser cookie. The cookie
/// payload is a compact JSON map of <c>{ flagName: bool }</c>, base64url-encoded.
/// </summary>
/// <remarks>
/// <para>
/// This store is registered <c>Scoped</c> — one instance per HTTP request. The first call to
/// <see cref="Get"/> / <see cref="GetAll"/> reads the request cookie and caches the decoded
/// map; every subsequent call within the same request uses the in-memory cache. Any
/// <see cref="Set"/> / <see cref="Clear"/> updates the cache <em>and</em> writes the
/// corresponding <c>Set-Cookie</c> response header. We deliberately do NOT re-read the request
/// cookie after a mutation — the request cookie is fixed for the lifetime of the request, so
/// "what did I just set" must come from the cache, not from re-parsing the inbound header.
/// </para>
/// <para>
/// The cookie is HttpOnly: overrides are read server-side during feature evaluation, so JS
/// doesn't need direct access.
/// </para>
/// </remarks>
internal sealed class CookieFeatureFlagOverrideStore : IFeatureFlagOverrideStore
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptionsMonitor<FlagDeckOptions> _options;

    // Null = not yet loaded. Non-null = authoritative map for this request scope.
    private Dictionary<string, bool>? _cache;

    public CookieFeatureFlagOverrideStore(
        IHttpContextAccessor httpContextAccessor,
        IOptionsMonitor<FlagDeckOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options;
    }

    public bool? Get(string flagName)
    {
        ArgumentException.ThrowIfNullOrEmpty(flagName);
        var overrides = LoadOrCache();
        return overrides.TryGetValue(flagName, out var value) ? value : null;
    }

    public void Set(string flagName, bool? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(flagName);
        var current = new Dictionary<string, bool>(LoadOrCache(), StringComparer.Ordinal);
        if (value is null)
        {
            current.Remove(flagName);
        }
        else
        {
            current[flagName] = value.Value;
        }
        WriteCookie(current);
    }

    public IReadOnlyDictionary<string, bool> GetAll() => LoadOrCache();

    public void Clear()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            // No request — just blow away the cache so any subsequent reads in this scope are empty.
            _cache = new Dictionary<string, bool>(StringComparer.Ordinal);
            return;
        }

        var opts = _options.CurrentValue;
        ctx.Response.Cookies.Delete(opts.OverrideCookieName, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
        });

        // Cache is now authoritative: empty for the remainder of this request scope.
        _cache = new Dictionary<string, bool>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the cached override map, lazily loading it from the request cookie on first call.
    /// Once loaded (or mutated), the cache is authoritative for the rest of the request.
    /// </summary>
    private Dictionary<string, bool> LoadOrCache()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            return EmptyOverrides;
        }

        var raw = ctx.Request.Cookies[_options.CurrentValue.OverrideCookieName];
        if (string.IsNullOrEmpty(raw))
        {
            _cache = new Dictionary<string, bool>(StringComparer.Ordinal);
        }
        else
        {
            _cache = TryDecode(raw) ?? new Dictionary<string, bool>(StringComparer.Ordinal);
        }
        return _cache;
    }

    private void WriteCookie(Dictionary<string, bool> overrides)
    {
        var ctx = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("CookieFeatureFlagOverrideStore.Set requires an active HttpContext.");
        var opts = _options.CurrentValue;

        var cookieOptions = new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            IsEssential = true,
        };
        if (opts.CookieMaxAge > TimeSpan.Zero)
        {
            cookieOptions.MaxAge = opts.CookieMaxAge;
            cookieOptions.Expires = DateTimeOffset.UtcNow.Add(opts.CookieMaxAge);
        }

        if (overrides.Count == 0)
        {
            ctx.Response.Cookies.Delete(opts.OverrideCookieName, cookieOptions);
        }
        else
        {
            var encoded = Encode(overrides);
            ctx.Response.Cookies.Append(opts.OverrideCookieName, encoded, cookieOptions);
        }

        // Replace cache with a fresh defensive copy of the new state.
        _cache = new Dictionary<string, bool>(overrides, StringComparer.Ordinal);
    }

    internal static string Encode(IReadOnlyDictionary<string, bool> overrides)
    {
        var dict = overrides as Dictionary<string, bool>
                   ?? new Dictionary<string, bool>(overrides, StringComparer.Ordinal);
        var json = JsonSerializer.Serialize(dict, OverrideJsonContext.Default.OverridesMap);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Base64UrlEncode(bytes);
    }

    internal static Dictionary<string, bool>? TryDecode(string raw)
    {
        try
        {
            var bytes = Base64UrlDecode(raw);
            var dict = JsonSerializer.Deserialize(bytes, OverrideJsonContext.Default.OverridesMap);
            return dict is null ? null : new Dictionary<string, bool>(dict, StringComparer.Ordinal);
        }
        catch (FormatException) { return null; }
        catch (JsonException) { return null; }
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string raw)
    {
        var s = raw.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private static readonly Dictionary<string, bool> EmptyOverrides = new(StringComparer.Ordinal);
}

[SuppressMessage("Design", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the source generator.")]
[JsonSerializable(typeof(Dictionary<string, bool>), TypeInfoPropertyName = "OverridesMap")]
internal sealed partial class OverrideJsonContext : JsonSerializerContext
{
}
