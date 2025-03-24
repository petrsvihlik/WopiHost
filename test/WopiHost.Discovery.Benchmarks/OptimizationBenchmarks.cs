using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Options;
using System.Xml.Linq;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Benchmarks;

[MemoryDiagnoser]
public class OptimizationBenchmarks
{
    // Path to test discovery XML
    private string _xmlPath = "";
    
    // The current optimized implementation
    private IDiscoverer _optimizedDiscoverer;
    
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
    }

    [Benchmark]
    public async Task<bool> OptimizedSupportsExtension()
    {
        return await _optimizedDiscoverer.SupportsExtensionAsync("docx");
    }

    [Benchmark]
    public async Task<bool> OptimizedSupportsAction()
    {
        return await _optimizedDiscoverer.SupportsActionAsync("docx", WopiActionEnum.Edit);
    }

    [Benchmark]
    public async Task<bool> OptimizedRequiresCobalt()
    {
        return await _optimizedDiscoverer.RequiresCobaltAsync("docx", WopiActionEnum.Edit);
    }

    [Benchmark]
    public async Task<string?> OptimizedGetUrlTemplate()
    {
        return await _optimizedDiscoverer.GetUrlTemplateAsync("docx", WopiActionEnum.Edit);
    }

    [Benchmark]
    public async Task<string?> OptimizedGetApplicationName()
    {
        return await _optimizedDiscoverer.GetApplicationNameAsync("docx");
    }

    [Benchmark]
    public async Task<Dictionary<string, bool>> OptimizedBatchExtensionCheck()
    {
        string[] extensions = { "docx", "xlsx", "pptx", "txt", "pdf", "one" };
        var results = new Dictionary<string, bool>();

        foreach (var ext in extensions)
        {
            results[ext] = await _optimizedDiscoverer.SupportsExtensionAsync(ext);
        }

        return results;
    }

    [Benchmark]
    public async Task<Dictionary<string, Dictionary<WopiActionEnum, bool>>> OptimizedBatchActionCheck()
    {
        string[] extensions = { "docx", "xlsx", "pptx", "one" };
        WopiActionEnum[] actions = { WopiActionEnum.View, WopiActionEnum.Edit, WopiActionEnum.EditNew };
        
        var results = new Dictionary<string, Dictionary<WopiActionEnum, bool>>();
        
        foreach (var ext in extensions)
        {
            var actionResults = new Dictionary<WopiActionEnum, bool>();
            foreach (var action in actions)
            {
                actionResults[action] = await _optimizedDiscoverer.SupportsActionAsync(ext, action);
            }
            results[ext] = actionResults;
        }
        
        return results;
    }

    // Creates a large discovery XML for testing
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
            new (string ext, string[] actions)[] {
                ("docx", new[] {"VIEW", "EDIT", "EDITNEW"}),
                ("doc", new[] {"VIEW", "CONVERT"}),
                ("docm", new[] {"VIEW", "EDIT"}),
                ("dotx", new[] {"VIEW", "EDIT", "EDITNEW"})
            });
            
        AddAppWithActions(xmlBuilder, "Excel", "http://officeserver/x/_layouts/images/FavIcon_Excel.ico", 
            new (string ext, string[] actions)[] {
                ("xlsx", new[] {"VIEW", "EDIT", "EDITNEW"}),
                ("xls", new[] {"VIEW", "CONVERT"}),
                ("xlsm", new[] {"VIEW", "EDIT"}),
                ("xltx", new[] {"VIEW", "EDIT"})
            });
            
        AddAppWithActions(xmlBuilder, "PowerPoint", "http://officeserver/p/_layouts/images/FavIcon_PowerPoint.ico", 
            new (string ext, string[] actions)[] {
                ("pptx", new[] {"VIEW", "EDIT", "EDITNEW"}),
                ("ppt", new[] {"VIEW", "CONVERT"}),
                ("pptm", new[] {"VIEW", "EDIT"}),
                ("ppsx", new[] {"VIEW", "EDIT"})
            });
            
        AddAppWithActions(xmlBuilder, "OneNote", "http://officeserver/o/_layouts/images/FavIcon_OneNote.ico", 
            new (string ext, string[] actions)[] {
                ("one", new[] {"VIEW", "EDIT"}),
                ("onetoc2", new[] {"VIEW", "EDIT"})
            });
            
        // Add 100 more apps with multiple extensions to create a very large discovery file
        for (int i = 1; i <= 100; i++)
        {
            AddAppWithActions(xmlBuilder, $"TestApp{i}", $"http://officeserver/test/app{i}.ico", 
                new (string ext, string[] actions)[] {
                    ($"ext{i}a", new[] {"VIEW", "EDIT"}),
                    ($"ext{i}b", new[] {"VIEW", "EDIT", "EDITNEW"}),
                    ($"ext{i}c", new[] {"VIEW"})
                });
        }
        
        xmlBuilder.AppendLine("  </net-zone>");
        
        // Add external-https zone
        xmlBuilder.AppendLine("  <net-zone name=\"external-https\">");
        
        // Repeat some apps in the external zone
        AddAppWithActions(xmlBuilder, "Word", "https://Office.com/wv/resources/1033/FavIcon_Word.ico", 
            new (string ext, string[] actions)[] {
                ("docx", new[] {"VIEW", "EDIT"}),
                ("doc", new[] {"VIEW"})
            });
            
        AddAppWithActions(xmlBuilder, "Excel", "https://Office.com/x/_layouts/images/FavIcon_Excel.ico", 
            new (string ext, string[] actions)[] {
                ("xlsx", new[] {"VIEW", "EDIT"}),
                ("xls", new[] {"VIEW"})
            });
            
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
                
                xmlBuilder.AppendLine($"      <action name=\"{action}\" ext=\"{ext}\" urlsrc=\"http://officeserver/{appName.ToLowerInvariant()}/{action.ToLowerInvariant()}.aspx?ext={ext}&amp;ui=UI_LLCC&amp;rs=DC_LLCC\"{requires} />");
            }
        }
        
        xmlBuilder.AppendLine("    </app>");
    }
} 