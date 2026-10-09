namespace WebCRM.Core.Entities;

/// <summary>
/// The built-in "system" account that stamps CreatedBy/UpdatedBy for work done outside a
/// signed-in session (seeding, later imports). See docs/adr/0002-system-user.md.
/// </summary>
public static class SystemUser
{
    /// <summary>Fixed AspNetUsers.Id so every environment shares the same value.</summary>
    public const string Id = "00000000-0000-0000-0000-000000000001";

    public const string UserName = "system";

    public const string DisplayName = "System";
}
