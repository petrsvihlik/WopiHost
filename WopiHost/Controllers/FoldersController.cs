using Microsoft.AspNet.Mvc;
using WopiHost.Attributes;

namespace WopiHost.Controllers
{
	/// <summary>
	/// Implementation of WOPI server protocol https://msdn.microsoft.com/en-us/library/hh659001.aspx
	/// </summary>
	[Route("wopi/[controller]")]
	[ServiceFilter(typeof(WopiAuthorizationAttribute))]
	public class FoldersController
    {
    }
}
