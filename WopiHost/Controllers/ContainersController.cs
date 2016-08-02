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
	public class ContainersController
	{
		public WopiUrlGenerator _urlGenerator;

		public IWopiFileProvider FileProvider { get; set; }

		public IWopiSecurityHandler SecurityHandler { get; set; }

		public IConfiguration Configuration { get; set; }

		public WopiUrlGenerator UrlGenerator
		{
			//TODO: get url from hosting config
			get { return _urlGenerator ?? (_urlGenerator = new WopiUrlGenerator(Configuration.GetSection("WopiClientUrl").Value, "http://wopihost:5000")); }
		}

		public ContainersController(IConfiguration configuration, IWopiFileProvider fileProvider, IWopiSecurityHandler securityHandler)
		{
			Configuration = configuration;
			FileProvider = fileProvider;
			SecurityHandler = securityHandler;
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
		[Produces("application/json")]
		public async Task<Folder> EnumerateChildren(string id, [FromQuery]string access_token)
		{
			Folder folder = new Folder();
			var children = new List<FolderChild>();
			var files = FileProvider.GetWopiFiles();
			foreach (IWopiFile wopiFile in files)
			{
				//TODO: files vs folders: http://wopi.readthedocs.io/projects/wopirest/en/latest/containers/EnumerateChildren.html?highlight=EnumerateChildren
				children.Add(new FolderChild
				{

					Name = wopiFile.Name,
					Url = (wopiFile.WopiItemType == WopiItemType.File) ? (await UrlGenerator.GetFileUrlAsync(wopiFile.Extension, wopiFile.Identifier, access_token, WopiActionEnum.Edit)) : UrlGenerator.GetContainerUrl(wopiFile.Identifier, access_token),
					Version = ""
				});
			}
			folder.Children = children;
			return folder;
		}
	}
}
