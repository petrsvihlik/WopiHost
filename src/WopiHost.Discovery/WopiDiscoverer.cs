using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Discovery.Enumerations;
using WopiHost.Discovery.Models;

namespace WopiHost.Discovery;

///<inheritdoc cref="IDiscoverer"/>
public partial class WopiDiscoverer : IDiscoverer, IDisposable
{
    private const string ElementNetZone = "net-zone";
    private const string ElementApp = "app";
    private const string ElementAction = "action";
    private const string ElementProofKey = "proof-key";
    private const string AttrNetZoneName = "name";
    private const string AttrActionExtension = "ext";
    private const string AttrActionName = "name";
    private const string AttrActionUrl = "urlsrc";
    private const string AttrActionRequires = "requires";
    private const string AttrAppName = "name";
    private const string AttrAppFavicon = "favIconUrl";
    private const string AttrValCobalt = "cobalt";
    private const string AttrProofKeyValue = "value";
    private const string AttrProofKeyOldValue = "oldvalue";
    private const string AttrProofKeyModulus = "modulus";
    private const string AttrProofKeyExponent = "exponent";
    private const string AttrProofKeyOldModulus = "oldmodulus";
    private const string AttrProofKeyOldExponent = "oldexponent";

    private readonly IOptions<DiscoveryOptions> _discoveryOptions;
    private readonly ILogger<WopiDiscoverer> _logger;

    // Eagerly constructed in the ctor (and stored in readonly fields) so
    // Infer# can statically track the SemaphoreSlim allocation through to
    // Dispose(). The previous lazy `??=` pattern hid ownership from the
    // analyzer and was reported as PULSE_RESOURCE_LEAK.
    private readonly AsyncExpiringLazy<IEnumerable<XElement>> _apps;
    private readonly AsyncExpiringLazy<XElement> _proofKey;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the <see cref="WopiDiscoverer"/>, a class for examining the capabilities of the WOPI client.
    /// </summary>
    /// <param name="discoveryFileProvider">A service that provides the discovery file to examine.</param>
    /// <param name="discoveryOptions">the discovery options</param>
    /// <param name="logger">Optional logger. When omitted, a <see cref="NullLogger{T}"/> is used so the
    /// package stays usable without DI.</param>
    public WopiDiscoverer(
        IDiscoveryFileProvider discoveryFileProvider,
        IOptions<DiscoveryOptions> discoveryOptions,
        ILogger<WopiDiscoverer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(discoveryFileProvider);
        ArgumentNullException.ThrowIfNull(discoveryOptions);

        _discoveryOptions = discoveryOptions;
        _logger = logger ?? NullLogger<WopiDiscoverer>.Instance;

        _apps = new AsyncExpiringLazy<IEnumerable<XElement>>(async _ =>
        {
            var apps = (await discoveryFileProvider.GetDiscoveryXmlAsync())
                .Elements(ElementNetZone)
                .Where(ValidateNetZone)
                .Elements(ElementApp)
                .ToArray();
            LogDiscoveryRefreshed(_logger, apps.Length, _discoveryOptions.Value.NetZone);
            return new TemporaryValue<IEnumerable<XElement>>
            {
                Result = apps,
                ValidUntil = DateTimeOffset.UtcNow.Add(discoveryOptions.Value.RefreshInterval),
            };
        });

        _proofKey = new AsyncExpiringLazy<XElement>(async _ =>
        {
            var proofKey = (await discoveryFileProvider.GetDiscoveryXmlAsync())
                .Elements(ElementProofKey)
                .FirstOrDefault() ?? new XElement(ElementProofKey);
            LogProofKeyRefreshed(_logger);
            return new TemporaryValue<XElement>
            {
                Result = proofKey,
                ValidUntil = DateTimeOffset.UtcNow.Add(discoveryOptions.Value.RefreshInterval),
            };
        });
    }

    internal async Task<IEnumerable<XElement>> GetAppsAsync() => await _apps.Value();

    internal async Task<XElement> GetProofKeyAsync() => await _proofKey.Value();

    private bool ValidateNetZone(XElement e)
    {
        var netZoneString = e.Attribute(AttrNetZoneName)?.Value;
        if (string.IsNullOrEmpty(netZoneString))
        {
            return false;
        }
        netZoneString = netZoneString.Replace("-", "", StringComparison.InvariantCulture);
        var success = Enum.TryParse(netZoneString, true, out NetZoneEnum netZone);
        return success && (netZone == _discoveryOptions.Value.NetZone);
    }

    ///<inheritdoc />
    public async Task<bool> SupportsExtensionAsync(string extension)
    {
        var query = (await GetAppsAsync()).Elements()
            .FirstOrDefault(e => string.Equals(e.Attribute(AttrActionExtension)?.Value, extension, StringComparison.OrdinalIgnoreCase));
        return query is not null;
    }

    ///<inheritdoc />
    public async Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
    {
        var actionString = action.ToString().ToUpperInvariant();

        var query = (await GetAppsAsync()).Elements()
            .Where(e => string.Equals(e.Attribute(AttrActionExtension)?.Value, extension, StringComparison.OrdinalIgnoreCase) && 
                e.Attribute(AttrActionName)?.Value.Equals(actionString, StringComparison.InvariantCultureIgnoreCase) == true);

        return query.Any();
    }

    ///<inheritdoc />
    public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
    {
        var actionString = action.ToString().ToUpperInvariant();

        var query = (await GetAppsAsync()).Elements()
            .Where(e => string.Equals(e.Attribute(AttrActionExtension)?.Value, extension, StringComparison.OrdinalIgnoreCase) && 
                e.Attribute(AttrActionName)?.Value.Equals(actionString, StringComparison.InvariantCultureIgnoreCase) == true)
            .Select(e => e.Attribute(AttrActionRequires)?.Value.Split(','));

        return query?.FirstOrDefault() ?? [];
    }

    ///<inheritdoc />
    public async Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action)
    {
        var requirements = await GetActionRequirementsAsync(extension, action);
        return requirements is not null && requirements.Contains(AttrValCobalt);
    }

    ///<inheritdoc />
    public async Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action)
    {
        var actionString = action.ToString().ToUpperInvariant();
        var query = (await GetAppsAsync()).Elements()
            .Where(e => string.Equals(e.Attribute(AttrActionExtension)?.Value, extension, StringComparison.OrdinalIgnoreCase) && 
                e.Attribute(AttrActionName)?.Value.Equals(actionString, StringComparison.InvariantCultureIgnoreCase) == true)
            .Select(e => e.Attribute(AttrActionUrl)?.Value);
        return query.FirstOrDefault();
    }

    ///<inheritdoc />
    public async Task<string?> GetApplicationNameAsync(string extension)
    {
        var query = (await GetAppsAsync())
            .Where(e => e.Descendants(ElementAction).Any(d => string.Equals(d.Attribute(AttrActionExtension)?.Value, extension, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Attribute(AttrAppName)?.Value);

        return query.FirstOrDefault();
    }

    ///<inheritdoc />
    public async Task<Uri?> GetApplicationFavIconAsync(string extension)
    {
        var query = (await GetAppsAsync())
            .Where(e => e.Descendants(ElementAction).Any(d => string.Equals(d.Attribute(AttrActionExtension)?.Value, extension, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Attribute(AttrAppFavicon)?.Value);
        var result = query.FirstOrDefault();
        return result is not null ? new Uri(result) : null;
    }
    
    ///<inheritdoc />
    public async Task<WopiProofKeys> GetProofKeysAsync()
    {
        var proofKey = await GetProofKeyAsync();

        return new WopiProofKeys
        {
            Value = proofKey.Attribute(AttrProofKeyValue)?.Value,
            OldValue = proofKey.Attribute(AttrProofKeyOldValue)?.Value,
            Modulus = proofKey.Attribute(AttrProofKeyModulus)?.Value,
            Exponent = proofKey.Attribute(AttrProofKeyExponent)?.Value,
            OldModulus = proofKey.Attribute(AttrProofKeyOldModulus)?.Value,
            OldExponent = proofKey.Attribute(AttrProofKeyOldExponent)?.Value
        };
    }

    /// <summary>
    /// Disposes the cached <see cref="AsyncExpiringLazy{T}"/> instances so
    /// their internal semaphores are released. The DI container invokes this
    /// at host shutdown when <see cref="WopiDiscoverer"/> is registered as a
    /// singleton.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources held by this instance.
    /// </summary>
    /// <param name="disposing"><c>true</c> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        if (disposing)
        {
            _apps.Dispose();
            _proofKey.Dispose();
        }
        _disposed = true;
    }
}
