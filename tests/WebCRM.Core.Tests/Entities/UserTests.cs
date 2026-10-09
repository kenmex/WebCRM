using Shouldly;
using WebCRM.Core.Entities;

namespace WebCRM.Core.Tests.Entities;

public class UserTests
{
    [Fact]
    public void New_user_has_data_dictionary_defaults()
    {
        var user = new User();

        user.TimeZoneId.ShouldBe("Europe/Athens");
        user.Theme.ShouldBe(ThemePreference.System);
        user.EmailReminders.ShouldBeTrue();
        user.NotifyAssigned.ShouldBeTrue();
        user.NotifyTaskDue.ShouldBeTrue();
        user.NotifyRecordChanged.ShouldBeTrue();
        user.IsActive.ShouldBeTrue();
        user.MustChangePassword.ShouldBeFalse();
        user.TeamId.ShouldBeNull();
        user.LastSignInAt.ShouldBeNull();
    }

    [Fact]
    public void Theme_values_match_stored_tinyint_codes()
    {
        ((byte)ThemePreference.System).ShouldBe((byte)0);
        ((byte)ThemePreference.Light).ShouldBe((byte)1);
        ((byte)ThemePreference.Dark).ShouldBe((byte)2);
    }
}
