using System;
using System.Linq;
using FlagDeck.Internal;
using FlagDeck.Overrides;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.FeatureManagement;

namespace FlagDeck;

/// <summary>
/// Extension methods that register FlagDeck services on <see cref="IServiceCollection"/>.
/// </summary>
public static class FlagDeckServiceCollectionExtensions
{
    /// <summary>
    /// Configuration section name FlagDeck binds <see cref="FlagDeckOptions"/> from.
    /// Override per call via the overload that accepts a section name.
    /// </summary>
    public const string DefaultConfigurationSection = "FlagDeck";

    /// <summary>
    /// Registers the FlagDeck developer dashboard services. Call this AFTER
    /// <c>services.AddFeatureManagement()</c> so FlagDeck can decorate the registered
    /// <see cref="IFeatureManager"/> with cookie-based overrides.
    /// </summary>
    /// <remarks>
    /// <see cref="FlagDeckOptions"/> is bound from the <c>FlagDeck</c> section of
    /// <c>IConfiguration</c> automatically. The optional <paramref name="configure"/>
    /// callback runs AFTER configuration binding, so code-level overrides win.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to customize <see cref="FlagDeckOptions"/> in code.</param>
    public static IServiceCollection AddFlagDeck(
        this IServiceCollection services,
        Action<FlagDeckOptions>? configure = null)
        => AddFlagDeck(services, DefaultConfigurationSection, configure);

    /// <summary>
    /// Registers the FlagDeck developer dashboard services and binds <see cref="FlagDeckOptions"/>
    /// from a specific configuration section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationSectionName">
    /// Configuration section to bind from (e.g. <c>"FlagDeck"</c>). Pass <c>null</c> or empty to
    /// skip configuration binding entirely and rely solely on the callback / defaults.
    /// </param>
    /// <param name="configure">Optional callback that runs after configuration binding.</param>
    public static IServiceCollection AddFlagDeck(
        this IServiceCollection services,
        string? configurationSectionName,
        Action<FlagDeckOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<FlagDeckOptions>();

        if (!string.IsNullOrWhiteSpace(configurationSectionName))
        {
            // Bind from IConfiguration; reacts to reload on change.
            optionsBuilder.BindConfiguration(configurationSectionName);
        }

        // Defaults: populate the env allow-list if neither configuration nor the callback set it.
        optionsBuilder.PostConfigure(opts =>
        {
            if (opts.AllowedEnvironments.Count == 0)
            {
                opts.AllowedEnvironments.Add("Development");
                opts.AllowedEnvironments.Add("Staging");
                opts.AllowedEnvironments.Add("Test");
                opts.AllowedEnvironments.Add("QA");
                opts.AllowedEnvironments.Add("Local");
            }
        });

        if (configure is not null)
        {
            // Configure runs BEFORE PostConfigure, so a non-empty allow-list set in code is honored
            // and the default fill above only fires when neither configuration nor code supplied one.
            services.Configure(configure);
        }

        services.AddHttpContextAccessor();
        services.TryAddScoped<IFeatureFlagOverrideStore, CookieFeatureFlagOverrideStore>();

        // Razor static rendering host for the dashboard.
        services.TryAddScoped<HtmlRenderer>();
        services.TryAddScoped<DashboardRenderer>();

        DecorateFeatureManager(services);

        // Sentinel so AddFlagDeck() is safe to call multiple times.
        services.TryAddSingleton<FlagDeckMarker>();

        return services;
    }

    /// <summary>Sentinel service indicating that AddFlagDeck() has already wrapped the feature manager.</summary>
    internal sealed class FlagDeckMarker { }

    /// <summary>
    /// Replaces the existing <see cref="IFeatureManager"/> registration with a FlagDeck decorator
    /// that consults the override store before delegating to the original manager. Idempotent.
    /// </summary>
    private static void DecorateFeatureManager(IServiceCollection services)
    {
        // Already decorated by a previous AddFlagDeck() call → no-op.
        if (services.Any(d => d.ServiceType == typeof(FlagDeckMarker)))
        {
            return;
        }

        var existing = services.LastOrDefault(d => d.ServiceType == typeof(IFeatureManager));
        if (existing is null)
        {
            throw new InvalidOperationException(
                "FlagDeck requires Microsoft.FeatureManagement. "
                + "Call services.AddFeatureManagement(...) before services.AddFlagDeck().");
        }

        // Capture how to construct the inner manager BEFORE we mutate the collection.
        var innerFactory = BuildInnerFactory(existing);

        var lifetime = existing.Lifetime;
        services.Remove(existing);

        // Remove the variant manager registration too; we'll re-register it as a pointer to the decorator.
        var variantDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(IVariantFeatureManager));
        if (variantDescriptor is not null)
        {
            services.Remove(variantDescriptor);
        }

        services.Add(new ServiceDescriptor(typeof(IFeatureManager), sp =>
        {
            var inner = innerFactory(sp);
            var legacy = (IFeatureManager)inner;
            var variants = inner as IVariantFeatureManager
                ?? throw new InvalidOperationException(
                    "FlagDeck expected the underlying Microsoft.FeatureManagement implementation to also "
                    + "implement IVariantFeatureManager, which is the case for FeatureManager in v3+.");
            // Only depend on Singleton-safe services here. The decorator may inherit the inner
            // registration's lifetime (Singleton in Microsoft.FeatureManagement v4.5+), so we
            // cannot capture a Scoped IFeatureFlagOverrideStore — it is resolved per call from
            // HttpContext.RequestServices inside the decorator.
            var http = sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            return new FlagDeckFeatureManager(legacy, variants, http);
        }, lifetime));

        services.Add(new ServiceDescriptor(typeof(IVariantFeatureManager), sp =>
            (IVariantFeatureManager)sp.GetRequiredService<IFeatureManager>(), lifetime));
    }

    private static Func<IServiceProvider, object> BuildInnerFactory(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            var instance = descriptor.ImplementationInstance;
            return _ => instance;
        }
        if (descriptor.ImplementationFactory is not null)
        {
            var factory = descriptor.ImplementationFactory;
            return factory;
        }
        var implType = descriptor.ImplementationType
            ?? throw new InvalidOperationException("ServiceDescriptor has neither implementation type, factory, nor instance.");
        return sp => ActivatorUtilities.CreateInstance(sp, implType);
    }
}
