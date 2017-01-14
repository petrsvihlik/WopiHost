using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WopiHost.Url
{
    public class WopiNavigationParameters
    {
	    public string BaseUrl { get; }
	    public string FileIdentifier { get; }
	    public string AccessToken { get; }
	    public string WopiUrl { get; }


	    public WopiNavigationParameters(string wopiUrl)
	    {
		    WopiUrl = wopiUrl;
	    }

	    public WopiNavigationParameters(string baseUrl, string fileIdentifier, string access_token)
	    {
		    BaseUrl = baseUrl;
		    FileIdentifier = fileIdentifier;
		    AccessToken = access_token;
	    }
    }
}
