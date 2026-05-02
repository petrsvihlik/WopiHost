using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Web.Models;

namespace WopiHost.Web.Controllers;

/// <summary>
/// Demonstrates the host-frontend side of WOPI: list files, generate the WOPI URL for the
/// chosen action, and mint an <c>access_token</c> the WOPI client will replay back to the
/// WopiHost server.
/// </summary>
/// <remarks>
/// The token format is a contract with the WopiHost server's
/// <c>JwtAccessTokenService</c>: same HMAC key, same claim layout. We sign the JWT inline
/// here rather than depending on the server's Core library so this sample stays a thin
/// frontend (no controllers/auth pipeline).
/// </remarks>
public class HomeController(
    IOptions<WopiOptions> wopiOptions,
    IWopiStorageProvider storageProvider,
    IDiscoverer discoverer) : Controller
{
    private readonly WopiUrlBuilder urlGenerator = new(
        discoverer,
        new WopiUrlSettings { UiLlcc = ResolveUiCulture(wopiOptions.Value.UiCulture) });

    private static CultureInfo ResolveUiCulture(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? CultureInfo.CurrentUICulture : new CultureInfo(configured);

    // Demo-only shared key — must match the WopiHost server's Wopi:Security:SigningKey.
    // In a real frontend, load this from the same managed secret store the server uses.
    private static readonly byte[] SharedSigningKey = DerivePaddedKey("wopi-sample-shared-dev-key");

    public async Task<ActionResult> Index()
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
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

        var (token, expiresAt) = MintAccessToken("Anonymous", file.Identifier);
        ViewData["access_token"] = token;
        ViewData["access_token_ttl"] = expiresAt.ToUnixTimeMilliseconds();

        var extension = file.Extension.TrimStart('.');
        ViewData["urlsrc"] = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiOptions.Value.HostUrl, $"/wopi/files/{id}"), actionEnum);
        ViewData["favicon"] = await discoverer.GetApplicationFavIconAsync(extension);

        // Host page headers per WOPI spec — prevent browser caching.
        // https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/online/hostpage#host-page-headers
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Expires = "-1";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Vary = "*";
        return View();
    }

    [Route("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        Exception = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error,
        ShowExceptionDetails = true,
    });

    private static (string Token, DateTimeOffset ExpiresAt) MintAccessToken(string userId, string resourceId)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var descriptor = new SecurityTokenDescriptor
        {
            // Claims must match the layout the WopiHost server's JwtAccessTokenService writes.
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId),
                new Claim("wopi:rid", resourceId),
                new Claim("wopi:rtype", "File"),
                new Claim("wopi:fperms",
                    (WopiFilePermissions.UserCanWrite |
                     WopiFilePermissions.UserCanRename |
                     WopiFilePermissions.UserCanAttend |
                     WopiFilePermissions.UserCanPresent).ToString()),
            ]),
            NotBefore = DateTime.UtcNow,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SharedSigningKey), SecurityAlgorithms.HmacSha256),
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return (handler.WriteToken(handler.CreateToken(descriptor)), expires);
    }

    private static byte[] DerivePaddedKey(string secret)
    {
        var raw = Encoding.UTF8.GetBytes(secret);
        if (raw.Length >= 32) return raw;
        var padded = new byte[32];
        Array.Copy(raw, padded, raw.Length);
        return padded;
    }
}
