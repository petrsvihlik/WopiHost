using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WopiHost.Abstractions;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Validator.Models;

namespace WopiHost.Validator.Pages;

public class IndexModel(
    IWopiStorageProvider storageProvider,
    IDiscoverer discoverer) : PageModel
{
    public List<FileViewModel> FileViewModels { get; set; } = [];

    public async Task<ActionResult> OnGet()
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
        try
        {
            var files = storageProvider.GetWopiFiles(storageProvider.RootContainerPointer.Identifier);
            foreach (var file in files)
            {
                FileViewModels.Add(new FileViewModel
                {
                    FileId = file.Identifier,
                    FileName = file.Name,
                    SupportsEdit = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                    SupportsView = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                    IconUri = (await discoverer.GetApplicationFavIconAsync(file.Extension)) ?? new Uri("file.ico", UriKind.Relative)
                });
            }
            return Page();
        }
        catch
        {
            return Redirect("Error");
        }

        //catch (DiscoveryException ex)
        //{
        //    return Redirect("Error");
        //}
        //catch (HttpRequestException ex)
        //{
        //    return Redirect("Error");
        //}
    }
}
