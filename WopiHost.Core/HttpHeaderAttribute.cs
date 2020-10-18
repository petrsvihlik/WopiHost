using Microsoft.AspNetCore.Mvc.ActionConstraints;
using System;
using System.Linq;

namespace WopiHost.Core
{
    public class HttpHeaderAttribute : Attribute, IActionConstraint
    {
        public string Header { get; set; }
        public string[] Values { get; set; }

        public HttpHeaderAttribute(string header, params string[] values)
        {
            Header = header;
            Values = values;
        }

        public bool Accept(ActionConstraintContext context)
        {
            return context is null
                ? false
                : context.RouteContext.HttpContext.Request.Headers.TryGetValue(Header, out var value) ? Values.Contains(value[0]) : false;
        }

        public int Order => 0;
    }
}
