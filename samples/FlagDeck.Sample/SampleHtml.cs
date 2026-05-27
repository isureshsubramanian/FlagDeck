namespace FlagDeck.Sample;

internal static class SampleHtml
{
    public static string Index(string environmentName) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>FlagDeck Sample</title>
          <style>
            body { font: 15px/1.6 -apple-system, "Segoe UI", system-ui, sans-serif; background: #f6f5f1; color: #1a1d21; margin: 0; padding: 40px 24px; }
            main { max-width: 760px; margin: 0 auto; }
            h1 { font-family: ui-serif, Georgia, serif; font-weight: 600; margin: 0 0 4px; }
            .env { color: #5a6068; text-transform: uppercase; font-size: 11px; letter-spacing: 0.08em; }
            .cta { display: inline-block; margin: 18px 0 28px; padding: 8px 16px; background: #1a1d21; color: #fff; border-radius: 6px; text-decoration: none; font-weight: 600; }
            ul { padding: 0; list-style: none; }
            li { background: #fff; border: 1px solid #e2ddd2; border-radius: 8px; padding: 14px 18px; margin: 10px 0; }
            code { background: #efece5; padding: 1px 6px; border-radius: 4px; font-size: 0.9em; }
            a { color: #8a6d3b; }
          </style>
        </head>
        <body>
        <main>
          <h1>FlagDeck Sample</h1>
          <p class="env">Environment: {{environmentName}}</p>
          <a class="cta" href="/_flagdeck">Open the FlagDeck dashboard →</a>
          <p>This app exercises every common Microsoft.FeatureManagement scenario so you can flip flags in the dashboard and watch behavior change in real time.</p>
          <ul>
            <li><strong>Simple boolean</strong> — <a href="/api/welcome">/api/welcome</a> swaps between two implementations of <code>IGreetingService</code> via the <code>NewGreeting</code> flag.</li>
            <li><strong>Percentage rollout</strong> — <a href="/api/beta">/api/beta</a> reads the <code>BetaUI</code> flag, configured at 25%. Force it on/off via FlagDeck.</li>
            <li><strong>Time window</strong> — <a href="/api/holiday-banner">/api/holiday-banner</a> checks <code>HolidayBanner</code>, gated by a TimeWindowFilter.</li>
            <li><strong>Targeting</strong> — <a href="/api/internal-tools?user=alice">/api/internal-tools?user=alice</a> uses the TargetingFilter against a <code>SampleTargetingContextAccessor</code>.</li>
            <li><strong>Variant flag</strong> — <a href="/api/checkout-experience">/api/checkout-experience</a> returns the variant assigned by <code>CheckoutExperience</code>.</li>
            <li><strong>[FeatureGate] attribute</strong> — <a href="/api/reports/monthly">/api/reports/monthly</a> returns 404 unless <code>ReportsApi</code> is on.</li>
            <li><strong>Conditional middleware</strong> — flipping <code>MaintenanceMode</code> on makes every non-dashboard route return 503.</li>
            <li><strong>Business-logic flag</strong> — <code>POST</code> <a href="/api/orders">/api/orders</a> picks between OrdersV1 and OrdersV2 based on <code>OrdersV2</code>.</li>
          </ul>
        </main>
        </body>
        </html>
        """;
}
