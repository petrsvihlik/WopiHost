using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WopiHost.Abstractions;
using WopiHost.Discovery.Enumerations;
using WopiHost.Models;
using WopiHost.Url;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
	/// </summary>
	[Route("wopi/[controller]")]
	public class ContainersController : WopiControllerBase
	{
		public ContainersController(IConfiguration configuration, IWopiFileProvider fileProvider, IWopiSecurityHandler securityHandler) : base(fileProvider, securityHandler, configuration)
		{
		}

		/// <summary>
		/// Returns the metadata about a folder specified by an identifier.
		/// Specification: https://msdn.microsoft.com/en-us/library/hh642840.aspx
		/// Example URL: HTTP://server/<...>/wopi*/folders/<id>
		/// </summary>
		/// <param name="id">Folder identifier.</param>
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpGet("{id}")]
		[Produces("application/json")]
		public async Task<CheckFolderInfo> GetCheckFolderInfo(string id, [FromQuery]string access_token)
		{
			throw new NotImplementedException();
		}


		/// <summary>
		/// The EnumerateChildren method returns the contents of a folder on the WOPI server.
		/// Specification: https://msdn.microsoft.com/en-us/library/hh641593.aspx
		/// Example URL: HTTP://server/<...>/wopi*/folders/<id>/children
		/// </summary>
		/// <param name="id">Folder identifier.</param>
		/// <param name="access_token">Access token used to validate the request.</param>
		/// <returns></returns>
		[HttpGet("{id}/children")]
		//[HttpGet("/children")]
		[Produces("application/json")]
		public async Task<Container> EnumerateChildren(string id, [FromQuery]string access_token)
		{
			Container folder = new Container();
			var files = new List<ChildFile>();
			var containers = new List<ChildContainer>();

			foreach (IWopiFile wopiFile in FileProvider.GetWopiFiles(id))
			{
				files.Add(new ChildFile
				{
					//TODO: add all properties http://wopi.readthedocs.io/projects/wopirest/en/latest/containers/EnumerateChildren.html?highlight=EnumerateChildren
					Name = wopiFile.Name,
					Url = await UrlGenerator.GetFileUrlAsync(wopiFile.Extension, wopiFile.Identifier, access_token, WopiActionEnum.Edit)
				});
			}

			foreach (IWopiItem wopiFolder in FileProvider.GetWopiContainers(id))
			{
				containers.Add(new ChildContainer
				{
					//TODO: add all properties
					Name = wopiFolder.Name,
					Url = UrlGenerator.GetContainerUrl(wopiFolder.Identifier, access_token)
				});
			}

			folder.ChildFiles = files;
			folder.ChildContainers = containers;

			return folder;
		}
	}
}
