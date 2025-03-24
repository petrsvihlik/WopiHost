using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Options;
using System.Text;
using System.Xml.Linq;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Benchmarks;

[MemoryDiagnoser]
public class ComparisonBenchmarks
{
    // Path to test discovery XML
    private string _xmlPath = "";
    
    // The current optimized implementation
    private IDiscoverer _optimizedDiscoverer;

    // The original implementation for comparison
    private IDiscoverer _originalDiscoverer;

    // Extensions to test
    private readonly string[] _fileExtensions = ["docx", "xlsx", "pptx", "pdf", "one"];
    private readonly WopiActionEnum[] _actions = [WopiActionEnum.View, WopiActionEnum.Edit, WopiActionEnum.EditNew];
    
    [GlobalSetup]
    public void Setup()
    {
        // Use a file system provider with a sample discovery XML
        _xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discovery_large.xml");
        
        // Create sample XML if it doesn't exist
        if (!File.Exists(_xmlPath))
        {
            var sampleXml = CreateLargeDiscoveryXml();
            File.WriteAllText(_xmlPath, sampleXml);
        }

        var discoveryFileProvider = new FileSystemDiscoveryFileProvider(_xmlPath);
        var options = Options.Create(new DiscoveryOptions { NetZone = NetZoneEnum.InternalHttp, RefreshInterval = TimeSpan.FromHours(1) });
        
        // Create the optimized discoverer (current implementation)
        _optimizedDiscoverer = new WopiDiscoverer(discoveryFileProvider, options);

        // Create the original discoverer (for comparison)
        // Note: This is the same implementation, but we're comparing the performance
        // of the current optimized implementation (after our refactoring)
        _originalDiscoverer = new WopiDiscoverer(discoveryFileProvider, options);
    }

    [Benchmark]
    public async Task<bool> SupportsExtension_Optimized()
    {
        return await _optimizedDiscoverer.SupportsExtensionAsync("docx");
    }

    [Benchmark]
    public async Task<bool> SupportsAction_Optimized()
    {
        return await _optimizedDiscoverer.SupportsActionAsync("docx", WopiActionEnum.Edit);
    }

    [Benchmark]
    public async Task<string?> GetUrlTemplate_Optimized()
    {
        return await _optimizedDiscoverer.GetUrlTemplateAsync("docx", WopiActionEnum.Edit);
    }

    [Benchmark]
    public async Task<string?> GetApplicationName_Optimized() 
    {
        return await _optimizedDiscoverer.GetApplicationNameAsync("docx");
    }

    [Benchmark]
    public async Task<Dictionary<string, bool>> BatchExtensionCheck_Optimized()
    {
        var results = new Dictionary<string, bool>();

        foreach (var ext in _fileExtensions)
        {
            results[ext] = await _optimizedDiscoverer.SupportsExtensionAsync(ext);
        }

        return results;
    }

    [Benchmark]
    public async Task<Dictionary<string, Dictionary<WopiActionEnum, bool>>> BatchActionCheck_Optimized()
    {
        var results = new Dictionary<string, Dictionary<WopiActionEnum, bool>>();
        
        foreach (var ext in _fileExtensions)
        {
            var actionResults = new Dictionary<WopiActionEnum, bool>();
            foreach (var action in _actions)
            {
                actionResults[action] = await _optimizedDiscoverer.SupportsActionAsync(ext, action);
            }
            results[ext] = actionResults;
        }
        
        return results;
    }

    [Benchmark]
    public async Task<Dictionary<string, string?>> OptimizedGetAllAppNames()
    {
        var results = new Dictionary<string, string?>();
        
        foreach (var ext in _fileExtensions)
        {
            results[ext] = await _optimizedDiscoverer.GetApplicationNameAsync(ext);
        }
        
        return results;
    }

    [Benchmark]
    public async Task<Dictionary<string, Dictionary<WopiActionEnum, string?>>> OptimizedGetAllUrlTemplates()
    {
        var results = new Dictionary<string, Dictionary<WopiActionEnum, string?>>();
        
        foreach (var ext in _fileExtensions)
        {
            var actionResults = new Dictionary<WopiActionEnum, string?>();
            foreach (var action in _actions)
            {
                actionResults[action] = await _optimizedDiscoverer.GetUrlTemplateAsync(ext, action);
            }
            results[ext] = actionResults;
        }
        
        return results;
    }

    [Benchmark]
    public async Task<Dictionary<string, Dictionary<WopiActionEnum, bool>>> OptimizedGetAllCobaltRequirements()
    {
        var results = new Dictionary<string, Dictionary<WopiActionEnum, bool>>();
        
        foreach (var ext in _fileExtensions)
        {
            var actionResults = new Dictionary<WopiActionEnum, bool>();
            foreach (var action in _actions)
            {
                actionResults[action] = await _optimizedDiscoverer.RequiresCobaltAsync(ext, action);
            }
            results[ext] = actionResults;
        }
        
        return results;
    }

    /// <summary>
    /// Creates a large discovery XML with multiple applications and their supported file extensions.
    /// </summary>
    private string CreateLargeDiscoveryXml()
    {
        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.AppendLine("<wopi-discovery>");
        xml.AppendLine("  <net-zone name=\"internal-http\">");

        // Add Office apps
        AddAppWithActions(xml, "Word", "http://officeserver/wv/resources/1033/FavIcon_Word.ico", 
            new[] { "docx", "doc", "docm", "dot", "dotx", "dotm", "rtf" });

        AddAppWithActions(xml, "Excel", "http://officeserver/x/_layouts/images/FavIcon_Excel.ico", 
            new[] { "xlsx", "xls", "xlsm", "xlst", "xltx", "xltm", "xlsb" });

        AddAppWithActions(xml, "PowerPoint", "http://officeserver/p/_layouts/images/FavIcon_PowerPoint.ico", 
            new[] { "pptx", "ppt", "pptm", "pot", "potx", "potm", "odp" });

        AddAppWithActions(xml, "OneNote", "http://officeserver/n/_layouts/images/FavIcon_OneNote.ico", 
            new[] { "one", "onepkg" });

        AddAppWithActions(xml, "PDF Viewer", "http://officeserver/pdf/favicon.ico", 
            new[] { "pdf" });

        // Add multiple additional apps to make the XML larger
        for (int i = 0; i < 20; i++)
        {
            AddAppWithActions(xml, $"App{i}", $"http://example.com/app{i}/favicon.ico", 
                new[] { $"ext{i}a", $"ext{i}b", $"ext{i}c" });
        }

        xml.AppendLine("  </net-zone>");
        xml.AppendLine("</wopi-discovery>");

        return xml.ToString();
    }

    /// <summary>
    /// Adds an application with actions for each of its supported file extensions to the XML.
    /// </summary>
    private void AddAppWithActions(StringBuilder xml, string appName, string favIconUrl, string[] extensions)
    {
        xml.AppendLine($"    <app name=\"{appName}\" favIconUrl=\"{favIconUrl}\">");
        
        foreach (var ext in extensions)
        {
            // Add VIEW action
            xml.AppendLine($"      <action name=\"VIEW\" ext=\"{ext}\" urlsrc=\"http://example.com/{appName.ToLower()}/view?ext={ext}&amp;ui=UI_LLCC&amp;rs=DC_LLCC\" />");
            
            // Add EDIT action with cobalt requirement for some extensions
            bool requiresCobalt = appName == "Word" || (appName.StartsWith("App") && int.Parse(appName.Substring(3)) % 2 == 0);
            string cobaltReq = requiresCobalt ? ",cobalt" : "";
            xml.AppendLine($"      <action name=\"EDIT\" ext=\"{ext}\" urlsrc=\"http://example.com/{appName.ToLower()}/edit?ext={ext}&amp;ui=UI_LLCC&amp;rs=DC_LLCC\" requires=\"locks,update{cobaltReq}\" />");
            
            // Add EDITNEW action
            xml.AppendLine($"      <action name=\"EDITNEW\" ext=\"{ext}\" urlsrc=\"http://example.com/{appName.ToLower()}/editnew?ext={ext}&amp;ui=UI_LLCC&amp;rs=DC_LLCC\" requires=\"locks,update\" />");
        }
        
        xml.AppendLine("    </app>");
    }
} 