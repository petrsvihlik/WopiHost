using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;

namespace WopiHost.Core.Infrastructure;

/// <summary>
/// Endpoint matcher policy that discriminates endpoints sharing the same route template and HTTP
/// verb based on the <see cref="WopiHeaders.WOPI_OVERRIDE"/> header value. Endpoints opt in by
/// attaching <see cref="WopiOverrideMetadata"/>; only the endpoint whose metadata contains the
/// request header value remains a valid candidate. Endpoints without the metadata are left alone
/// (e.g., <c>GET wopi/files/{id}</c> sharing the route template with the <c>POST</c>-multiplexed
/// override endpoints).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Ordering — load-bearing.</strong> Built-in policies run first: HTTP method matching
/// (Order ≈ -1000) discards verb mismatches; URL template matching populates the candidate set.
/// By the time this policy runs, the surviving candidates share both route template and verb,
/// so header-based discrimination is the last cut and per-endpoint authorization is evaluated
/// against the single selected endpoint.
/// </para>
/// <para>
/// <strong>Selection failure mode.</strong> When the request header is missing or its value is
/// not in any candidate's <see cref="WopiOverrideMetadata.Values"/> set, every override-bearing
/// candidate is invalidated and the router returns <c>404 Not Found</c> — even when another
/// endpoint shares the template under a different verb. The 405 hint belongs to
/// <see cref="HttpMethodMatcherPolicy"/> alone; custom policies that nullify candidates cannot
/// re-emit it.
/// </para>
/// </remarks>
internal sealed class WopiOverrideMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    /// <inheritdoc />
    public override int Order => 1000;

    /// <inheritdoc />
    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        for (var i = 0; i < endpoints.Count; i++)
        {
            if (endpoints[i].Metadata.GetMetadata<WopiOverrideMetadata>() is not null)
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        var header = httpContext.Request.Headers[WopiHeaders.WOPI_OVERRIDE].ToString();

        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            var metadata = candidates[i].Endpoint.Metadata.GetMetadata<WopiOverrideMetadata>();
            if (metadata is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(header) || !metadata.Values.Contains(header))
            {
                candidates.SetValidity(i, false);
            }
        }

        return Task.CompletedTask;
    }
}
