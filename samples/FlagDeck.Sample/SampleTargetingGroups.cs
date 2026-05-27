namespace FlagDeck.Sample;

/// <summary>
/// Cached group arrays passed to <see cref="Microsoft.FeatureManagement.FeatureFilters.TargetingContext"/>.
/// Hoisting these out of the request hot path satisfies CA1861 (don't allocate constant arrays per call).
/// </summary>
internal static class SampleTargetingGroups
{
    public static readonly string[] Users = ["users"];
    public static readonly string[] Internal = ["internal", "users"];
}
