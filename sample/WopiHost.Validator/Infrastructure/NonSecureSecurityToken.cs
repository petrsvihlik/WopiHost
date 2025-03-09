using Microsoft.IdentityModel.Tokens;

namespace WopiHost.Validator.Infrastructure;

public class NonSecureSecurityToken : SecurityToken
{
    private readonly string _id;
    private readonly string _userName;
    private readonly DateTime _effectiveTime;

    public NonSecureSecurityToken(string userName)
    {
        _userName = userName;
        _id = userName;// Guid.NewGuid().ToString("N");
        _effectiveTime = DateTime.UtcNow;
    }

    public override string Id
    {
        get { return _id; }
    }

    public override DateTime ValidFrom
    {
        get { return _effectiveTime; }
    }

    public override DateTime ValidTo
    {
        // Never expire
        get { return DateTime.MaxValue; }
    }

    public string UserName
    {
        get { return _userName; }
    }

    public override string Issuer => throw new NotImplementedException();

    public override SecurityKey SecurityKey => throw new NotImplementedException();

    public override SecurityKey SigningKey { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
}