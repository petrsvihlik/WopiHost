using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Models;

namespace WopiHost.Core.Controllers
{
    /// <summary>
    /// Implementation of WOPI server protocol http://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/CheckEcosystem.html
    /// </summary>
    [Route("wopi/[controller]")]
	public class EcosystemController : WopiControllerBase
	{
		/// <summary>
		/// Creates an instance of <see cref="EcosystemController"/>.
		/// </summary>
		/// <param name="storageProvider">Storage provider instance for retrieving files and folders.</param>
		/// <param name="securityHandler">Security handler instance for performing security-related operations.</param>
		/// <param name="wopiHostOptions">WOPI Host configuration</param>
		public EcosystemController(IWopiStorageProvider storageProvider, IWopiSecurityHandler securityHandler, IOptionsSnapshot<WopiHostOptions> wopiHostOptions) 
            : base(storageProvider, securityHandler, wopiHostOptions)
		{
		}

		/// <summary>
		/// The GetRootContainer operation returns the root container. A WOPI client can use this operation to get a reference to the root container, from which the client can call EnumerateChildren (containers) to navigate a container hierarchy.
		/// Specification: http://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/GetRootContainer.html
		/// Example URL: GET /wopi/ecosystem/root_container_pointer
		/// </summary>
		/// <returns></returns>
		[HttpGet("root_container_pointer")]
		[Produces("application/json")]
		public RootContainerInfo GetRootContainer()
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
}
