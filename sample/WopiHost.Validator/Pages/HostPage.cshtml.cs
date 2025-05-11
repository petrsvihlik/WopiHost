using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Validator.Models;

namespace WopiHost.Validator.Pages;

public class HostPageModel(
    IOptions<WopiOptions> wopiOptions,
    IWopiStorageProvider storageProvider,
    IWopiSecurityHandler securityHandler,
    IDiscoverer discoverer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public required string FileId { get; set; }
    [BindProperty(SupportsGet = true)]
    public required WopiActionEnum WopiAction { get; set; }

    public string AccessToken { get; set; } = string.Empty;
    public string AccessTokenTtl { get; set; } = string.Empty;
    public string UrlSrc { get; set; } = string.Empty;

    private readonly WopiUrlBuilder urlGenerator = new(discoverer, new WopiUrlSettings { UiLlcc = CultureInfo.CurrentUICulture });

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var file = await storageProvider.GetWopiResource<IWopiFile>(FileId, cancellationToken)
            ?? throw new FileNotFoundException($"File with ID '{FileId}' not found.");
        var token = await securityHandler.GenerateAccessToken(wopiOptions.Value.UserId, file.Identifier, cancellationToken);


        AccessToken = securityHandler.WriteToken(token);
        var tokenDateOffset = new DateTimeOffset(token.ValidTo);
        AccessTokenTtl = (tokenDateOffset - DateTimeOffset.UnixEpoch).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        //TODO: fix
        //ViewData["access_token_ttl"] = //token.ValidTo

        //http://dotnet-stuff.com/tutorials/aspnet-mvc/how-to-render-different-layout-in-asp-net-mvc


        var extension = file.Extension.TrimStart('.');
        // Url.ValidatorTestCategory
        //TODO: add a test for the URL not to contain double slashes between host and path
        UrlSrc = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiOptions.Value.HostUrl, $"/wopi/files/{FileId}"), WopiAction)
            ?? throw new InvalidOperationException($"Could not retrieve WopiUrl for extension '{extension}'");
        ViewData["favicon"] = await discoverer.GetApplicationFavIconAsync(extension);
        return Page();
    }
}
