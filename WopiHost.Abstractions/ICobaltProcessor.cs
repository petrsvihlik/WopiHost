using System;
using System.IO;
using System.Security.Claims;

namespace WopiHost.Abstractions
{
    public interface ICobaltProcessor
    {
        Action<Stream> ProcessCobalt(IWopiFile file, ClaimsPrincipal principal, byte[] newContent);
    }
}
