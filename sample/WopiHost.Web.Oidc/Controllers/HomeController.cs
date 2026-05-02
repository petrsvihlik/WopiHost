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
public class HomeController(
    IOptions<WopiOptions> wopiOptions,
    IOptions<OidcOptions> oidcOptions,
    IWopiStorageProvider storageProvider,
    IDiscoverer discoverer,
    WopiAccessTokenMinter tokenMinter) : Controller
{
    private readonly WopiUrlBuilder _urlGenerator = new(discoverer, new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") });

    public async Task<ActionResult> Index()
    {
        ViewData["Title"] = "Welcome to WOPI HOST (OIDC sample)";
        try
        {
            var fileViewModels = new List<FileViewModel>();
            await foreach (var file in storageProvider.GetWopiFiles(storageProvider.RootContainerPointer.Identifier))
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
        catch (DiscoveryException ex)
        {
            return View("Error", new ErrorViewModel { Exception = ex, ShowExceptionDetails = true });
        }
        catch (HttpRequestException ex)
        {
            return View("Error", new ErrorViewModel { Exception = ex, ShowExceptionDetails = true });
        }
    }

    public async Task<ActionResult> Detail(string id, string wopiAction)
    {
        var actionEnum = Enum.Parse<WopiActionEnum>(wopiAction);
        var file = await storageProvider.GetWopiResource<IWopiFile>(id)
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
        ShowExceptionDetails = true,
    });

    private static string? AppendBusinessFlag(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }
        var separator = url.Contains('?') ? "&" : "?";
        return url + separator + "business_user=1";
    }
}
