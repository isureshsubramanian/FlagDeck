using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace FlagDeck.Sample.Controllers;

/// <summary>
/// Demonstrates the <see cref="FeatureGateAttribute"/>. When <c>ReportsApi</c> is off,
/// every action on this controller returns 404 — there is no need to add manual
/// <c>IsEnabledAsync</c> checks. Toggle the flag in FlagDeck to enable the controller.
/// </summary>
[ApiController]
[Route("api/reports")]
[FeatureGate(SampleFlags.ReportsApi)]
public sealed class ReportsController : ControllerBase
{
    [HttpGet("monthly")]
    public IActionResult Monthly() => Ok(new
    {
        report = "monthly",
        rows = new[]
        {
            new { date = "2026-01", revenue = 412_300m },
            new { date = "2026-02", revenue = 487_500m },
            new { date = "2026-03", revenue = 506_900m },
        }
    });

    [HttpGet("quarterly")]
    public IActionResult Quarterly() => Ok(new
    {
        report = "quarterly",
        revenue = 1_406_700m,
        growth = 0.18,
    });
}
