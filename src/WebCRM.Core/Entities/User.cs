using Microsoft.AspNetCore.Identity;

namespace WebCRM.Core.Entities;

/// <summary>
/// Application user. Extends the ASP.NET Core Identity user (table AspNetUsers);
/// columns and defaults per the User table in docs/data-dictionary.md.
/// </summary>
public class User : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    // FK to Team is configured once the Team entity exists.
    public int? TeamId { get; set; }

    /// <summary>IANA time zone id, e.g. Europe/Athens.</summary>
    public string TimeZoneId { get; set; } = "Europe/Athens";

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public bool EmailReminders { get; set; } = true;

    public bool NotifyAssigned { get; set; } = true;

    public bool NotifyTaskDue { get; set; } = true;

    public bool NotifyRecordChanged { get; set; } = true;

    /// <summary>Deactivated users cannot sign in and leave owner pickers.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set when an Admin sets a temporary password.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? LastSignInAt { get; set; }
}
