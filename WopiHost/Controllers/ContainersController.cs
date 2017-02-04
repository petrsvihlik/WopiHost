using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.Models;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
	/// </summary>
	[Route("wopi/[controller]")]
	public class ContainersController : WopiControllerBase
	{
		public ContainersController(IConfiguration configuration, IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler) : base(fileProvider, securityHandler, configuration)
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
			Container container = new Container();
			var files = new List<ChildFile>();
			var containers = new List<ChildContainer>();

			foreach (IWopiFile wopiFile in StorageProvider.GetWopiFiles(id))
			{
				files.Add(new ChildFile
				{
					Name = wopiFile.Name,
					Url = GetChildUrl("files", wopiFile.Identifier, AccessToken),
					LastModifiedTime = wopiFile.LastWriteTimeUtc.ToString("o"),
					Size = wopiFile.Size,
					Version = wopiFile.Version
				});
			}

			foreach (IWopiFolder wopiContainer in StorageProvider.GetWopiContainers(id))
			{
				containers.Add(new ChildContainer
				{
					Name = wopiContainer.Name,
					Url = GetChildUrl("containers", wopiContainer.Identifier, AccessToken)
				});
			}

			container.ChildFiles = files;
			container.ChildContainers = containers;

			return container;
		}
	}
}
