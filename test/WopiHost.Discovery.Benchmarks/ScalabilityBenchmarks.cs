using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Microsoft.Extensions.Options;
using System.Text;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Benchmarks;

[MemoryDiagnoser]
public class ScalabilityBenchmarks
{
    private Dictionary<string, IDiscoverer> _discoverers = new();

    [Params(5, 20, 50, 100)]
    public int AppCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Create discovery files with different numbers of apps
        foreach (int count in new[] { 5, 20, 50, 100 })
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"discovery_{count}.xml");
            
            if (!File.Exists(xmlPath))
            {
                var xmlContent = GenerateDiscoveryXml(count);
                File.WriteAllText(xmlPath, xmlContent);
            }

            var discoveryFileProvider = new FileSystemDiscoveryFileProvider(xmlPath);
            var options = Options.Create(new DiscoveryOptions { NetZone = NetZoneEnum.InternalHttp, RefreshInterval = TimeSpan.FromHours(1) });
            _discoverers[count.ToString()] = new WopiDiscoverer(discoveryFileProvider, options);
        }
    }

    [Benchmark]
    public async Task<bool> FirstTimeExtensionCheck()
    {
        // Get a fresh discoverer with the specified app count
        string testDiscoveryFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"discovery_{AppCount}.xml");
        var discoveryFileProvider = new FileSystemDiscoveryFileProvider(testDiscoveryFile);
        var options = Options.Create(new DiscoveryOptions { NetZone = NetZoneEnum.InternalHttp, RefreshInterval = TimeSpan.FromHours(1) });
        var discoverer = new WopiDiscoverer(discoveryFileProvider, options);
        
        // First-time check, forces XML parsing
        return await discoverer.SupportsExtensionAsync("docx");
    }

    [Benchmark]
    public async Task<bool> CachedExtensionCheck()
    {
        // Use the pre-initialized discoverer
        var discoverer = _discoverers[AppCount.ToString()];
        return await discoverer.SupportsExtensionAsync("docx");
    }

    [Benchmark]
    public async Task<bool> ActionCheckWithLargeXml()
    {
        var discoverer = _discoverers[AppCount.ToString()];
        
        // Test a mix of common and uncommon extensions
        bool result = true;
        result &= await discoverer.SupportsActionAsync("docx", WopiActionEnum.Edit);
        result &= await discoverer.SupportsActionAsync("xlsx", WopiActionEnum.View);
        result &= await discoverer.SupportsActionAsync($"ext{AppCount/2}b", WopiActionEnum.EditNew);
        
        return result;
    }

    [Benchmark]
    public async Task<Dictionary<string, bool>> BatchExtensionCheck()
    {
        var discoverer = _discoverers[AppCount.ToString()];
        var results = new Dictionary<string, bool>();
        
        // Check multiple extensions in batch
        string[] extensions = ["docx", "xlsx", "pptx", $"ext1a", $"ext{AppCount-1}c"];
        
        foreach (var ext in extensions)
        {
            results[ext] = await discoverer.SupportsExtensionAsync(ext);
        }
        
        return results;
    }

    [Benchmark]
    public async Task<bool> FindRareExtension()
    {
        // Look for a rarely used extension that will be near the end of the XML
        var discoverer = _discoverers[AppCount.ToString()];
        string rareExtension = $"ext{AppCount-1}c";
        
        return await discoverer.SupportsExtensionAsync(rareExtension);
    }

    private string GenerateDiscoveryXml(int appCount)
    {
        var xmlBuilder = new StringBuilder();
        xmlBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xmlBuilder.AppendLine("<wopi-discovery>");
        xmlBuilder.AppendLine("  <net-zone name=\"internal-http\">");
        
        // Always include the basic Office apps
        AddCommonOfficeApps(xmlBuilder);
        
        // Add the specified number of test apps
        for (int i = 1; i <= appCount - 4; i++) // -4 for the office apps we already added
        {
            AddTestApp(xmlBuilder, i);
        }
        
        xmlBuilder.AppendLine("  </net-zone>");
        xmlBuilder.AppendLine("</wopi-discovery>");
        
        return xmlBuilder.ToString();
    }
    
    private void AddCommonOfficeApps(StringBuilder xmlBuilder)
    {
        // Word
        xmlBuilder.AppendLine("    <app name=\"Word\" favIconUrl=\"http://officeserver/word.ico\">");
        xmlBuilder.AppendLine("      <action name=\"VIEW\" ext=\"docx\" urlsrc=\"http://officeserver/word/view.aspx\" />");
        xmlBuilder.AppendLine("      <action name=\"EDIT\" ext=\"docx\" urlsrc=\"http://officeserver/word/edit.aspx\" requires=\"locks,update,cobalt\" />");
        xmlBuilder.AppendLine("      <action name=\"EDITNEW\" ext=\"docx\" urlsrc=\"http://officeserver/word/new.aspx\" requires=\"locks,update\" />");
        xmlBuilder.AppendLine("    </app>");
        
        // Excel
        xmlBuilder.AppendLine("    <app name=\"Excel\" favIconUrl=\"http://officeserver/excel.ico\">");
        xmlBuilder.AppendLine("      <action name=\"VIEW\" ext=\"xlsx\" urlsrc=\"http://officeserver/excel/view.aspx\" />");
        xmlBuilder.AppendLine("      <action name=\"EDIT\" ext=\"xlsx\" urlsrc=\"http://officeserver/excel/edit.aspx\" requires=\"locks,update\" />");
        xmlBuilder.AppendLine("    </app>");
        
        // PowerPoint
        xmlBuilder.AppendLine("    <app name=\"PowerPoint\" favIconUrl=\"http://officeserver/powerpoint.ico\">");
        xmlBuilder.AppendLine("      <action name=\"VIEW\" ext=\"pptx\" urlsrc=\"http://officeserver/powerpoint/view.aspx\" />");
        xmlBuilder.AppendLine("      <action name=\"EDIT\" ext=\"pptx\" urlsrc=\"http://officeserver/powerpoint/edit.aspx\" requires=\"locks,update\" />");
        xmlBuilder.AppendLine("    </app>");
        
        // OneNote
        xmlBuilder.AppendLine("    <app name=\"OneNote\" favIconUrl=\"http://officeserver/onenote.ico\">");
        xmlBuilder.AppendLine("      <action name=\"VIEW\" ext=\"one\" urlsrc=\"http://officeserver/onenote/view.aspx\" requires=\"containers\" />");
        xmlBuilder.AppendLine("      <action name=\"EDIT\" ext=\"one\" urlsrc=\"http://officeserver/onenote/edit.aspx\" requires=\"locks,update,containers\" />");
        xmlBuilder.AppendLine("    </app>");
    }
    
    private void AddTestApp(StringBuilder xmlBuilder, int appNumber)
    {
        xmlBuilder.AppendLine($"    <app name=\"TestApp{appNumber}\" favIconUrl=\"http://officeserver/app{appNumber}.ico\">");
        
        // Each test app has three extensions
        xmlBuilder.AppendLine($"      <action name=\"VIEW\" ext=\"ext{appNumber}a\" urlsrc=\"http://officeserver/app{appNumber}/view.aspx\" />");
        xmlBuilder.AppendLine($"      <action name=\"EDIT\" ext=\"ext{appNumber}a\" urlsrc=\"http://officeserver/app{appNumber}/edit.aspx\" requires=\"locks,update\" />");
        
        xmlBuilder.AppendLine($"      <action name=\"VIEW\" ext=\"ext{appNumber}b\" urlsrc=\"http://officeserver/app{appNumber}/view.aspx\" />");
        xmlBuilder.AppendLine($"      <action name=\"EDIT\" ext=\"ext{appNumber}b\" urlsrc=\"http://officeserver/app{appNumber}/edit.aspx\" requires=\"locks,update\" />");
        xmlBuilder.AppendLine($"      <action name=\"EDITNEW\" ext=\"ext{appNumber}b\" urlsrc=\"http://officeserver/app{appNumber}/new.aspx\" requires=\"locks,update\" />");
        
        xmlBuilder.AppendLine($"      <action name=\"VIEW\" ext=\"ext{appNumber}c\" urlsrc=\"http://officeserver/app{appNumber}/view.aspx\" />");
        
        xmlBuilder.AppendLine("    </app>");
    }
} 