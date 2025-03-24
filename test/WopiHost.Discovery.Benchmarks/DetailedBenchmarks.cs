using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Options;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Benchmarks;

[MemoryDiagnoser]
public class DetailedBenchmarks
{
    private WopiDiscoverer? _discoverer;
    private readonly string[] _fileExtensions = ["docx", "xlsx", "pptx", "pdf", "one"];
    private readonly WopiActionEnum[] _actions = [WopiActionEnum.View, WopiActionEnum.Edit, WopiActionEnum.EditNew];
    private string _xmlPath = "";

    [GlobalSetup]
    public void Setup()
    {
        // Use a file system provider with a sample discovery XML
        _xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discovery.xml");
        
        // Create sample XML if it doesn't exist
        if (!File.Exists(_xmlPath))
        {
            var sampleXml = CreateLargeDiscoveryXml();
            File.WriteAllText(_xmlPath, sampleXml);
        }

        var discoveryFileProvider = new FileSystemDiscoveryFileProvider(_xmlPath);
        var options = Options.Create(new DiscoveryOptions { NetZone = NetZoneEnum.InternalHttp, RefreshInterval = TimeSpan.FromHours(1) });
        _discoverer = new WopiDiscoverer(discoveryFileProvider, options);
    }

    [Benchmark]
    public async Task<bool> SingleExtensionCheck() => 
        await _discoverer!.SupportsExtensionAsync("docx");

    [Benchmark]
    public async Task<bool> SingleActionCheck() => 
        await _discoverer!.SupportsActionAsync("docx", WopiActionEnum.Edit);

    [Benchmark]
    public async Task<string?> GetSingleUrlTemplate() => 
        await _discoverer!.GetUrlTemplateAsync("docx", WopiActionEnum.View);

    [Benchmark]
    public async Task<bool> RequiresCobaltCheck() => 
        await _discoverer!.RequiresCobaltAsync("docx", WopiActionEnum.Edit);

    [Benchmark]
    public async Task<string?> GetSingleAppName() => 
        await _discoverer!.GetApplicationNameAsync("xlsx");

    // Tests caching by calling the same method multiple times
    [Benchmark]
    public async Task<bool> CacheEfficiencyTest()
    {
        // The first call should parse XML and cache results
        bool result = await _discoverer!.SupportsExtensionAsync("docx");
        
        // Subsequent calls should use the cache
        for (int i = 0; i < 10; i++)
        {
            result &= await _discoverer!.SupportsExtensionAsync("docx");
        }
        
        return result;
    }

    // Tests multiple parallel requests to the discoverer
    [Benchmark]
    public async Task<bool[]> ParallelRequests()
    {
        var tasks = new List<Task<bool>>();
        
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_discoverer!.SupportsExtensionAsync("docx"));
        }
        
        return await Task.WhenAll(tasks);
    }

    // Tests various file extensions for both View and Edit
    [Benchmark]
    public async Task<Dictionary<string, Dictionary<WopiActionEnum, bool>>> FullDiscoveryTest()
    {
        var results = new Dictionary<string, Dictionary<WopiActionEnum, bool>>();
        
        foreach (var ext in _fileExtensions)
        {
            var actionResults = new Dictionary<WopiActionEnum, bool>();
            foreach (var action in _actions)
            {
                actionResults[action] = await _discoverer!.SupportsActionAsync(ext, action);
            }
            results[ext] = actionResults;
        }
        
        return results;
    }

    private string CreateLargeDiscoveryXml()
    {
        // Starting with base XML document
        var xmlBuilder = new System.Text.StringBuilder();
        xmlBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xmlBuilder.AppendLine("<wopi-discovery>");
        
        // Add internal-http zone
        xmlBuilder.AppendLine("  <net-zone name=\"internal-http\">");
        
        // Common MS Office formats
        AddAppWithActions(xmlBuilder, "Word", "http://officeserver/wv/resources/1033/FavIcon_Word.ico", 
            [
                ("docx", new[] {"VIEW", "EDIT", "EDITNEW"}),
                ("doc", new[] {"VIEW", "CONVERT"}),
                ("docm", new[] {"VIEW", "EDIT"}),
                ("dotx", new[] {"VIEW", "EDIT", "EDITNEW"})
            ]);
            
        AddAppWithActions(xmlBuilder, "Excel", "http://officeserver/x/_layouts/images/FavIcon_Excel.ico", 
            [
                ("xlsx", new[] {"VIEW", "EDIT", "EDITNEW"}),
                ("xls", new[] {"VIEW", "CONVERT"}),
                ("xlsm", new[] {"VIEW", "EDIT"}),
                ("xltx", new[] {"VIEW", "EDIT"})
            ]);
            
        AddAppWithActions(xmlBuilder, "PowerPoint", "http://officeserver/p/_layouts/images/FavIcon_PowerPoint.ico", 
            [
                ("pptx", new[] {"VIEW", "EDIT", "EDITNEW"}),
                ("ppt", new[] {"VIEW", "CONVERT"}),
                ("pptm", new[] {"VIEW", "EDIT"}),
                ("ppsx", new[] {"VIEW", "EDIT"})
            ]);
            
        AddAppWithActions(xmlBuilder, "OneNote", "http://officeserver/o/_layouts/images/FavIcon_OneNote.ico", 
            [
                ("one", new[] {"VIEW", "EDIT"}),
                ("onetoc2", new[] {"VIEW", "EDIT"})
            ]);
            
        // Add 10 more apps with multiple extensions to create a large discovery file
        for (int i = 1; i <= 10; i++)
        {
            AddAppWithActions(xmlBuilder, $"TestApp{i}", $"http://officeserver/test/app{i}.ico", 
                [
                    ($"ext{i}a", new[] {"VIEW", "EDIT"}),
                    ($"ext{i}b", new[] {"VIEW", "EDIT", "EDITNEW"}),
                    ($"ext{i}c", new[] {"VIEW"})
                ]);
        }
        
        xmlBuilder.AppendLine("  </net-zone>");
        
        // Add external-https zone
        xmlBuilder.AppendLine("  <net-zone name=\"external-https\">");
        
        // Repeat some apps in the external zone
        AddAppWithActions(xmlBuilder, "Word", "https://Office.com/wv/resources/1033/FavIcon_Word.ico", 
            [
                ("docx", new[] {"VIEW", "EDIT"}),
                ("doc", new[] {"VIEW"})
            ]);
            
        AddAppWithActions(xmlBuilder, "Excel", "https://Office.com/x/_layouts/images/FavIcon_Excel.ico", 
            [
                ("xlsx", new[] {"VIEW", "EDIT"}),
                ("xls", new[] {"VIEW"})
            ]);
            
        xmlBuilder.AppendLine("  </net-zone>");
        xmlBuilder.AppendLine("</wopi-discovery>");
        
        return xmlBuilder.ToString();
    }
    
    private void AddAppWithActions(System.Text.StringBuilder xmlBuilder, string appName, string favIconUrl, 
        (string ext, string[] actions)[] extActions)
    {
        xmlBuilder.AppendLine($"    <app name=\"{appName}\" favIconUrl=\"{favIconUrl}\">");
        
        foreach (var (ext, actions) in extActions)
        {
            foreach (var action in actions)
            {
                string requires = "";
                if (action == "EDIT" || action == "EDITNEW")
                {
                    requires = " requires=\"locks,update\"";
                    if (appName == "Word" && ext == "docx" && action == "EDIT")
                    {
                        requires = " requires=\"locks,update,cobalt\"";
                    }
                }
                else if (action == "VIEW" && appName == "OneNote")
                {
                    requires = " requires=\"containers\"";
                }
                
                xmlBuilder.AppendLine($"      <action name=\"{action}\" ext=\"{ext}\" urlsrc=\"http://officeserver/{appName.ToLowerInvariant()}/{action.ToLowerInvariant()}.aspx?ext={ext}\"{requires} />");
            }
        }
        
        xmlBuilder.AppendLine("    </app>");
    }
} 