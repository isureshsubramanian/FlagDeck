using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlagDeck.Overrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

namespace FlagDeck.Internal;

/// <summary>
/// Decorates the underlying <see cref="IFeatureManager"/> / <see cref="IVariantFeatureManager"/> so
/// developer-supplied cookie overrides win over configured definitions when present.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft.FeatureManagement registers <c>IFeatureManager</c> as a Singleton. To stay safely
/// resolvable from any lifetime, this decorator depends only on <see cref="IHttpContextAccessor"/>
/// (also Singleton-safe) and resolves the per-request <see cref="IFeatureFlagOverrideStore"/>
/// from <c>HttpContext.RequestServices</c> on each call. Outside an HTTP request, no override
/// is applied and the inner manager is consulted as-is.
/// </para>
/// </remarks>
internal sealed class FlagDeckFeatureManager : IFeatureManager, IVariantFeatureManager
{
    private readonly IFeatureManager _legacy;
    private readonly IVariantFeatureManager _variants;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FlagDeckFeatureManager(
        IFeatureManager legacy,
        IVariantFeatureManager variants,
        IHttpContextAccessor httpContextAccessor)
    {
        _legacy = legacy;
        _variants = variants;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// The underlying feature manager, bypassing the override store. Used by the dashboard to
    /// surface the "configured" value alongside the effective (post-override) value.
    /// </summary>
    internal IFeatureManager UnderlyingLegacy => _legacy;

    /// <summary>
    /// The underlying variant manager, bypassing the override store.
    /// </summary>
    internal IVariantFeatureManager UnderlyingVariants => _variants;

    /// <summary>
    /// Resolves the per-request override store, or returns <c>null</c> when called outside
    /// of an HTTP request (background tasks, startup, etc).
    /// </summary>
    private bool? TryGetOverride(string feature)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null) return null;
        var store = ctx.RequestServices.GetService<IFeatureFlagOverrideStore>();
        return store?.Get(feature);
    }

    // ----- IFeatureManager -----

    public IAsyncEnumerable<string> GetFeatureNamesAsync() => _legacy.GetFeatureNamesAsync();

    public Task<bool> IsEnabledAsync(string feature)
    {
        var ov = TryGetOverride(feature);
        return ov.HasValue ? Task.FromResult(ov.Value) : _legacy.IsEnabledAsync(feature);
    }

    public Task<bool> IsEnabledAsync<TContext>(string feature, TContext context)
    {
        var ov = TryGetOverride(feature);
        return ov.HasValue ? Task.FromResult(ov.Value) : _legacy.IsEnabledAsync(feature, context);
    }

    // ----- IVariantFeatureManager -----

    public IAsyncEnumerable<string> GetFeatureNamesAsync(CancellationToken cancellationToken)
        => _variants.GetFeatureNamesAsync(cancellationToken);

    public ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken)
    {
        var ov = TryGetOverride(feature);
        return ov.HasValue ? new ValueTask<bool>(ov.Value) : _variants.IsEnabledAsync(feature, cancellationToken);
    }

    public ValueTask<bool> IsEnabledAsync<TContext>(string feature, TContext context, CancellationToken cancellationToken)
    {
        var ov = TryGetOverride(feature);
        return ov.HasValue ? new ValueTask<bool>(ov.Value) : _variants.IsEnabledAsync(feature, context, cancellationToken);
    }

    public ValueTask<Variant> GetVariantAsync(string feature, CancellationToken cancellationToken)
    {
        var ov = TryGetOverride(feature);
        // Forced OFF: the flag is disabled, so no variant is allocated. We return null to mirror
        // Microsoft.FeatureManagement's behavior when DefaultWhenDisabled isn't configured.
        // Forced ON or no override: defer to the inner manager.
        return ov is false
            ? new ValueTask<Variant>((Variant)null!)
            : _variants.GetVariantAsync(feature, cancellationToken);
    }

    public ValueTask<Variant> GetVariantAsync(string feature, ITargetingContext context, CancellationToken cancellationToken)
    {
        var ov = TryGetOverride(feature);
        return ov is false
            ? new ValueTask<Variant>((Variant)null!)
            : _variants.GetVariantAsync(feature, context, cancellationToken);
    }
}
