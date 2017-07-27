using System;

namespace WopiHost.Abstractions
{
    [Flags]
    public enum Permission
    {
        None = 0,
        Create = 1,
        Read = 2,
        Update = 4,
        Delte = 8
    }
}
