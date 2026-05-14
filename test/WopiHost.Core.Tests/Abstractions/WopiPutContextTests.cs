using System.Collections.ObjectModel;
using System.Security.Claims;
using Moq;
using WopiHost.Abstractions;

namespace WopiHost.Core.Tests.Abstractions;

/// <summary>
/// Smoke tests for the <see cref="WopiPutFileContext"/> and <see cref="WopiPutRelativeFileContext"/>
/// record types. Records are auto-generated, but the property-getter IL is only emitted/exercised
/// when something actually reads the values — which the controllers do at runtime but no other
/// test does directly.
/// </summary>
public class WopiPutContextTests
{
    [Fact]
    public void WopiPutFileContext_PreservesArguments()
    {
        var user = new ClaimsPrincipal();
        var file = Mock.Of<IWopiFile>();
        var editors = new ReadOnlyCollection<string>(["u1", "u2"]);

        var ctx = new WopiPutFileContext(user, file, editors);

        Assert.Same(user, ctx.User);
        Assert.Same(file, ctx.File);
        Assert.Same(editors, ctx.Editors);
    }

    [Fact]
    public void WopiPutFileContext_AllowsNullUser()
    {
        // The User field is nullable on the record — service-to-service callers (a host job that
        // proxies an end-user write) may not have a principal in scope.
        var ctx = new WopiPutFileContext(User: null, Mock.Of<IWopiFile>(), new ReadOnlyCollection<string>([]));

        Assert.Null(ctx.User);
    }

    [Fact]
    public void WopiPutRelativeFileContext_PreservesArguments()
    {
        var user = new ClaimsPrincipal();
        var original = Mock.Of<IWopiFile>();
        var created = Mock.Of<IWopiFile>();

        var ctx = new WopiPutRelativeFileContext(user, original, created, IsFileConversion: true, DeclaredSize: 4096);

        Assert.Same(user, ctx.User);
        Assert.Same(original, ctx.OriginalFile);
        Assert.Same(created, ctx.NewFile);
        Assert.True(ctx.IsFileConversion);
        Assert.Equal(4096, ctx.DeclaredSize);
    }

    [Fact]
    public void WopiPutRelativeFileContext_AllowsNullSizeAndUser()
    {
        // X-WOPI-Size is optional — when absent the controller passes null through.
        var ctx = new WopiPutRelativeFileContext(
            User: null,
            OriginalFile: Mock.Of<IWopiFile>(),
            NewFile: Mock.Of<IWopiFile>(),
            IsFileConversion: false,
            DeclaredSize: null);

        Assert.Null(ctx.User);
        Assert.Null(ctx.DeclaredSize);
        Assert.False(ctx.IsFileConversion);
    }
}
