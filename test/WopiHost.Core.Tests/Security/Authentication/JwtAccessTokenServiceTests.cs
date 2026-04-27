using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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

    [Fact]
    public async Task Token_Can_Carry_Both_File_And_Container_Permission_Claims()
    {
        // Office uses one token for file + ancestor-container navigation, so both perm
        // surfaces are written when supplied (see ServiceCollectionExtensions/sample wiring).
        var svc = BuildService();
        var fperms = WopiFilePermissions.UserCanWrite;
        var cperms = WopiContainerPermissions.UserCanCreateChildFile | WopiContainerPermissions.UserCanRename;

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "file-1",
            ResourceType = WopiResourceType.File,
            FilePermissions = fperms,
            ContainerPermissions = cperms,
        });

        var validation = await svc.ValidateAsync(token.Token);

        Assert.True(validation.IsValid);
        var principal = validation.Principal!;
        Assert.Equal(fperms.ToString(), principal.FindFirstValue(WopiClaimTypes.FilePermissions));
        Assert.Equal(cperms.ToString(), principal.FindFirstValue(WopiClaimTypes.ContainerPermissions));
    }

    [Fact]
    public async Task Token_Omits_Permission_Claims_When_None()
    {
        var svc = BuildService();

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
            // No FilePermissions, no ContainerPermissions.
        });
        var validation = await svc.ValidateAsync(token.Token);

        Assert.Null(validation.Principal!.FindFirst(WopiClaimTypes.FilePermissions));
        Assert.Null(validation.Principal!.FindFirst(WopiClaimTypes.ContainerPermissions));
    }

    [Fact]
    public async Task AdditionalClaims_Are_Embedded_In_Token()
    {
        var svc = BuildService();

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
            AdditionalClaims = new Dictionary<string, string>
            {
                ["tenant_id"] = "tenant-42",
                ["session_id"] = "abc",
            },
        });

        var validation = await svc.ValidateAsync(token.Token);
        var principal = validation.Principal!;

        Assert.Equal("tenant-42", principal.FindFirstValue("tenant_id"));
        Assert.Equal("abc", principal.FindFirstValue("session_id"));
    }

    [Fact]
    public async Task Empty_Token_String_Is_Rejected_Without_Throwing()
    {
        var svc = BuildService();

        var result = await svc.ValidateAsync("");

        Assert.False(result.IsValid);
        Assert.Equal("Token is empty.", result.FailureReason);
    }

    [Fact]
    public async Task Asymmetric_SecurityKey_Takes_Precedence_Over_SigningKey_Bytes()
    {
        // Asymmetric pair (RSA). When SecurityKey is set, SigningKey bytes should be ignored.
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var rsaKey = new RsaSecurityKey(rsa);
        var svc = BuildService(new WopiSecurityOptions
        {
            SigningKey = JwtAccessTokenService.DeriveHmacKey("ignored-because-of-securitykey"),
            SecurityKey = rsaKey,
            SigningAlgorithm = SecurityAlgorithms.RsaSha256,
        });

        var token = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
        });
        var result = await svc.ValidateAsync(token.Token);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task DeriveHmacKey_PadsShortSecret_AndPassesThrough_LongSecret()
    {
        var shortKey = JwtAccessTokenService.DeriveHmacKey("short");
        var longKey = JwtAccessTokenService.DeriveHmacKey(new string('x', 64));

        Assert.Equal(32, shortKey.Length);
        Assert.Equal(64, longKey.Length);

        // Both should actually work to sign and validate.
        foreach (var keyBytes in new[] { shortKey, longKey })
        {
            var svc = BuildService(new WopiSecurityOptions { SigningKey = keyBytes });
            var token = await svc.IssueAsync(new WopiAccessTokenRequest
            {
                UserId = "u",
                ResourceId = "r",
                ResourceType = WopiResourceType.File,
            });
            Assert.True((await svc.ValidateAsync(token.Token)).IsValid);
        }
    }

    [Fact]
    public void DeriveHmacKey_Throws_For_Empty_Secret()
    {
        Assert.Throws<ArgumentException>(() => JwtAccessTokenService.DeriveHmacKey(""));
    }

    [Fact]
    public async Task Ephemeral_DevKey_Is_Reused_Across_Calls_When_No_Key_Configured()
    {
        // No SigningKey, no SecurityKey — service generates a random per-process key on
        // first use and reuses it. Issuance + validation should round-trip on the SAME instance.
        var svc = BuildService(new WopiSecurityOptions());

        var t1 = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r",
            ResourceType = WopiResourceType.File,
        });
        var v1 = await svc.ValidateAsync(t1.Token);
        Assert.True(v1.IsValid);

        // A second token from the same instance should also validate (same ephemeral key).
        var t2 = await svc.IssueAsync(new WopiAccessTokenRequest
        {
            UserId = "u",
            ResourceId = "r2",
            ResourceType = WopiResourceType.File,
        });
        Assert.True((await svc.ValidateAsync(t2.Token)).IsValid);
    }

    [Fact]
    public async Task IssueAsync_Throws_For_Null_Request()
    {
        var svc = BuildService();
        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.IssueAsync(null!));
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
