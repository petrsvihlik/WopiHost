using System.Text;
using Microsoft.AspNetCore.Http;

namespace WopiHost.Core.Security.Authentication;

/// <summary>
/// Builds the <c>WWW-Authenticate</c> header value the WOPI bootstrap spec requires on a
/// <c>401 Unauthorized</c> response from <c>/wopibootstrapper</c>.
/// </summary>
/// <remarks>
/// <para>
/// The bootstrapper authenticates using OAuth2 Bearer tokens from the host's identity
/// provider. When the token is missing or invalid, the spec mandates a specific
/// <c>WWW-Authenticate</c> challenge that points the WOPI client at the host's IdP.
/// Hosts wire this into their authentication scheme — typically as a <c>JwtBearerEvents.OnChallenge</c>
/// handler:
/// </para>
/// <code>
/// services.AddAuthentication()
///     .AddJwtBearer(WopiAuthenticationSchemes.Bootstrap, options =&gt;
///     {
///         options.Authority = "https://idp.contoso.com";
///         options.Events = new JwtBearerEvents
///         {
///             OnChallenge = context =&gt;
///             {
///                 var header = WopiBootstrapChallenge.Build(
///                     authorizationUri: new Uri("https://idp.contoso.com/oauth2/authorize"),
///                     tokenIssuanceUri: new Uri("https://idp.contoso.com/oauth2/token"),
///                     providerId: "tp_contoso");
///                 context.Response.Headers.Append("WWW-Authenticate", header);
///                 context.HandleResponse();
///                 context.Response.StatusCode = StatusCodes.Status401Unauthorized;
///                 return Task.CompletedTask;
///             }
///         };
///     });
/// </code>
/// <para>
/// Spec: <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/bootstrapper/bootstrap#www-authenticate-response-header-format"/>.
/// </para>
/// </remarks>
public static class WopiBootstrapChallenge
{
    /// <summary>
    /// Builds the spec-compliant <c>WWW-Authenticate</c> header value.
    /// </summary>
    /// <param name="authorizationUri">The OAuth2 authorization endpoint URL.</param>
    /// <param name="tokenIssuanceUri">The OAuth2 token endpoint URL.</param>
    /// <param name="providerId">
    /// Optional well-known identifier (registered with Microsoft 365) for the host.
    /// Per spec, allowed characters are <c>[a-zA-Z0-9]</c>.
    /// </param>
    /// <param name="urlSchemes">
    /// Optional URL-encoded JSON document mapping platforms to their URL schemes, e.g.
    /// <c>{"iOS":["contoso"], "Android":["contoso"]}</c>. The string passed in must already
    /// be URL-encoded as the spec requires; this method does not encode it.
    /// </param>
    public static string Build(
        Uri authorizationUri,
        Uri tokenIssuanceUri,
        string? providerId = null,
        string? urlSchemes = null)
    {
        ArgumentNullException.ThrowIfNull(authorizationUri);
        ArgumentNullException.ThrowIfNull(tokenIssuanceUri);

        if (!string.IsNullOrEmpty(providerId) && !IsValidProviderId(providerId))
        {
            throw new ArgumentException(
                "providerId must contain only ASCII letters and digits per the WOPI spec.",
                nameof(providerId));
        }

        var sb = new StringBuilder("Bearer ");
        AppendParameter(sb, "authorization_uri", authorizationUri.AbsoluteUri, first: true);
        AppendParameter(sb, "tokenIssuance_uri", tokenIssuanceUri.AbsoluteUri, first: false);
        if (!string.IsNullOrEmpty(providerId))
        {
            AppendParameter(sb, "providerId", providerId, first: false);
        }
        if (!string.IsNullOrEmpty(urlSchemes))
        {
            AppendParameter(sb, "UrlSchemes", urlSchemes, first: false);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Convenience: build the header value and append it to <paramref name="response"/>.
    /// Sets the status code to <c>401</c>; callers can override afterwards if needed.
    /// </summary>
    public static void Apply(
        HttpResponse response,
        Uri authorizationUri,
        Uri tokenIssuanceUri,
        string? providerId = null,
        string? urlSchemes = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers.Append(
            "WWW-Authenticate",
            Build(authorizationUri, tokenIssuanceUri, providerId, urlSchemes));
    }

    private static void AppendParameter(StringBuilder sb, string name, string value, bool first)
    {
        if (!first)
        {
            sb.Append(", ");
        }
        sb.Append(name).Append("=\"").Append(value).Append('"');
    }

    private static bool IsValidProviderId(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsAsciiLetterOrDigit(ch))
            {
                return false;
            }
        }
        return true;
    }
}
