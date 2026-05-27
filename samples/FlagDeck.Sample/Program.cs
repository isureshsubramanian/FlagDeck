using System;
using System.Linq;
using System.Threading.Tasks;
using FlagDeck;
using FlagDeck.Sample;
using FlagDeck.Sample.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Register Microsoft.FeatureManagement WITH the built-in filters we need
//    so the sample can demonstrate Percentage / TimeWindow / Targeting flags.
// ---------------------------------------------------------------------------
builder.Services
    .AddFeatureManagement()
    .AddFeatureFilter<PercentageFilter>()
    .AddFeatureFilter<TimeWindowFilter>()
    .AddFeatureFilter<ContextualTargetingFilter>()
    .WithTargeting<SampleTargetingContextAccessor>();

// ---------------------------------------------------------------------------
// 2. Register FlagDeck on top of feature management. By default this binds
//    FlagDeckOptions from the "FlagDeck" section of appsettings.json — see
//    appsettings.json for the values driving the dashboard's title, theme,
//    allowed environments, etc. Pass a callback if you want to override in code.
// ---------------------------------------------------------------------------
builder.Services.AddFlagDeck();

// ---------------------------------------------------------------------------
// 3. Demonstrate flag-gated DI: pick one of two IGreetingService implementations
//    at request time based on the "NewGreeting" flag.
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ClassicGreetingService>();
builder.Services.AddScoped<NewGreetingService>();
builder.Services.AddScoped<IGreetingService>(sp =>
{
    var fm = sp.GetRequiredService<IFeatureManager>();
    // IFeatureManager calls are async; we resolve once per request so .Result is safe in this hot path,
    // but in production code prefer awaiting in an async accessor. Kept synchronous here for sample clarity.
    var on = fm.IsEnabledAsync(SampleFlags.NewGreeting).GetAwaiter().GetResult();
    return on
        ? sp.GetRequiredService<NewGreetingService>()
        : sp.GetRequiredService<ClassicGreetingService>();
});

builder.Services.AddScoped<SampleTargetingContextAccessor>();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// ---------------------------------------------------------------------------
// 4. Conditional middleware: only runs when "MaintenanceMode" is on.
//    Toggle it in FlagDeck and watch the response change without restarting.
// ---------------------------------------------------------------------------
app.UseMiddlewareForFeature<MaintenanceModeMiddleware>(SampleFlags.MaintenanceMode);

// ---------------------------------------------------------------------------
// 5. Map FlagDeck. No-op in Production unless AllowInProduction is set.
// ---------------------------------------------------------------------------
app.MapFlagDeck();

app.MapControllers();

// ---------------------------------------------------------------------------
// 6. Minimal API endpoints showcasing each flag scenario.
// ---------------------------------------------------------------------------

app.MapGet("/", () => Results.Content(SampleHtml.Index(app.Environment.EnvironmentName), "text/html"));

// Scenario A: simple boolean flag, checked programmatically.
app.MapGet("/api/welcome", async ([FromServices] IGreetingService greeter) =>
    Results.Ok(new { message = greeter.Greet() }));

// Scenario B: percentage rollout.
app.MapGet("/api/beta", async ([FromServices] IFeatureManager fm) =>
{
    var enabled = await fm.IsEnabledAsync(SampleFlags.BetaUI);
    return Results.Ok(new
    {
        flag = SampleFlags.BetaUI,
        enabled,
        note = "Configured as a 25% percentage rollout. Override in FlagDeck to force on/off."
    });
});

// Scenario C: time-window flag.
app.MapGet("/api/holiday-banner", async ([FromServices] IFeatureManager fm) =>
{
    var enabled = await fm.IsEnabledAsync(SampleFlags.HolidayBanner);
    return Results.Ok(new
    {
        flag = SampleFlags.HolidayBanner,
        enabled,
        note = "TimeWindow filter; configured to be on during the demo window in appsettings.json."
    });
});

// Scenario D: targeted/contextual flag.
app.MapGet("/api/internal-tools", async ([FromServices] IFeatureManager fm, [FromQuery] string? user) =>
{
    var ctx = new TargetingContext
    {
        UserId = user ?? "guest",
        Groups = user is null ? Array.Empty<string>() : SampleTargetingGroups.Users,
    };
    var enabled = await fm.IsEnabledAsync(SampleFlags.InternalTools, ctx);
    return Results.Ok(new
    {
        flag = SampleFlags.InternalTools,
        user = ctx.UserId,
        enabled,
        note = "Targets users in the 'internal' group OR specific named users. Override in FlagDeck to force the result."
    });
});

// Scenario E: variant feature flag (A/B/C bucket).
app.MapGet("/api/checkout-experience", async ([FromServices] IVariantFeatureManager vfm) =>
{
    var enabled = await vfm.IsEnabledAsync(SampleFlags.CheckoutExperience, CancellationToken.None);
    var variant = enabled
        ? await vfm.GetVariantAsync(SampleFlags.CheckoutExperience, CancellationToken.None)
        : null;
    return Results.Ok(new
    {
        flag = SampleFlags.CheckoutExperience,
        enabled,
        variant = variant?.Name,
        configuration = variant?.Configuration?.Value,
        note = enabled
            ? "Flag is enabled — variant selected by configured allocation."
            : "Flag is disabled (either configured off or forced off via FlagDeck)."
    });
});

// Scenario F: programmatic gating in business logic.
app.MapPost("/api/orders", async ([FromServices] IFeatureManager fm) =>
{
    if (!await fm.IsEnabledAsync(SampleFlags.OrdersV2))
    {
        return Results.Ok(new { engine = "OrdersV1", note = "Legacy code path still in use." });
    }
    return Results.Ok(new { engine = "OrdersV2", note = "New code path active. Toggle OrdersV2 in FlagDeck to compare." });
});

app.Run();

public partial class Program; // exposed for WebApplicationFactory in tests
