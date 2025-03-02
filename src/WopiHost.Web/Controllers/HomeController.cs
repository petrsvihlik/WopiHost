using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;
using WopiHost.Abstractions;
using WopiHost.FileSystemProvider;
using WopiHost.Web.Models;
using Microsoft.Extensions.Options;

namespace WopiHost.Web.Controllers;

public class HomeController(
    IOptions<WopiOptions> wopiOptions, 
    IWopiStorageProvider storageProvider, 
    IDiscoverer discoverer, 
    ILoggerFactory loggerFactory) : Controller
{
    //TODO: remove test culture value and load it from configuration SECTION
    private readonly WopiUrlBuilder urlGenerator = new WopiUrlBuilder(discoverer, new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") });

    public async Task<ActionResult> Index()
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
        try
        {
            var files = storageProvider.GetWopiFiles(storageProvider.RootContainerPointer.Identifier);
            var fileViewModels = new List<FileViewModel>();
            foreach (var file in files)
            {
                fileViewModels.Add(new FileViewModel
                {
                    FileId = file.Identifier,
                    FileName = file.Name,
                    SupportsEdit = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                    SupportsView = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                    IconUri = (await discoverer.GetApplicationFavIconAsync(file.Extension)) ?? new Uri("file.ico", UriKind.Relative)
                });
            }
            return View(fileViewModels);
        }
        catch (DiscoveryException ex)
        {
            return View("Error", ex);
        }
        catch (HttpRequestException ex)
        {
            return View("Error", ex);
        }
    }

    public async Task<ActionResult> Detail(string id, string wopiAction)
    {
        var actionEnum = Enum.Parse<WopiActionEnum>(wopiAction);
        var securityHandler = new WopiSecurityHandler(loggerFactory); //TODO: via DI

        var file = storageProvider.GetWopiFile(id);
        var token = await securityHandler.GenerateAccessToken("Anonymous", file.Identifier);


        ViewData["access_token"] = securityHandler.WriteToken(token);
        //TODO: fix
        //ViewData["access_token_ttl"] = //token.ValidTo

        //http://dotnet-stuff.com/tutorials/aspnet-mvc/how-to-render-different-layout-in-asp-net-mvc


        var extension = file.Extension.TrimStart('.');
        ViewData["urlsrc"] = await urlGenerator.GetFileUrlAsync(extension, new Uri(wopiOptions.Value.HostUrl, $"/wopi/files/{id}"), actionEnum); //TODO: add a test for the URL not to contain double slashes between host and path
        ViewData["favicon"] = await discoverer.GetApplicationFavIconAsync(extension);
        return View();
    }

}
