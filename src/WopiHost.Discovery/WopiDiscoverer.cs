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
    private const string AttrNetZoneName = "name";
    private const string AttrActionExtension = "ext";
    private const string AttrActionName = "name";
    private const string AttrActionUrl = "urlsrc";
    private const string AttrActionRequires = "requires";
    private const string AttrAppName = "name";
    private const string AttrAppFavicon = "favIconUrl";
    private const string AttrValCobalt = "cobalt";

    private AsyncExpiringLazy<WopiDiscoveryData>? _discoveryData;

    private AsyncExpiringLazy<WopiDiscoveryData> DiscoveryData
    {
        get
        {
            return _discoveryData ??= new AsyncExpiringLazy<WopiDiscoveryData>(async metadata =>
            {
                return new TemporaryValue<WopiDiscoveryData>
                {
                    Result = await ProcessDiscoveryXmlAsync(),
                    ValidUntil = DateTimeOffset.UtcNow.Add(discoveryOptions.Value.RefreshInterval)
                };
            });
        }
    }

    private async Task<WopiDiscoveryData> ProcessDiscoveryXmlAsync()
    {
        var discoveryData = new WopiDiscoveryData();
        var xDoc = await discoveryFileProvider.GetDiscoveryXmlAsync();

        // Filter net zones based on options
        var netZones = xDoc.Elements(ElementNetZone).Where(ValidateNetZone);

        foreach (var netZone in netZones)
        {
            foreach (var appElement in netZone.Elements(ElementApp))
            {
                var appInfo = new AppInfo
                {
                    Name = appElement.Attribute(AttrAppName)?.Value,
                    FavIconUrl = appElement.Attribute(AttrAppFavicon)?.Value
                };

                foreach (var actionElement in appElement.Elements(ElementAction))
                {
                    var extension = actionElement.Attribute(AttrActionExtension)?.Value;
                    if (string.IsNullOrEmpty(extension))
                        continue;

                    var actionName = actionElement.Attribute(AttrActionName)?.Value;
                    if (string.IsNullOrEmpty(actionName) || 
                        !Enum.TryParse(actionName, true, out WopiActionEnum action))
                        continue;

                    var actionInfo = new ActionInfo
                    {
                        Action = action,
                        UrlTemplate = actionElement.Attribute(AttrActionUrl)?.Value,
                        Requirements = actionElement.Attribute(AttrActionRequires)?.Value?
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []
                    };

                    // Add to app extensions
                    if (!appInfo.Extensions.TryGetValue(extension, out var actions))
                    {
                        actions = new List<ActionInfo>();
                        appInfo.Extensions[extension] = actions;
                    }
                    actions.Add(actionInfo);

                    // Update lookup tables
                    discoveryData.ExtensionLookup[extension] = true;
                    
                    if (!discoveryData.ActionLookup.TryGetValue(extension, out var actionDict))
                    {
                        actionDict = new Dictionary<WopiActionEnum, ActionInfo>();
                        discoveryData.ActionLookup[extension] = actionDict;
                    }
                    actionDict[action] = actionInfo;

                    // Map extension to app
                    discoveryData.ExtensionToAppLookup[extension] = appInfo;
                }

                // Only add apps that have at least one extension/action
                if (appInfo.Extensions.Count > 0)
                {
                    discoveryData.Apps.Add(appInfo);
                }
            }
        }

        return discoveryData;
    }

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
        var data = await DiscoveryData.Value();
        return data.ExtensionLookup.TryGetValue(extension, out var supported) && supported;
    }

    ///<inheritdoc />
    public async Task<bool> SupportsActionAsync(string extension, WopiActionEnum action)
    {
        var data = await DiscoveryData.Value();
        return data.ActionLookup.TryGetValue(extension, out var actions) && 
               actions.ContainsKey(action);
    }

    ///<inheritdoc />
    public async Task<IEnumerable<string>> GetActionRequirementsAsync(string extension, WopiActionEnum action)
    {
        var data = await DiscoveryData.Value();
        if (data.ActionLookup.TryGetValue(extension, out var actions) && 
            actions.TryGetValue(action, out var actionInfo))
        {
            return actionInfo.Requirements;
        }
        
        return [];
    }

    ///<inheritdoc />
    public async Task<bool> RequiresCobaltAsync(string extension, WopiActionEnum action)
    {
        var data = await DiscoveryData.Value();
        if (data.ActionLookup.TryGetValue(extension, out var actions) && 
            actions.TryGetValue(action, out var actionInfo))
        {
            return actionInfo.RequiresCobalt;
        }
        
        return false;
    }

    ///<inheritdoc />
    public async Task<string?> GetUrlTemplateAsync(string extension, WopiActionEnum action)
    {
        var data = await DiscoveryData.Value();
        if (data.ActionLookup.TryGetValue(extension, out var actions) && 
            actions.TryGetValue(action, out var actionInfo))
        {
            return actionInfo.UrlTemplate;
        }
        
        return null;
    }

    ///<inheritdoc />
    public async Task<string?> GetApplicationNameAsync(string extension)
    {
        var data = await DiscoveryData.Value();
        if (data.ExtensionToAppLookup.TryGetValue(extension, out var appInfo))
        {
            return appInfo.Name;
        }
        
        return null;
    }

    ///<inheritdoc />
    public async Task<Uri?> GetApplicationFavIconAsync(string extension)
    {
        var data = await DiscoveryData.Value();
        if (data.ExtensionToAppLookup.TryGetValue(extension, out var appInfo) && 
            !string.IsNullOrEmpty(appInfo.FavIconUrl))
        {
            return new Uri(appInfo.FavIconUrl);
        }
        
        return null;
    }

    /// <summary>
    /// Gets all apps from the discovery XML.
    /// </summary>
    /// <returns>The collection of WOPI applications.</returns>
    public async Task<WopiApps> GetAppsAsync()
    {
        var xDoc = await discoveryFileProvider.GetDiscoveryXmlAsync();

        // Filter net zones based on options
        var netZones = xDoc.Elements(ElementNetZone).Where(ValidateNetZone);
        if (!netZones.Any())
        {
            return new WopiApps();
        }

        // Extract apps from the first valid net zone
        var appsElement = netZones.First();
        var result = new WopiApps
        {
            Apps = appsElement.Elements(ElementApp)
                .Select(appElement => new AppInfo
                {
                    Name = appElement.Attribute(AttrAppName)?.Value,
                    FavIconUrl = appElement.Attribute(AttrAppFavicon)?.Value,
                    // You could also populate the Extensions dictionary here if needed
                })
                .ToList()
        };

        return result;
    }
}
