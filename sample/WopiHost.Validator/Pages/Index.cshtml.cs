using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
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

    public string ContainerName { get; set; } = string.Empty;
    public List<ChildContainer> BreadcrumbParts { get; set; } = [];
    public List<FileViewModel> Files { get; set; } = [];
    public List<ContainerViewModel> Containers { get; set; } = [];

    public async Task<ActionResult> OnGet(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Welcome to WOPI HOST test page";
        try
        {
            // setup title
            ContainerId ??= storageProvider.RootContainerPointer.Identifier;
            ContainerName = (await storageProvider.GetWopiResource<IWopiFolder>(ContainerId, cancellationToken))?.Name
                ?? throw new InvalidOperationException("Container not found");
            // calc breadcrumb
            if (ContainerId != storageProvider.RootContainerPointer.Identifier)
            {
                var ancestors = await storageProvider.GetAncestors<IWopiFolder>(ContainerId, cancellationToken);
                for (var i=0; i<ancestors.Count; i++)
                {
                    var ancestor = ancestors[i];
                    var parentId = i > 0
                        ? ancestors[i - 1].Identifier
                        : null;
                    var part = new ChildContainer(
                        ancestor.Name,
                        Url.Page(pageName: null, values: new 
                        { 
                            ContainerId = ancestor.Identifier,
                            ParentContainerId = parentId
                        })!
                    );
                    BreadcrumbParts.Add(part);
                }
                if (string.IsNullOrWhiteSpace(ParentContainerId))
                {
                    ParentContainerId = ancestors.Count > 0
                        ? ancestors[^1].Identifier
                        : null;
                }
            }

            // allow to navigate to parent container
            if (!string.IsNullOrWhiteSpace(ParentContainerId))
            {
                var parentContainer = await storageProvider.GetWopiResource<IWopiFolder>(ParentContainerId, cancellationToken)
                    ?? throw new InvalidOperationException("Parent container not found");
                Containers.Add(new ContainerViewModel
                {
                    ContainerId = parentContainer.Identifier,
                    Name = ".."
                });
            }
            // get child containers
            await foreach (var container in storageProvider.GetWopiContainers(ContainerId, cancellationToken))
            {
                Containers.Add(new ContainerViewModel
                {
                    ContainerId = container.Identifier,
                    Name = container.Name
                });
            }

            // get files
            await foreach (var file in storageProvider.GetWopiFiles(ContainerId, cancellationToken: cancellationToken))
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
                    IconUri = await GetFileIcon(file) 
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

    private async Task<Uri> GetFileIcon(IWopiFile file)
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
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
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
