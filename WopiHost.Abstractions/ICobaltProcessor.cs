using System;
using System.IO;
using System.Security.Claims;

namespace WopiHost.Abstractions
{
    /// <summary>
    /// Service that can process MS-FSSHTTP requests.
    /// </summary>
    public interface ICobaltProcessor
    {
        Action<Stream> ProcessCobalt(IWopiFile file, ClaimsPrincipal principal, byte[] newContent);
    }
}
