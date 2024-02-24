using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Mime;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Controllers;

/// <summary>
/// Implementation of WOPI server protocol https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/ecosystem/checkecosystem
/// </summary>
/// <remarks>
/// Creates an instance of <see cref="EcosystemController"/>.
/// </remarks>
/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
/// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
/// <param name="wopiHostOptions">WOPI Host configuration</param>
[Route("wopi/[controller]")]
	public class EcosystemController(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions) : WopiControllerBase(storageProvider, securityHandler, wopiHostOptions)
	{

		/// <summary>
		/// The GetRootContainer operation returns the root container. A WOPI client can use this operation to get a reference to the root container, from which the client can call EnumerateChildren (containers) to navigate a container hierarchy.
		/// Specification: https://learn.microsoft.com/en-us/microsoft-365/cloud-storage-partner-program/rest/ecosystem/getrootcontainer
		/// Example URL: GET /wopi/ecosystem/root_container_pointer
		/// </summary>
		/// <returns></returns>
		[HttpGet("root_container_pointer")]
		[Produces(MediaTypeNames.Application.Json)]
		public RootContainerInfo GetRootContainer() //TODO: fix the path
		{
			var root = StorageProvider.GetWopiContainer(@".\");
			var rc = new RootContainerInfo
			{
				ContainerPointer = new ChildContainer
				{
					Name = root.Name,
					Url = GetWopiUrl("containers", root.Identifier, AccessToken)
				}
			};
			return rc;
		}
	}
