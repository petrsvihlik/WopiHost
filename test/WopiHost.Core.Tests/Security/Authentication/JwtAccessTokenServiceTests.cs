using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WopiHost.Abstractions;
using WopiHost.Core.Security;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Tests.Security.Authentication;

public class JwtAccessTokenServiceTests
{
    private static JwtAccessTokenService BuildService(WopiSecurityOptions? options = null, TimeProvider? clock = null)
    {
        options ??= new WopiSecurityOptions { SigningKey = JwtAccessTokenService.DeriveHmacKey("unit-test-key") };
        var monitor = new TestOptionsMonitor<WopiSecurityOptions>(options);
        return new JwtAccessTokenService(monitor, NullLogger<JwtAccessTokenService>.Instance, clock);
    }

    [Fact]
    public async Task Issued_Token_Round_Trips_With_Permission_Claims()
    {
        var svc = BuildService();
        var perms = WopiFilePermissions.UserCanWrite | WopiFilePermissions.UserCanRename;

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "alice",
            UserDisplayName = "Alice Example",
            UserEmail = "alice@example.com",
            ResourceId = "file-42",
            ResourceType = WopiResourceType.File,
            FilePermissions = perms,
        });

        Assert.False(string.IsNullOrEmpty(token.Token));

        var validation = await svc.ValidateAsync(token.Token);

        Assert.True(validation.IsValid);
        var p = validation.Principal!;
        Assert.Equal("alice", p.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Alice Example", p.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("alice@example.com", p.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("file-42", p.FindFirstValue(WopiClaimTypes.ResourceId));
        Assert.Equal(WopiResourceType.File.ToString(), p.FindFirstValue(WopiClaimTypes.ResourceType));
        Assert.Equal(perms.ToString(), p.FindFirstValue(WopiClaimTypes.FilePermissions));
    }

    [Fact]
    public async Task Validation_Rejects_Token_Signed_With_Different_Key()
    {
        var aliceSvc = BuildService(new WopiSecurityOptions { SigningKey = JwtAccessTokenService.DeriveHmacKey("alice-key") });
        var bobSvc = BuildService(new WopiSecurityOptions { SigningKey = JwtAccessTokenService.DeriveHmacKey("bob-key") });

        var token = await aliceSvc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
        });

        var result = await bobSvc.ValidateAsync(token.Token);

        Assert.False(result.IsValid);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Validation_Rejects_Expired_Token()
    {
        // JwtSecurityTokenHandler's lifetime check uses wall-clock time, not our TimeProvider,
        // so we issue a very short-lived token with zero skew and wait past it.
        var svc = BuildService(new WopiSecurityOptions
        {
            SigningKey = JwtAccessTokenService.DeriveHmacKey("k"),
            ClockSkew = TimeSpan.Zero,
        });

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
            Lifetime = TimeSpan.FromMilliseconds(1),
        });

        await Task.Delay(50);
        var result = await svc.ValidateAsync(token.Token);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task AdditionalValidationKeys_Accept_Tokens_Issued_With_Previous_Key()
    {
        var oldKey = JwtAccessTokenService.DeriveHmacKey("old-key");
        var newKey = JwtAccessTokenService.DeriveHmacKey("new-key");

        var oldSvc = BuildService(new WopiSecurityOptions { SigningKey = oldKey });
        var token = await oldSvc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
        });

        var rotatedOptions = new WopiSecurityOptions { SigningKey = newKey };
        rotatedOptions.AdditionalValidationKeys.Add(oldKey);
        var newSvc = BuildService(rotatedOptions);

        var result = await newSvc.ValidateAsync(token.Token);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Issuer_And_Audience_Are_Enforced_When_Configured()
    {
        var issuer = "https://wopi.example.com";
        var audience = "wopi-clients";
        var svc = BuildService(new WopiSecurityOptions
        {
            SigningKey = JwtAccessTokenService.DeriveHmacKey("k"),
            Issuer = issuer,
            Audience = audience,
        });

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
        });

        var ok = await svc.ValidateAsync(token.Token);
        Assert.True(ok.IsValid);

        // A service with a different audience must reject the same token.
        var stricterSvc = BuildService(new WopiSecurityOptions
        {
            SigningKey = JwtAccessTokenService.DeriveHmacKey("k"),
            Issuer = issuer,
            Audience = "different-audience",
        });
        var rejected = await stricterSvc.ValidateAsync(token.Token);
        Assert.False(rejected.IsValid);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
