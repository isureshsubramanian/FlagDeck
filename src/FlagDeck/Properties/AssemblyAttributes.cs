using System.Diagnostics.CodeAnalysis;

// FlagDeck.Overrides — kept despite CA1716 because renaming would touch every consumer of
// IFeatureFlagOverrideStore for the sake of VB.NET interop, which is not a target audience.
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Scope = "namespace", Target = "~N:FlagDeck.Overrides",
    Justification = "C# / F# library — VB.NET interop friction is acceptable here.")]
