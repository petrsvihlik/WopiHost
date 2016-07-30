using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WopiHost.Abstractions;
using WopiHost.Models;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
	/// </summary>
	[Route("wopi/[controller]")]
	public class FoldersController
	{
		public IWopiFileProvider FileProvider { get; set; }

		public FoldersController(IWopiFileProvider fileProvider)
		{
			FileProvider = fileProvider;
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
		public async Task<FolderChildren> EnumerateChildren(string id, [FromQuery]string access_token)
		{
			FolderChildren fc = new FolderChildren();
			var children = new List<FolderChild>();
			var files = FileProvider.GetWopiFiles();
			foreach (IWopiFile wopiFile in files)
			{
				children.Add(new FolderChild()
				{
					Name = wopiFile.Name,
					Url = "",
					Version = ""
				});
			}
			fc.Children = children;
			return fc;
		}
	}
}
