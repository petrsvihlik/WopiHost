using System.Xml.Linq;
using Microsoft.Extensions.Options;
using WopiHost.Discovery.Enumerations;
using WopiHost.Discovery.Models;

namespace WopiHost.Discovery;

///<inheritdoc cref="IDiscoverer"/>
/// <summary>
/// Creates a new instance of the <see cref="WopiDiscoverer"/>, a class for examining the capabilities of the WOPI client.
/// </summary>
/// <param name="discoveryFileProvider">A service that provides the discovery file to examine.</param>
/// <param name="discoveryOptions">the discovery options</param>
public class WopiDiscoverer(
    IDiscoveryFileProvider discoveryFileProvider, 
    IOptions<DiscoveryOptions> discoveryOptions) : IDiscoverer
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

    private AsyncExpiringLazy<IEnumerable<XElement>>? _apps;
    private AsyncExpiringLazy<XElement>? _proofKey;

    private AsyncExpiringLazy<IEnumerable<XElement>> Apps
    {
        get
        {
            return _apps ??= new AsyncExpiringLazy<IEnumerable<XElement>>(async metadata =>
            {
                return new TemporaryValue<IEnumerable<XElement>>
                {
                    Result = (await discoveryFileProvider.GetDiscoveryXmlAsync())
                    .Elements(ElementNetZone)
                    .Where(ValidateNetZone)
                    .Elements(ElementApp),

                    ValidUntil = DateTimeOffset.UtcNow.Add(discoveryOptions.Value.RefreshInterval)
                };
            });
        }
    }
    
    private AsyncExpiringLazy<XElement> ProofKey
    {
        get
        {
            return _proofKey ??= new AsyncExpiringLazy<XElement>(async metadata =>
            {
                return new TemporaryValue<XElement>
                {
                    Result = (await discoveryFileProvider.GetDiscoveryXmlAsync())
                        .Elements(ElementProofKey)
                        .FirstOrDefault() ?? new XElement(ElementProofKey),

                    ValidUntil = DateTimeOffset.UtcNow.Add(discoveryOptions.Value.RefreshInterval)
                };
            });
        }
    }

    internal async Task<IEnumerable<XElement>> GetAppsAsync() => await Apps.Value();
    
    internal async Task<XElement> GetProofKeyAsync() => await ProofKey.Value();

    private bool ValidateNetZone(XElement e)
    {
        var netZoneString = e.Attribute(AttrNetZoneName)?.Value;
        if (string.IsNullOrEmpty(netZoneString))
        {
            return false;
        }
        netZoneString = netZoneString.Replace("-", "", StringComparison.InvariantCulture);
        var success = Enum.TryParse(netZoneString, true, out NetZoneEnum netZone);
        return success && (netZone == discoveryOptions.Value.NetZone);
    }

    ///<inheritdoc />
    public async Task<bool> SupportsExtensionAsync(string extension)
    {
        var query = (await GetAppsAsync()).Elements()
            .FirstOrDefault(e => e.Attribute(AttrActionExtension)?.Value == extension);
        return query is not null;
    }

    ///<inheritdoc />
    public async Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
    {
        var actionString = action.ToString().ToUpperInvariant();

        var query = (await GetAppsAsync()).Elements()
            .Where(e => e.Attribute(AttrActionExtension)?.Value == extension && 
                e.Attribute(AttrActionName)?.Value.Equals(actionString, StringComparison.InvariantCultureIgnoreCase) == true);

        return query.Any();
    }

    ///<inheritdoc />
    public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
    {
        var actionString = action.ToString().ToUpperInvariant();

        var query = (await GetAppsAsync()).Elements()
            .Where(e => e.Attribute(AttrActionExtension)?.Value == extension && 
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
            .Where(e => e.Attribute(AttrActionExtension)?.Value == extension && 
                e.Attribute(AttrActionName)?.Value.Equals(actionString, StringComparison.InvariantCultureIgnoreCase) == true)
            .Select(e => e.Attribute(AttrActionUrl)?.Value);
        return query.FirstOrDefault();
    }

    ///<inheritdoc />
    public async Task<string?> GetApplicationNameAsync(string extension)
    {
        var query = (await GetAppsAsync())
            .Where(e => e.Descendants(ElementAction).Any(d => d.Attribute(AttrActionExtension)?.Value == extension))
            .Select(e => e.Attribute(AttrAppName)?.Value);

        return query.FirstOrDefault();
    }

    ///<inheritdoc />
    public async Task<Uri?> GetApplicationFavIconAsync(string extension)
    {
        var query = (await GetAppsAsync())
            .Where(e => e.Descendants(ElementAction).Any(d => d.Attribute(AttrActionExtension)?.Value == extension))
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
}
