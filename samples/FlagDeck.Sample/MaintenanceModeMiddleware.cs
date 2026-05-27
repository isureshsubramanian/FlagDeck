using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FlagDeck.Sample;

/// <summary>
/// Conditionally inserted into the pipeline by
/// <see cref="Microsoft.FeatureManagement.AspNetCore.FeatureManagementApplicationBuilderExtensions.UseMiddlewareForFeature{T}(IApplicationBuilder, string)"/>.
/// When the <c>MaintenanceMode</c> flag is on, every request to a non-FlagDeck path returns 503.
/// Toggle the flag in the dashboard and watch the response change live.
/// </summary>
public sealed class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceModeMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        // Always let the dashboard through, otherwise developers can't toggle the flag back off.
        if (context.Request.Path.StartsWithSegments("/_flagdeck"))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Service is in maintenance mode (the 'MaintenanceMode' flag is on). "
            + "Open /_flagdeck and flip it off to restore service.");
    }
}
