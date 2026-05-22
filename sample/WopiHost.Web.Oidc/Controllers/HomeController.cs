using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Web.Oidc.Infrastructure;
using WopiHost.Web.Oidc.Models;

namespace WopiHost.Web.Oidc.Controllers;

/// <summary>
/// OIDC-authenticated host frontend: the user's identity comes from the OpenID Connect cookie
/// instead of a hardcoded "Anonymous" string, and per-resource permissions baked into the WOPI
/// access token are derived from OIDC role claims via <see cref="OidcRolePermissionMapper"/>.
/// </summary>
[Authorize]
public partial class HomeController(
    IOptions<WopiOptions> wopiOptions,
    IOptions<OidcOptions> oidcOptions,
    IWopiStorageProvider storageProvider,
    IDiscoverer discoverer,
    WopiAccessTokenMinter tokenMinter,
    IWebHostEnvironment hostEnvironment,
    ILogger<HomeController> logger,
    ILogger<WopiUrlBuilder> urlBuilderLogger) : Controller
{
    private readonly WopiUrlBuilder _urlGenerator = new(discoverer, urlBuilderLogger, new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") });

    public async Task<ActionResult> Index()
    {
        ViewData["Title"] = "Welcome to WOPI HOST (OIDC sample)";
        try
        {
            var fileViewModels = new List<FileViewModel>();
            await foreach (var file in storageProvider.GetWopiFiles(storageProvider.RootContainer.Identifier))
            {
                fileViewModels.Add(new FileViewModel
                {
                    FileId = file.Identifier,
                    FileName = file.Name + "." + file.Extension,
                    SupportsEdit = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                    SupportsView = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                    IconUri = (await discoverer.GetApplicationFavIconAsync(file.Extension)) ?? new Uri("file.ico", UriKind.Relative)
                });
            }
            return View(fileViewModels);
        }
        // DiscoveryException wraps the HttpRequestException its underlying provider raised
        // when fetching /hosting/discovery from the WOPI client. Plain HttpRequestException
        // here is the fallback for non-discovery network failures the listing might trigger.
        // Both shapes render the same Error view; logging happens once via the structured
        // logger so the stack trace lands in the server logs even when the UI hides it.
        catch (Exception ex) when (ex is DiscoveryException or HttpRequestException)
        {
            LogIndexException(logger, ex);
            return View("Error", new ErrorViewModel { Exception = ex, ShowExceptionDetails = hostEnvironment.IsDevelopment() });
        }
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error,
        Message = "WopiHost.Web.Oidc Index failed to enumerate / discover files")]
    private static partial void LogIndexException(ILogger logger, Exception exception);

    public async Task<ActionResult> Detail(string id, string wopiAction)
    {
        if (!Enum.TryParse<WopiActionEnum>(wopiAction, ignoreCase: true, out var actionEnum))
        {
            return BadRequest($"Unknown WOPI action '{wopiAction}'.");
        }
        var file = await storageProvider.GetWopiFile(id)
            ?? throw new FileNotFoundException($"File with ID '{id}' not found.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Authenticated principal is missing a subject claim.");
        var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

        var permissions = OidcRolePermissionMapper.Resolve(User, oidcOptions.Value.RoleClaimType);

        // Scope by the requested action: even an editor-role user opening a "view" link gets a
        // read-only token. Necessary because Collabora derives view-vs-edit from CheckFileInfo
        // permission flags (single editor URL); OOS / M365 ship distinct view/edit URLs and so
        // would mask this. Strip write+rename; keep Attend/Present (interaction, not authoring).
        if (actionEnum != WopiActionEnum.Edit)
        {
            permissions &= ~(WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename);
        }

        var (token, expiresAt) = tokenMinter.Mint(new WopiTokenMintRequest
        {
            UserId = userId,
            UserDisplayName = displayName,
            UserEmail = email,
            ResourceId = file.Identifier,
            FilePermissions = permissions,
        });

        ViewData["access_token"] = token;
        ViewData["access_token_ttl"] = expiresAt.ToUnixTimeMilliseconds();
        ViewData["user_display_name"] = displayName ?? userId;

        var extension = file.Extension.TrimStart('.');
        // business_user=1 marks the user as a business (org) user per the M365 business flow.
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/business
        var wopiSrc = new Uri(wopiOptions.Value.HostUrl, $"/wopi/files/{id}");
        var url = await _urlGenerator.GetFileUrlAsync(extension, wopiSrc, actionEnum);
        ViewData["urlsrc"] = AppendBusinessFlag(url);
        ViewData["favicon"] = await discoverer.GetApplicationFavIconAsync(extension);

        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Expires = "-1";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "*";
        return View();
    }

    [Route("Error")]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        Exception = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error,
        // Stack traces leak file paths, configuration values, and source-line numbers — gate
        // on Development so a stray production deploy of this sample doesn't echo internals
        // back to anonymous callers.
        ShowExceptionDetails = hostEnvironment.IsDevelopment(),
    });

    /// <summary>
    /// Appends the M365 business-flow marker (<c>business_user=1</c>) as a query parameter.
    /// Replaces any existing <c>business_user</c> value and preserves the URL's fragment —
    /// naive string concatenation broke on URLs that already carried a fragment (the
    /// <c>&amp;business_user=1</c> would land after the <c>#</c>) and on URLs that already
    /// contained <c>business_user</c> (producing a duplicate query key with undefined semantics).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Spec reference:
    /// <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/scenarios/business"/>.
    /// </para>
    /// <para>
    /// <c>HttpUtility.ParseQueryString</c> returns a <see cref="System.Collections.Specialized.NameValueCollection"/>
    /// whose <c>ToString()</c> emits URL-encoded <c>key=value&amp;…</c> shape, and whose
    /// indexer-set replaces any prior value for the key in place — exactly the idempotent-set
    /// semantic this method needs. <see cref="UriBuilder"/> owns fragment / port / scheme parts
    /// in their own properties so we don't have to slice them out by hand.
    /// </para>
    /// </remarks>
    internal static string? AppendBusinessFlag(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        var builder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
        query["business_user"] = "1";
        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}
