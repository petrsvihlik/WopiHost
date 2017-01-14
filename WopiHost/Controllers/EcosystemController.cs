using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.Models;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol http://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/CheckEcosystem.html
	/// </summary>
	[Route("wopi/[controller]")]
	public class EcosystemController : WopiControllerBase
	{

		public EcosystemController(IWopiStorageProvider fileProvider, IWopiSecurityHandler securityHandler, IConfiguration configuration) : base(fileProvider, securityHandler, configuration)
		{
		}

		/// <summary>
		/// The GetRootContainer operation returns the root container. A WOPI client can use this operation to get a reference to the root container, from which the client can call EnumerateChildren (containers) to navigate a container hierarchy.
		/// Specification: http://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/GetRootContainer.html
		/// Example URL: GET /wopi/ecosystem/root_container_pointer
		/// </summary>
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpGet("root_container_pointer")]
		[Produces("application/json")]
		public RootContainerInfo GetRootContainer([FromQuery]string access_token)
		{
			var root = StorageProvider.GetWopiContainer(@".\");
			RootContainerInfo rc = new RootContainerInfo
			{
				ContainerPointer = new ChildContainer
				{
					Name = root.Name,
					Url = GetChildUrl("containers", root.Identifier, access_token)
				}
			};
			return rc;
		}
	}
}
