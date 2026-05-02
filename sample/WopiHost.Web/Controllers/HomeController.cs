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

    public async Task<ActionResult> Index(string? containerId = null, string? parentContainerId = null, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
        try
        {
            // Default to the storage provider's root when no container is specified.
            containerId ??= storageProvider.RootContainerPointer.Identifier;
            var current = await storageProvider.GetWopiResource<IWopiFolder>(containerId, cancellationToken)
                ?? throw new DirectoryNotFoundException($"Container '{containerId}' not found.");

            var model = new BrowseViewModel
            {
                ContainerId = containerId,
                ContainerName = current.Name,
            };

            // Breadcrumb: if we're below the root, walk ancestors to build clickable trail
            // and recover the parent container id when the URL didn't supply one.
            if (containerId != storageProvider.RootContainerPointer.Identifier)
            {
                var ancestors = await storageProvider.GetAncestors<IWopiFolder>(containerId, cancellationToken);
                for (var i = 0; i < ancestors.Count; i++)
                {
                    var ancestor = ancestors[i];
                    var parentId = i > 0 ? ancestors[i - 1].Identifier : null;
                    model.BreadcrumbParts.Add(new BreadcrumbPart(
                        ancestor.Name,
                        Url.Action("Index", "Home", new { containerId = ancestor.Identifier, parentContainerId = parentId })!));
                }
                if (string.IsNullOrWhiteSpace(parentContainerId) && ancestors.Count > 0)
                {
                    parentContainerId = ancestors[^1].Identifier;
                }
            }

            // Show ".." entry when there's somewhere to go up to.
            if (!string.IsNullOrWhiteSpace(parentContainerId))
            {
                var parent = await storageProvider.GetWopiResource<IWopiFolder>(parentContainerId, cancellationToken)
                    ?? throw new DirectoryNotFoundException($"Parent container '{parentContainerId}' not found.");
                model.Containers.Add(new ContainerViewModel { ContainerId = parent.Identifier, Name = ".." });
            }

            await foreach (var folder in storageProvider.GetWopiContainers(containerId, cancellationToken))
            {
                model.Containers.Add(new ContainerViewModel { ContainerId = folder.Identifier, Name = folder.Name });
            }

            await foreach (var file in storageProvider.GetWopiFiles(containerId, cancellationToken: cancellationToken))
            {
                model.Files.Add(new FileViewModel
                {
                    FileId = file.Identifier,
                    FileName = file.Name + "." + file.Extension,
                    LastModified = file.LastWriteTimeUtc.ToLocalTime(),
                    Size = file.Size,
                    FormattedSize = FormatFileSize(file.Size),
                    SupportsEdit = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                    SupportsView = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                    // Leading slash so the fallback resolves to /file.ico regardless of the
                    // current URL — without it the browser resolves "file.ico" relative to
                    // the current path (e.g. /Home/Index → /Home/file.ico → 404).
                    IconUri = (await discoverer.GetApplicationFavIconAsync(file.Extension)) ?? new Uri("/file.ico", UriKind.Relative)
                });
            }

            return View(model);
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

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", size, units[unit]);
    }

    public async Task<ActionResult> Detail(string id, string wopiAction)
    {
        var actionEnum = Enum.Parse<WopiActionEnum>(wopiAction);
        var file = await storageProvider.GetWopiResource<IWopiFile>(id)
            ?? throw new FileNotFoundException($"File with ID '{id}' not found.");

        var (token, expiresAt) = MintAccessToken("Anonymous", file.Identifier, actionEnum);
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

    private static (string Token, DateTimeOffset ExpiresAt) MintAccessToken(string userId, string resourceId, WopiActionEnum action)
    {
        // Scope permissions by action. OOS / M365 ship distinct view vs edit URLs (the URL alone
        // enforces the mode), but Collabora Online uses a single editor URL and derives the mode
        // from CheckFileInfo permission flags — so a token granting UserCanWrite always opens
        // in edit, regardless of which discovery action was selected. View-only must omit it.
        var perms = action == WopiActionEnum.Edit
            ? (WopiFilePermissions.UserCanWrite
               | WopiFilePermissions.UserCanRename
               | WopiFilePermissions.UserCanAttend
               | WopiFilePermissions.UserCanPresent)
            : WopiFilePermissions.UserCanAttend;

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
                new Claim("wopi:fperms", perms.ToString()),
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
