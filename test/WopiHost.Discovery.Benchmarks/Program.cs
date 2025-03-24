using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Options;
using System.Xml.Linq;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;

namespace WopiHost.Discovery.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Running WopiHost.Discovery benchmarks...");
        
        // Run all benchmark classes
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        
        Console.WriteLine("Benchmarks completed!");
    }
}

[Config(typeof(Config))]
public class WopiDiscovererBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddExporter(MarkdownExporter.GitHub);
            AddDiagnoser(MemoryDiagnoser.Default);
            AddJob(Job.ShortRun);
        }
    }

    private IDiscoverer? _discoverer;
    private readonly string[] _fileExtensions = ["docx", "xlsx", "pptx", "pdf", "one"];
    private readonly WopiActionEnum[] _actions = [WopiActionEnum.View, WopiActionEnum.Edit, WopiActionEnum.EditNew];

    [GlobalSetup]
    public void Setup()
    {
        // Use a file system provider with a sample discovery XML
        string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "discovery.xml");
        
        // Create sample XML if it doesn't exist
        if (!File.Exists(xmlPath))
        {
            var sampleXml = CreateSampleDiscoveryXml();
            File.WriteAllText(xmlPath, sampleXml);
        }

        var discoveryFileProvider = new FileSystemDiscoveryFileProvider(xmlPath);
        var options = Options.Create(new DiscoveryOptions { NetZone = NetZoneEnum.InternalHttp, RefreshInterval = TimeSpan.FromHours(1) });
        _discoverer = new WopiDiscoverer(discoveryFileProvider, options);
    }

    [Benchmark]
    public async Task<List<bool>> SupportsExtension()
    {
        var results = new List<bool>();
        foreach (var extension in _fileExtensions)
        {
            results.Add(await _discoverer!.SupportsExtensionAsync(extension));
        }
        return results;
    }

    [Benchmark]
    public async Task<List<bool>> SupportsAction()
    {
        var results = new List<bool>();
        foreach (var extension in _fileExtensions)
        {
            foreach (var action in _actions)
            {
                results.Add(await _discoverer!.SupportsActionAsync(extension, action));
            }
        }
        return results;
    }

    [Benchmark]
    public async Task<List<bool>> RequiresCobalt()
    {
        var results = new List<bool>();
        foreach (var extension in _fileExtensions)
        {
            foreach (var action in _actions)
            {
                results.Add(await _discoverer!.RequiresCobaltAsync(extension, action));
            }
        }
        return results;
    }

    [Benchmark]
    public async Task<List<string?>> GetUrlTemplate()
    {
        var results = new List<string?>();
        foreach (var extension in _fileExtensions)
        {
            foreach (var action in _actions)
            {
                results.Add(await _discoverer!.GetUrlTemplateAsync(extension, action));
            }
        }
        return results;
    }

    [Benchmark]
    public async Task<List<string?>> GetApplicationName()
    {
        var results = new List<string?>();
        foreach (var extension in _fileExtensions)
        {
            results.Add(await _discoverer!.GetApplicationNameAsync(extension));
        }
        return results;
    }

    [Benchmark]
    public async Task<List<Uri?>> GetApplicationFavIcon()
    {
        var results = new List<Uri?>();
        foreach (var extension in _fileExtensions)
        {
            results.Add(await _discoverer!.GetApplicationFavIconAsync(extension));
        }
        return results;
    }

    [Benchmark]
    public async Task<List<IEnumerable<string>>> GetActionRequirements()
    {
        var results = new List<IEnumerable<string>>();
        foreach (var extension in _fileExtensions)
        {
            foreach (var action in _actions)
            {
                results.Add(await _discoverer!.GetActionRequirementsAsync(extension, action));
            }
        }
        return results;
    }

    private string CreateSampleDiscoveryXml()
    {
        // Create a sample discovery XML with common Office formats
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
<wopi-discovery>
    <net-zone name=""internal-http"">
        <app name=""Word"" favIconUrl=""http://officeserver/wv/resources/1033/FavIcon_Word.ico"">
            <action name=""VIEW"" ext=""docx"" urlsrc=""http://officeserver/wv/wordviewerframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>"" />
            <action name=""EDIT"" ext=""docx"" urlsrc=""http://officeserver/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>"" requires=""locks,update,cobalt"" />
            <action name=""EDITNEW"" ext=""docx"" urlsrc=""http://officeserver/we/wordeditorframe.aspx?new=1&<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>"" requires=""locks,update"" />
        </app>
        <app name=""Excel"" favIconUrl=""http://officeserver/x/_layouts/images/FavIcon_Excel.ico"">
            <action name=""VIEW"" ext=""xlsx"" urlsrc=""http://officeserver/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&>"" />
            <action name=""EDIT"" ext=""xlsx"" urlsrc=""http://officeserver/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""locks,update"" />
            <action name=""EDITNEW"" ext=""xlsx"" urlsrc=""http://officeserver/x/_layouts/xlviewerinternal.aspx?edit=1&new=1&<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""locks,update"" />
        </app>
        <app name=""PowerPoint"" favIconUrl=""http://officeserver/p/_layouts/images/FavIcon_PowerPoint.ico"">
            <action name=""VIEW"" ext=""pptx"" urlsrc=""http://officeserver/p/_layouts/PowerPointFrame.aspx?PowerPointView=ReadingView&<ui=UI_LLCC&><rs=DC_LLCC&>"" />
            <action name=""EDIT"" ext=""pptx"" urlsrc=""http://officeserver/p/_layouts/PowerPointFrame.aspx?<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""locks,update"" />
            <action name=""EDITNEW"" ext=""pptx"" urlsrc=""http://officeserver/p/_layouts/PowerPointFrame.aspx?PowerPointView=EditView&New=1&<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""locks,update"" />
        </app>
        <app name=""OneNote"" favIconUrl=""http://officeserver/o/_layouts/images/FavIcon_OneNote.ico"">
            <action name=""VIEW"" ext=""one"" urlsrc=""http://officeserver/o/_layouts/OneNote.aspx?<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""containers"" />
            <action name=""EDIT"" ext=""one"" urlsrc=""http://officeserver/o/_layouts/OneNote.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""locks,update,containers"" />
        </app>
        <app name=""PDF Viewer"" favIconUrl=""http://officeserver/wv/resources/1033/FavIcon_PDF.ico"">
            <action name=""VIEW"" ext=""pdf"" urlsrc=""http://officeserver/wv/pdfviewerframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&>"" />
        </app>
    </net-zone>
    <net-zone name=""external-https"">
        <app name=""Word"" favIconUrl=""https://Office.com/wv/resources/1033/FavIcon_Word.ico"">
            <action name=""VIEW"" ext=""docx"" urlsrc=""https://Office.com/wv/wordviewerframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>"" />
            <action name=""EDIT"" ext=""docx"" urlsrc=""https://Office.com/we/wordeditorframe.aspx?<ui=UI_LLCC&><rs=DC_LLCC&><showpagestats=PERFSTATS&>"" requires=""locks,update,cobalt"" />
        </app>
        <app name=""Excel"" favIconUrl=""https://Office.com/x/_layouts/images/FavIcon_Excel.ico"">
            <action name=""VIEW"" ext=""xlsx"" urlsrc=""https://Office.com/x/_layouts/xlviewerinternal.aspx?<ui=UI_LLCC&><rs=DC_LLCC&>"" />
            <action name=""EDIT"" ext=""xlsx"" urlsrc=""https://Office.com/x/_layouts/xlviewerinternal.aspx?edit=1&<ui=UI_LLCC&><rs=DC_LLCC&>"" requires=""locks,update"" />
        </app>
    </net-zone>
</wopi-discovery>";
    }
}
