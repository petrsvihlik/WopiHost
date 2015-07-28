using System.Collections.Generic;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Routing;

namespace WopiHost
{
	public class HeaderRouteConstraint : IRouteConstraint
	{
		public bool Match(HttpContext httpContext, IRouter route, string routeKey, IDictionary<string, object> values, RouteDirection routeDirection)
		{
			return true;
		}
	}
}