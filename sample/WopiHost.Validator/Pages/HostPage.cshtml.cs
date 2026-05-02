using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Infrastructure;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Validator.Models;

namespace WopiHost.Validator.Pages;

public class HostPageModel(
    IOptions<WopiOptions> wopiOptions,
    IWopiStorageProvider storageProvider,
    IWopiAccessTokenService accessTokenService,
    IWopiPermissionProvider permissionProvider,
    IDiscoverer discoverer,
    LinkGenerator linkGenerator) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public required string FileId { get; set; }
    [BindProperty(SupportsGet = true)]
    public required WopiActionEnum WopiAction { get; set; }

    public string AccessToken { get; set; } = string.Empty;
    public string AccessTokenTtl { get; set; } = string.Empty;
    public Uri? UrlSrc { get; set; }

    private readonly WopiUrlBuilder urlGenerator = new(discoverer, new WopiUrlSettings { UiLlcc = CultureInfo.CurrentUICulture });

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var file = await storageProvider.GetWopiResource<IWopiFile>(FileId, cancellationToken)
            ?? throw new FileNotFoundException($"File with ID '{FileId}' not found.");

        var hostUser = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, wopiOptions.Value.UserId),
            new Claim(ClaimTypes.Name, wopiOptions.Value.UserId),
        ], "validator"));

        var permissions = await permissionProvider.GetFilePermissionsAsync(hostUser, file, cancellationToken);
        var token = await accessTokenService.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = wopiOptions.Value.UserId,
            UserDisplayName = wopiOptions.Value.UserId,
            UserEmail = wopiOptions.Value.UserId + "@domain.tld",
            ResourceId = file.Identifier,
            ResourceType = WopiResourceType.File,
            FilePermissions = permissions,
        }, cancellationToken);

        AccessToken = token.Token;
        AccessTokenTtl = token.ExpiresAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        var extension = file.Extension.TrimStart('.');
        var wopiFileUrl = new Uri(
            wopiOptions.Value.HostUrl,
            linkGenerator.GetPathByRouteValues(WopiRouteNames.CheckFileInfo, new { id = FileId })
            ?? throw new InvalidOperationException($"Could not generate route for '{WopiRouteNames.CheckFileInfo}'"));
        var urlSrcString = await urlGenerator.GetFileUrlAsync(extension, wopiFileUrl, WopiAction)
            ?? throw new InvalidOperationException($"Could not retrieve WopiUrl for extension '{extension}'");
        UrlSrc = new Uri(urlSrcString, UriKind.Absolute);
        ViewData["favicon"] = await discoverer.GetApplicationFavIconAsync(extension);
        return Page();
    }
}
