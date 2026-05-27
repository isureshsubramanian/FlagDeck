using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace FlagDeck.Overrides;

/// <summary>
/// Stores per-developer feature flag overrides. The default implementation persists
/// overrides in a cookie scoped to the current browser, so each developer can flip
/// flags independently without affecting other users of the same environment.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Get/Set are the natural, idiomatic verbs for an override store. "
                    + "FlagDeck targets C# / F# consumers and the cost of degrading the API "
                    + "for hypothetical VB.NET overriders outweighs the benefit.")]
public interface IFeatureFlagOverrideStore
{
    /// <summary>
    /// Returns the override for <paramref name="flagName"/>, or <c>null</c> if no override is set
    /// (in which case the underlying <see cref="Microsoft.FeatureManagement.IFeatureManager"/>
    /// configuration is honored).
    /// </summary>
    bool? Get(string flagName);

    /// <summary>
    /// Sets the override for <paramref name="flagName"/>. Passing <c>null</c> clears the override.
    /// </summary>
    void Set(string flagName, bool? value);

    /// <summary>
    /// Returns every override currently active for the caller.
    /// </summary>
    IReadOnlyDictionary<string, bool> GetAll();

    /// <summary>
    /// Removes every override.
    /// </summary>
    void Clear();
}
