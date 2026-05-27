using System.Collections.Generic;
using System.Threading.Tasks;
using FlagDeck.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FlagDeck.Internal;

/// <summary>
/// Renders the dashboard's Blazor components to an HTML string using
/// <see cref="HtmlRenderer"/>. This keeps the UI in real .razor files while letting us
/// ship as a plain class library (no SignalR, no client-side bundle).
/// </summary>
internal sealed class DashboardRenderer
{
    private readonly HtmlRenderer _htmlRenderer;

    public DashboardRenderer(HtmlRenderer htmlRenderer)
    {
        _htmlRenderer = htmlRenderer;
    }

    public Task<string> RenderDashboardAsync(DashboardModel model)
        => _htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DashboardPage.Model)] = model,
            });
            var output = await _htmlRenderer.RenderComponentAsync<DashboardPage>(parameters);
            return output.ToHtmlString();
        });
}
