using System.Security.Claims;
using Microsoft.Extensions.Options;
using Moq;
using WopiHost.Abstractions;
using WopiHost.Core.Models;
using WopiHost.Core.Security.Authorization;

namespace WopiHost.Core.Tests.Security.Authorization;

public class DefaultWopiPermissionProviderTests
{
    private static DefaultWopiPermissionProvider Build(WopiHostOptions? options = null)
    {
        options ??= new WopiHostOptions
        {
            ClientUrl = new Uri("http://localhost"),
            StorageProviderAssemblyName = "x",
        };
        var monitor = new Mock<IOptionsMonitor<WopiHostOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(options);
        return new DefaultWopiPermissionProvider(monitor.Object);
    }

    [Fact]
    public async Task File_Permissions_From_Claim_Take_Precedence_Over_Defaults()
    {
        var provider = Build();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(WopiClaimTypes.FilePermissions, WopiFilePermissions.ReadOnly.ToString())],
            "test"));

        var perms = await provider.GetFilePermissionsAsync(user, new Mock<IWopiFile>().Object);

        Assert.Equal(WopiFilePermissions.ReadOnly, perms);
    }

    [Fact]
    public async Task File_Permissions_Fall_Back_To_Configured_Defaults_When_No_Claim()
    {
        var options = new WopiHostOptions
        {
            ClientUrl = new Uri("http://localhost"),
            StorageProviderAssemblyName = "x",
            DefaultFilePermissions = WopiFilePermissions.UserCanWrite,
        };
        var provider = Build(options);
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var perms = await provider.GetFilePermissionsAsync(user, new Mock<IWopiFile>().Object);

        Assert.Equal(WopiFilePermissions.UserCanWrite, perms);
    }

    [Fact]
    public async Task Container_Permissions_From_Claim_Take_Precedence_Over_Defaults()
    {
        var provider = Build();
        var perms = WopiContainerPermissions.UserCanRename | WopiContainerPermissions.UserCanCreateChildFile;
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(WopiClaimTypes.ContainerPermissions, perms.ToString())],
            "test"));

        var actual = await provider.GetContainerPermissionsAsync(user, new Mock<IWopiFolder>().Object);

        Assert.Equal(perms, actual);
    }

    [Fact]
    public async Task Container_Permissions_Fall_Back_To_Configured_Defaults_When_No_Claim()
    {
        var options = new WopiHostOptions
        {
            ClientUrl = new Uri("http://localhost"),
            StorageProviderAssemblyName = "x",
            DefaultContainerPermissions = WopiContainerPermissions.UserCanDelete,
        };
        var provider = Build(options);
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var actual = await provider.GetContainerPermissionsAsync(user, new Mock<IWopiFolder>().Object);

        Assert.Equal(WopiContainerPermissions.UserCanDelete, actual);
    }

    [Fact]
    public async Task Unparseable_Claim_Falls_Back_To_Defaults()
    {
        var options = new WopiHostOptions
        {
            ClientUrl = new Uri("http://localhost"),
            StorageProviderAssemblyName = "x",
            DefaultFilePermissions = WopiFilePermissions.UserCanWrite,
        };
        var provider = Build(options);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(WopiClaimTypes.FilePermissions, "this-is-not-a-valid-flags-value-?!")],
            "test"));

        var actual = await provider.GetFilePermissionsAsync(user, new Mock<IWopiFile>().Object);

        Assert.Equal(WopiFilePermissions.UserCanWrite, actual);
    }
}
