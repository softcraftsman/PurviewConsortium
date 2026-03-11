namespace PurviewConsortium.Core.Enums;

/// <summary>
/// Indicates whether the data share is within the same Azure AD tenant (internal)
/// or crosses tenant boundaries (external).
/// </summary>
public enum ShareType
{
    /// <summary>
    /// Cross-tenant share: requires an External Data Share in Fabric and a consumer-side OneLake shortcut.
    /// </summary>
    External = 0,

    /// <summary>
    /// Same-tenant share: a direct OneLake shortcut can be created without an external data share.
    /// </summary>
    Internal = 1,
}
