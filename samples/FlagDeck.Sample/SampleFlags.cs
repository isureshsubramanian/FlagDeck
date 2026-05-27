namespace FlagDeck.Sample;

/// <summary>
/// Strongly-typed names for the feature flags configured in <c>appsettings.json</c>.
/// </summary>
public static class SampleFlags
{
    /// <summary>Simple boolean — swap between two implementations of IGreetingService.</summary>
    public const string NewGreeting = "NewGreeting";

    /// <summary>PercentageFilter — only 25% of requests see the beta UI by default.</summary>
    public const string BetaUI = "BetaUI";

    /// <summary>TimeWindowFilter — banner appears only between configured dates.</summary>
    public const string HolidayBanner = "HolidayBanner";

    /// <summary>TargetingFilter — opt specific users / groups into internal tooling.</summary>
    public const string InternalTools = "InternalTools";

    /// <summary>Variant flag — controls which checkout experience a user lands in.</summary>
    public const string CheckoutExperience = "CheckoutExperience";

    /// <summary>Boolean — gates a [FeatureGate] attribute on the controller.</summary>
    public const string ReportsApi = "ReportsApi";

    /// <summary>Boolean — drives a UseMiddlewareForFeature pipeline branch.</summary>
    public const string MaintenanceMode = "MaintenanceMode";

    /// <summary>Boolean — flips business logic between v1 and v2 in /api/orders.</summary>
    public const string OrdersV2 = "OrdersV2";
}
