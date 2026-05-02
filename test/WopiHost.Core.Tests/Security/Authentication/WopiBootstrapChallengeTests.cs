using Microsoft.AspNetCore.Http;
using WopiHost.Core.Security.Authentication;

namespace WopiHost.Core.Tests.Security.Authentication;

public class WopiBootstrapChallengeTests
{
    private static readonly Uri AuthUri = new("https://idp.contoso.com/oauth2/authorize");
    private static readonly Uri TokenUri = new("https://idp.contoso.com/oauth2/token");

    [Fact]
    public void Build_RequiredParametersOnly_EmitsBearerWithBothUris()
    {
        var header = WopiBootstrapChallenge.Build(AuthUri, TokenUri);

        Assert.Equal(
            "Bearer authorization_uri=\"https://idp.contoso.com/oauth2/authorize\", tokenIssuance_uri=\"https://idp.contoso.com/oauth2/token\"",
            header);
    }

    [Fact]
    public void Build_WithProviderId_AppendsParameter()
    {
        var header = WopiBootstrapChallenge.Build(AuthUri, TokenUri, providerId: "tpcontoso");

        Assert.EndsWith(", providerId=\"tpcontoso\"", header);
    }

    [Fact]
    public void Build_WithUrlSchemes_AppendsAlreadyEncodedValueVerbatim()
    {
        // Spec: hosts pre-URL-encode the JSON. The helper must not re-encode.
        var encoded = "%7B%22iOS%22%3A%5B%22contoso%22%5D%7D";
        var header = WopiBootstrapChallenge.Build(AuthUri, TokenUri, urlSchemes: encoded);

        Assert.EndsWith($", UrlSchemes=\"{encoded}\"", header);
    }

    [Fact]
    public void Build_AllParameters_EmitsThemInSpecOrder()
    {
        var header = WopiBootstrapChallenge.Build(
            AuthUri,
            TokenUri,
            providerId: "tpcontoso",
            urlSchemes: "abc");

        Assert.Equal(
            "Bearer authorization_uri=\"https://idp.contoso.com/oauth2/authorize\", tokenIssuance_uri=\"https://idp.contoso.com/oauth2/token\", providerId=\"tpcontoso\", UrlSchemes=\"abc\"",
            header);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("with-hyphen")]
    [InlineData("with_underscore")]
    [InlineData("punct.")]
    [InlineData("emoji😀")]
    public void Build_InvalidProviderId_Throws(string providerId)
    {
        Assert.Throws<ArgumentException>(() =>
            WopiBootstrapChallenge.Build(AuthUri, TokenUri, providerId: providerId));
    }

    [Theory]
    [InlineData("contoso")]
    [InlineData("Contoso")]
    [InlineData("Tp123")]
    [InlineData("ABCXYZ")]
    public void Build_ValidProviderId_Accepted(string providerId)
    {
        var ex = Record.Exception(() => WopiBootstrapChallenge.Build(AuthUri, TokenUri, providerId: providerId));

        Assert.Null(ex);
    }

    [Fact]
    public void Build_NullAuthorizationUri_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WopiBootstrapChallenge.Build(authorizationUri: null!, tokenIssuanceUri: TokenUri));
    }

    [Fact]
    public void Build_NullTokenIssuanceUri_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WopiBootstrapChallenge.Build(authorizationUri: AuthUri, tokenIssuanceUri: null!));
    }

    [Fact]
    public void Apply_Sets401AndAppendsHeader()
    {
        var ctx = new DefaultHttpContext();

        WopiBootstrapChallenge.Apply(ctx.Response, AuthUri, TokenUri, providerId: "tpcontoso");

        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        var header = Assert.Single(ctx.Response.Headers.WWWAuthenticate!);
        Assert.StartsWith("Bearer ", header);
        Assert.Contains("authorization_uri=\"https://idp.contoso.com/oauth2/authorize\"", header);
        Assert.Contains("tokenIssuance_uri=\"https://idp.contoso.com/oauth2/token\"", header);
        Assert.Contains("providerId=\"tpcontoso\"", header);
    }

    [Fact]
    public void Apply_NullResponse_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WopiBootstrapChallenge.Apply(response: null!, AuthUri, TokenUri));
    }
}
