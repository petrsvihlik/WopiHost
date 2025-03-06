using System.Globalization;
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
    [BindProperty(SupportsGet = true)]
    public string? ContainerId { get; set; }
    [BindProperty(SupportsGet = true)]
    public string? ParentContainerId { get; set; }

    public List<FileViewModel> Files { get; set; } = [];
    public List<ContainerViewModel> Containers { get; set; } = [];

    public async Task<ActionResult> OnGet()
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
        try
        {
            ContainerId ??= storageProvider.RootContainerPointer.Identifier;
            if (!string.IsNullOrWhiteSpace(ParentContainerId))
            {
                var parentContainer = storageProvider.GetWopiContainer(ParentContainerId);
                Containers.Add(new ContainerViewModel
                {
                    ContainerId = parentContainer.Identifier,
                    Name = ".."
                });
            }
            var containers = storageProvider.GetWopiContainers(ContainerId);
            foreach (var container in containers)
            {
                Containers.Add(new ContainerViewModel
                {
                    ContainerId = container.Identifier,
                    Name = container.Name
                });
            }

            var files = storageProvider.GetWopiFiles(ContainerId);
            foreach (var file in files)
            {
                Files.Add(new FileViewModel
                {
                    FileId = file.Identifier,
                    FileName = file.Name + '.' + file.Extension,
                    LastModified = file.LastWriteTimeUtc.ToLocalTime(),
                    Size = file.Size,
                    FormattedSize = ConvertFileSize(file.Size),
                    SupportsEdit = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.Edit),
                    SupportsView = await discoverer.SupportsActionAsync(file.Extension, WopiActionEnum.View),
                    IconUri = await GetIcon(file) 
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

    private async Task<Uri> GetIcon(IWopiFile file)
    {
        var icon = await discoverer.GetApplicationFavIconAsync(file.Extension)
                    ?? new Uri("file.ico", UriKind.Relative);
        if (!icon.ToString().EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            icon = new Uri("file.ico", UriKind.Relative);
        }
        return icon;
    }

    private static string ConvertFileSize(long fileSize)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        double size = System.Convert.ToDouble(fileSize, CultureInfo.CurrentCulture);
        int unit = 0;

        while (size >= 1024)
        {
            size /= 1024;
            ++unit;
        }

        return string.Format("{0:0.#} {1}", size, units[unit]);
    }
}
