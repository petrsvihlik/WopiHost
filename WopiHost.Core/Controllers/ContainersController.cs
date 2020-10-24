﻿using System.Collections.Generic;
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
        public ContainersController(IOptionsSnapshot<WopiHostOptions> wopiHostOptions, IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler) : base(fileProvider, securityHandler, wopiHostOptions)
        {
        }

        /// <summary>
        /// Returns the metadata about a container specified by an identifier.
        /// Specification: https://msdn.microsoft.com/en-us/library/hh642840.aspx
        /// Example URL: HTTP://server/<...>/wopi*/containers/<id>
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
        /// Specification: https://msdn.microsoft.com/en-us/library/hh641593.aspx
        /// Example URL: HTTP://server/<...>/wopi*/containers/<id>/children
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
