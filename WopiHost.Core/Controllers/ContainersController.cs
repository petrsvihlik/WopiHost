using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Controllers
{
    /// <summary>
    /// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
    /// </summary>
    [Route("wopi/[controller]")]
    public class ContainersController : WopiControllerBase
    {
        /// <summary>
        /// Creates an instance of <see cref="ContainersController"/>.
        /// </summary>
        /// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
        /// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
        /// <param name="wopiHostOptions">WOPI Host configuration</param>
        public ContainersController(IOptionsSnapshot<WopiHostOptions> wopiHostOptions, IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler) : base(storageProvider, securityHandler, wopiHostOptions)
        {
        }

        /// <summary>
        /// Returns the metadata about a container specified by an identifier.
        /// Specification: https://wopi.readthedocs.io/projects/wopirest/en/latest/containers/CheckContainerInfo.html
        /// Example URL path: /wopi/containers/(container_id)
        /// </summary>
        /// <param name="id">Container identifier.</param>
        /// <returns></returns>
        [HttpGet("{id}")]
        [Produces("application/json")]
        public CheckContainerInfo GetCheckContainerInfo(string id)
        {
            var container = StorageProvider.GetWopiContainer(id);
            return new CheckContainerInfo
            {
                Name = container.Name
            };
        }


        /// <summary>
        /// The EnumerateChildren method returns the contents of a container on the WOPI server.
        /// Specification: http://wopi.readthedocs.io/projects/wopirest/en/latest/containers/EnumerateChildren.html?highlight=EnumerateChildren
        /// Example URL path: /wopi/containers/(container_id)/children
        /// </summary>
        /// <param name="id">Container identifier.</param>
        /// <returns></returns>
        [HttpGet("{id}/children")]
        [Produces("application/json")]
        public Container EnumerateChildren(string id)
        {
            var container = new Container();
            var files = new List<ChildFile>();
            var containers = new List<ChildContainer>();

            foreach (var wopiFile in StorageProvider.GetWopiFiles(id))
            {
                files.Add(new ChildFile
                {
                    Name = wopiFile.Name,
                    Url = GetWopiUrl("files", wopiFile.Identifier, AccessToken),
                    LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                    Size = wopiFile.Size,
                    Version = wopiFile.Version
                });
            }

            foreach (var wopiContainer in StorageProvider.GetWopiContainers(id))
            {
                containers.Add(new ChildContainer
                {
                    Name = wopiContainer.Name,
                    Url = GetWopiUrl("containers", wopiContainer.Identifier, AccessToken)
                });
            }

            container.ChildFiles = files;
            container.ChildContainers = containers;

            return container;
        }
    }
}
