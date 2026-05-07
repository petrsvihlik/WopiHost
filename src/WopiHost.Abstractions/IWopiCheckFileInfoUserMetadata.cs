namespace WopiHost.Abstractions;

/// <summary>
/// User-metadata slice of the WOPI <see href="https://learn.microsoft.com/microsoft-365/cloud-storage-partner-program/rest/files/checkfileinfo">CheckFileInfo</see>
/// response — display name, tenant id, presence info, anonymous flag, license info. Implemented
/// by <see cref="WopiCheckFileInfo"/>.
/// </summary>
public interface IWopiCheckFileInfoUserMetadata
{
    /// <summary>Display name for the user, suitable for UI.</summary>
    string? UserFriendlyName { get; set; }

    /// <summary>UPN-style user principal name (typically email).</summary>
    string? UserPrincipalName { get; set; }

    /// <summary>Round-tripped <c>UserInfo</c> blob set by a previous <c>PutUserInfo</c> call.</summary>
    string UserInfo { get; set; }

    /// <summary>Stable id of the tenant / organization the user belongs to.</summary>
    string? TenantId { get; set; }

    /// <summary>True when the user is anonymous (no signed-in identity).</summary>
    bool IsAnonymousUser { get; set; }

    /// <summary>True when the user has a Microsoft education license.</summary>
    bool IsEduUser { get; set; }

    /// <summary>True when the host is licensed for business editing.</summary>
    bool LicenseCheckForEditIsEnabled { get; set; }

    /// <summary>Host-side stable identifier for the user, distinct from <see cref="IWopiCheckFileInfoIdentity.UserId"/>.</summary>
    string? HostAuthenticationId { get; set; }

    /// <summary>Identifies the provider used for online-presence info.</summary>
    string? PresenceProvider { get; set; }

    /// <summary>Identifies the user within the <see cref="PresenceProvider"/>.</summary>
    string? PresenceUserId { get; set; }
}
