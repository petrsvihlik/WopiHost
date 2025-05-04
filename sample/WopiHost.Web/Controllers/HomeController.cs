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
    private readonly WopiUrlBuilder urlGenerator = new(discoverer, new WopiUrlSettings { UiLlcc = new CultureInfo("en-US") });

    public async Task<ActionResult> Index()
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
        try
        {
            var fileViewModels = new List<FileViewModel>();
            await foreach (var file in storageProvider.GetWopiFiles(storageProvider.RootContainerPointer.Identifier))
            {
                fileViewModels.Add(new FileViewModel
                {
                    FileId = file.Identifier,
                    FileName = file.Name + "." + file.Extension,
                    SupportsEdit = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                    SupportsView = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                    IconUri = (await discoverer.GetApplicationFavIconAsync(file.Extension)) ?? new Uri("file.ico", UriKind.Relative)
                });
            }
            return View(fileViewModels);
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

    public async Task<ActionResult> Detail(string id, string wopiAction)
    {
        var actionEnum = Enum.Parse<WopiActionEnum>(wopiAction);
        var securityHandler = new WopiSecurityHandler(loggerFactory); //TODO: via DI

        var file = await storageProvider.GetWopiResource<IWopiFile>(id)
            ?? throw new FileNotFoundException($"File with ID '{id}' not found.");
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

    [Route("Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel 
        { 
            Exception = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error,
            ShowExceptionDetails = true // Set to false in production
        });
    }
}
