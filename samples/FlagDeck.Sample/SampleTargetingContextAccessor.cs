using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.FeatureManagement.FeatureFilters;

namespace FlagDeck.Sample;

/// <summary>
/// Demonstrates Microsoft.FeatureManagement's TargetingFilter. A real app would read the
/// authenticated user from <c>HttpContext.User</c> and project groups/claims into the context.
/// For sample purposes we accept a <c>?user=alice</c> query parameter and bucket built-ins
/// into the <c>internal</c> group.
/// </summary>
public sealed class SampleTargetingContextAccessor : ITargetingContextAccessor
{
    private static readonly string[] InternalUsers = ["suresh", "alice", "bob"];

    private readonly IHttpContextAccessor _http;

    public SampleTargetingContextAccessor(IHttpContextAccessor http) => _http = http;

    public ValueTask<TargetingContext> GetContextAsync()
    {
        var user = _http.HttpContext?.Request.Query["user"].ToString();
        if (string.IsNullOrWhiteSpace(user)) user = "guest";

        var groups = InternalUsers.Contains(user, StringComparer.OrdinalIgnoreCase)
            ? SampleTargetingGroups.Internal
            : SampleTargetingGroups.Users;

        return ValueTask.FromResult(new TargetingContext { UserId = user, Groups = groups });
    }
}
