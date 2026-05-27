# FlagDeck

> Embedded Blazor developer dashboard for **Microsoft.FeatureManagement** in ASP.NET Core. Lists every registered feature flag and lets developers toggle them per-browser via cookies — no `appsettings.json` edits, no app restarts.

[![CI](https://github.com/sureshsubramanian/FlagDeck/actions/workflows/ci.yml/badge.svg)](https://github.com/sureshsubramanian/FlagDeck/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/FlagDeck.svg)](https://www.nuget.org/packages/FlagDeck)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dot.net)

## Why

`Microsoft.FeatureManagement` reads flags from configuration. Flipping a flag for local testing means editing `appsettings.json`, restarting Kestrel, and losing context. FlagDeck adds a per-developer override layer on top, surfaced through a clean Blazor dashboard at `/_flagdeck`. The override is just a cookie — every developer gets their own view of the app.

Safe by design: the dashboard refuses to map in `Production` unless you explicitly opt-in.

### "But ASP.NET Core already hot-reloads `appsettings.json` — why do I need this?"

Hot reload is real, and it covers a slice of the problem: edit the file, `IConfiguration` re-binds, the next request sees the new value. That's enough when you're a solo developer on your own machine and willing to edit a tracked file. FlagDeck is for everything hot reload doesn't cover.

The honest contrast:

| Scenario | `appsettings.json` hot reload | FlagDeck |
|---|---|---|
| Two devs running the same app want **different** flag states at the same time | No — the file is shared | Yes — each cookie is per-browser |
| Testing in deployed **Staging / QA** (container, baked-in config) | No file on disk to edit | Cookie works anywhere the app runs |
| Don't want to risk **committing** an `appsettings.json` change | Edit is on disk, easy to miss in `git status` | Nothing on disk changes |
| Flip a flag **mid-test** from a UI | Open file → find key → edit → save | One click |
| Hand a teammate a specific test setup | "Edit your appsettings like this…" | "Hit `/_flagdeck`, force these three flags" |
| **Cloud configuration** (Azure App Configuration, AWS AppConfig) backing your flags | Reloads globally for everyone | Per-browser overlay on top |
| Force yourself into a **TargetingFilter** group you're not in | Can't fake yourself into a group | Override bypasses the gate entirely |
| Bisecting — "is this flag the cause?" — flip on, click around, flip off | Each flip is a file edit | Two clicks |
| Visualize **current** state of every flag at a glance | `grep` JSON / scroll through filters | Dashboard table |

**The one-liner:** hot reload changes the **file** (global, shared, persisted, riskier). FlagDeck changes your **cookie** (personal, ephemeral, safe). They solve different problems.

If you're a solo dev on localhost and editing JSON doesn't bother you, hot reload is enough. The moment you have:

- a shared Staging environment,
- multiple developers/testers on the same app,
- baked-in configuration that you can't edit on the fly,
- cloud-sourced flag values, or
- a need to flip flags repeatedly during a manual test session,

…hot reload stops being sufficient, and FlagDeck does what it's actually for. This is the same reason commercial products like LaunchDarkly and ConfigCat exist on top of perfectly capable cloud configuration systems — the question was never "can you change values?", it was "can you change values **for yourself, instantly, without touching the file**?"

## Install

```bash
dotnet add package FlagDeck
```

Target framework: `net10.0`.

## Quick start

```csharp
using FlagDeck;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddFeatureManagement()
    .AddFeatureFilter<PercentageFilter>();

builder.Services.AddFlagDeck(opts =>
{
    opts.DashboardTitle = "Acme Internal";
});

var app = builder.Build();

app.MapFlagDeck();   // mapped at /_flagdeck — no-op in Production by default
app.MapControllers();
app.Run();
```

Open `http://localhost:5000/_flagdeck` and toggle flags. Every override is scoped to your browser cookie — your teammates' sessions are unaffected.

## What's in the dashboard

- Every flag registered with Microsoft.FeatureManagement
- The **configured** value (from `appsettings.json` and filters)
- Your **override** for that flag (force on, force off, or clear)
- The **effective** value the app will see for your next request
- Search-as-you-type filter (press `/` to focus)
- Theme toggle (dark/light) — executive look on both
- Active environment badge so you always know where you are

## Configuration

`FlagDeckOptions` binds from the `FlagDeck` section of your standard ASP.NET Core configuration (`appsettings.json`, environment variables, user secrets, etc.). You can also override values in code via the `AddFlagDeck(opts => ...)` callback — code wins over configuration.

```json
{
  "FlagDeck": {
    "DashboardTitle": "Acme Internal",
    "DashboardSubtitle": "Engineering toggles",
    "PathPrefix": "/_flagdeck",
    "DefaultTheme": "Dark",
    "AllowInProduction": false,
    "AuthorizationPolicy": null,
    "AllowedEnvironments": [ "Development", "Staging", "QA" ],
    "OverrideCookieName": "flagdeck.overrides",
    "ThemeCookieName": "flagdeck.theme",
    "CookieMaxAge": "7.00:00:00"
  }
}
```

To bind from a different section name, use the overload: `services.AddFlagDeck("DevTools:FeatureFlags");`. Pass `null` for the section name to skip binding and configure entirely in code.

| Option | Default | Description |
| --- | --- | --- |
| `PathPrefix` | `/_flagdeck` | Where the dashboard mounts. Must start with `/`. |
| `DashboardTitle` | `"FlagDeck"` | Heading shown in the dashboard. |
| `DashboardSubtitle` | environment name | Smaller text under the title. |
| `AllowedEnvironments` | every env except `Production` | Set of `IHostEnvironment.EnvironmentName` values where the dashboard is permitted to mount. |
| `AllowInProduction` | `false` | When `true`, the dashboard also mounts in `Production`. Use with an `AuthorizationPolicy`. |
| `AuthorizationPolicy` | `null` | If set, the dashboard endpoint group requires this policy. Recommended for any non-Development environment. |
| `OverrideCookieName` | `flagdeck.overrides` | Cookie used to persist overrides. |
| `ThemeCookieName` | `flagdeck.theme` | Cookie used to persist the user's preferred theme. |
| `DefaultTheme` | `Dark` | Theme shown to first-time visitors. |
| `CookieMaxAge` | `7.00:00:00` (7d) | Lifetime of the override cookie. `00:00:00` = session cookie. |

## How overrides work

`AddFlagDeck()` decorates the registered `IFeatureManager` (and `IVariantFeatureManager`). On every `IsEnabledAsync(flagName)` call:

1. Look up `flagName` in the per-request `IFeatureFlagOverrideStore` (cookie).
2. If an override exists → return it.
3. Otherwise → delegate to the underlying `FeatureManager`.

This means **every** code path that consults `IFeatureManager` honors the override, including:

- Programmatic `IsEnabledAsync` checks in business logic
- `[FeatureGate]` attributes on MVC controllers and Minimal API endpoints
- `UseMiddlewareForFeature<T>` conditional middleware
- Razor `<feature>` tag helpers
- DI factories that read flags at resolution time

Variant selection (`GetVariantAsync`) is **not** affected by the on/off override — flipping a variant flag on enables it; the variant still comes from the inner manager's allocation rules.

## Sample / reference application

The repo ships a runnable reference app under [`samples/FlagDeck.Sample`](samples/FlagDeck.Sample/) that exercises every common Microsoft.FeatureManagement scenario:

| Endpoint | Flag | Scenario |
| --- | --- | --- |
| `/api/welcome` | `NewGreeting` | Simple boolean → swap DI implementations |
| `/api/beta` | `BetaUI` | Percentage rollout (25%) |
| `/api/holiday-banner` | `HolidayBanner` | TimeWindow filter |
| `/api/internal-tools?user=alice` | `InternalTools` | Targeting filter (groups / users) |
| `/api/checkout-experience` | `CheckoutExperience` | Variant flag (A/B/C bucketing) |
| `/api/reports/monthly` | `ReportsApi` | `[FeatureGate]` attribute on a controller |
| every non-dashboard route | `MaintenanceMode` | `UseMiddlewareForFeature<MaintenanceModeMiddleware>` |
| `POST /api/orders` | `OrdersV2` | Business-logic v1 / v2 split |

Run it:

```bash
dotnet run --project samples/FlagDeck.Sample
```

…then visit `http://localhost:5188/`.

## Production usage

If you really must surface the dashboard in Production (e.g., an internal admin tool behind SSO):

```csharp
builder.Services.AddAuthorization(o => o.AddPolicy("FlagDeck.Admins",
    p => p.RequireRole("platform-admin")));

builder.Services.AddFlagDeck(o =>
{
    o.AllowInProduction = true;
    o.AuthorizationPolicy = "FlagDeck.Admins";
});
```

The endpoint group is decorated with `RequireAuthorization` referencing your policy. FlagDeck logs a warning if you map it in a non-Development environment without one.

## JSON API

For automation, the dashboard also exposes a tiny JSON surface under the same prefix:

```http
GET    /_flagdeck/api/flags          → list flags + configured/override/effective state
POST   /_flagdeck/api/flags/{name}   → { "enabled": true | false | null }
DELETE /_flagdeck/api/flags/{name}   → clear override for one flag
POST   /_flagdeck/clear              → clear every override
```

## Repository layout

```
FlagDeck/
├── FlagDeck.sln
├── global.json                   # pins .NET 10.0.300
├── Directory.Build.props         # shared MSBuild settings
├── Directory.Packages.props      # central package version management
├── src/FlagDeck/                 # the library (this is what ships to NuGet)
├── samples/FlagDeck.Sample/      # runnable reference app, all scenarios wired up
├── tests/FlagDeck.Tests/         # xUnit + WebApplicationFactory integration tests
└── .github/workflows/            # CI, NuGet.org publish, GitHub Packages publish
```


## License

MIT © Suresh Subramanian — see [LICENSE](LICENSE).
