using System.Security.Cryptography;
using System.Text;
using Shouldly;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Entities;
using WebCRM.DemoData;

namespace WebCRM.Data.Tests.Demo;

/// <summary>The generator is plain code: no database. What it makes has to satisfy the database rules, because it is bulk-copied.</summary>
public class DemoDataGeneratorTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    private static readonly DemoReferenceData Reference = new(
        AccountStatusIds: [1, 2, 3],
        IndustryIds: [10, 11, 12, 13],
        MrId: 1, MsId: 2, DrId: 3,
        LeadSourceIds: [1, 2, 3],
        LeadStatusIds: [1, 2, 3, 4, 5],
        LeadStatusNewId: 1, LeadStatusConvertedId: 5, LeadStatusDisqualifiedId: 4,
        LostReasonIds: [1, 2],
        TaskTypeId: 1, CallTypeId: 2, MeetingTypeId: 3,
        Stages:
        [
            new StageInfo(1, 10m, false, false), new StageInfo(2, 20m, false, false), new StageInfo(3, 50m, false, false),
            new StageInfo(4, 75m, false, false), new StageInfo(5, 100m, true, false), new StageInfo(6, 0m, false, true),
        ],
        SystemUserId: SystemUser.Id,
        ImportBatchId: 7);

    private static DemoOptions Options(double scale = 0.05, int seed = 123) =>
        new DemoOptions { Seed = seed, Today = Today }.Scaled(scale);

    private static DemoDataSet Generate(DemoOptions options, IdBases? bases = null) =>
        new DemoDataGenerator(options, Reference, bases ?? IdBases.None).Generate();

    // A small shared set for the many checks that do not need a particular size.
    private static readonly Lazy<DemoDataSet> Shared = new(() => Generate(Options()));

    private static string Fingerprint(DemoDataSet set)
    {
        var text = new StringBuilder();
        foreach (var a in set.Accounts) text.Append(a.Id).Append(a.Name).Append(a.VatNumber).Append(a.Email).Append(a.OwnerId).Append(a.CreatedAt.Ticks).Append('|');
        foreach (var c in set.Contacts) text.Append(c.Id).Append(c.FirstName).Append(c.LastName).Append(c.Email).Append(c.AccountId).Append(c.DoNotContact).Append('|');
        foreach (var o in set.Opportunities) text.Append(o.Id).Append(o.Name).Append(o.Amount).Append(o.StageId).Append(o.CloseDate).Append('|');
        foreach (var l in set.Leads) text.Append(l.Id).Append(l.Name).Append(l.LeadStatusId).Append(l.ConvertedAccountId).Append('|');
        foreach (var a in set.Activities) text.Append(a.Id).Append(a.Subject).Append(a.AccountId).Append(a.ContactId).Append(a.DoneAt?.Ticks).Append('|');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    // ---- Repeatable ----

    [Fact]
    public void The_same_seed_and_date_give_exactly_the_same_data()
    {
        Fingerprint(Generate(Options())).ShouldBe(Fingerprint(Generate(Options())));
    }

    [Fact]
    public void Another_seed_gives_other_data()
    {
        Fingerprint(Generate(Options(seed: 1))).ShouldNotBe(Fingerprint(Generate(Options(seed: 2))));
    }

    [Fact]
    public void The_counts_are_what_was_asked_for_and_the_ids_continue_after_the_existing_ones()
    {
        var options = new DemoOptions { Accounts = 120, Contacts = 400, Leads = 30, Opportunities = 90, Activities = 700, Today = Today };
        var set = Generate(options, new IdBases(Teams: 4, Accounts: 1000, Addresses: 2000, Contacts: 3000, Leads: 40, Opportunities: 50, Activities: 6000));

        set.Accounts.Count.ShouldBe(120);
        set.Contacts.Count.ShouldBe(400);
        set.Leads.Count.ShouldBe(30);
        set.Opportunities.Count.ShouldBe(90);
        set.Activities.Count.ShouldBe(700);
        set.Accounts.Select(a => a.Id).ShouldBe(Enumerable.Range(1001, 120));
        set.Contacts.Select(c => c.Id).ShouldBe(Enumerable.Range(3001, 400));
        set.Activities.Select(a => a.Id).ShouldBe(Enumerable.Range(6001, 700));
        set.Teams.Select(t => t.Id).ShouldBe([5, 6]);
        set.Addresses.Select(a => a.Id).ShouldBe(Enumerable.Range(2001, set.Addresses.Count));
    }

    // ---- Users and teams ----

    [Fact]
    public void There_is_one_admin_two_managers_in_two_teams_and_eight_sales_people_and_every_id_is_marked_demo()
    {
        var set = Shared.Value;

        set.Roles.Values.Count(r => r == RoleNames.Admin).ShouldBe(1);
        set.Roles.Values.Count(r => r == RoleNames.Manager).ShouldBe(2);
        set.Roles.Values.Count(r => r == RoleNames.Sales).ShouldBe(8);
        set.Teams.Count.ShouldBe(2);
        set.Teams.ShouldAllBe(t => t.Name.StartsWith("Demo: "));
        set.Users.ShouldAllBe(u => u.Id.StartsWith("demo-") && u.Email!.EndsWith("@demo.webcrm.local"));
        set.Users.Select(u => u.UserName).Distinct().Count().ShouldBe(set.Users.Count);
        set.Users.Where(u => set.Roles[u.Id] != RoleNames.Admin).ShouldAllBe(u => set.Teams.Any(t => t.Id == u.TeamId));
        set.Teams.ShouldAllBe(t => set.Roles[t.ManagerId!] == RoleNames.Manager);
        set.Users.ShouldAllBe(u => ContactRules.IsValidEmail(u.Email));
        set.Users.Count(u => u.TeamId == set.Teams[0].Id).ShouldBe(5); // a manager and four sales people
    }

    [Fact]
    public void Ownership_is_spread_over_all_the_demo_users()
    {
        var set = Shared.Value;

        var owners = set.Accounts.Select(a => a.OwnerId).Distinct().ToList();
        owners.Count.ShouldBe(set.Users.Count);
        set.Accounts.Concat<BaseEntity>(set.Contacts).Concat(set.Opportunities).Concat(set.Leads).Concat(set.Activities)
            .ShouldAllBe(e => e.CreatedBy == SystemUser.Id);
        set.Opportunities.Select(o => o.OwnerId).Distinct().Count().ShouldBeGreaterThan(8);
        set.Activities.Select(a => a.OwnerId).Distinct().Count().ShouldBeGreaterThan(8);
    }

    // ---- Accounts ----

    [Fact]
    public void Account_names_are_unique_even_when_accents_and_case_are_ignored_like_the_database_index()
    {
        var set = Generate(Options(0.3));

        set.Accounts.Select(a => Pools.Fold(a.Name)).Distinct().Count().ShouldBe(set.Accounts.Count);
        set.Accounts.ShouldAllBe(a => a.Name.Length > 0 && a.Name.Length <= 200);
    }

    [Fact]
    public void Every_vat_number_passes_the_app_rule_and_the_database_check_and_is_unique()
    {
        var set = Generate(Options(0.3));
        var vats = set.Accounts.Where(a => a.VatNumber is not null).Select(a => a.VatNumber!).ToList();

        vats.Count.ShouldBeGreaterThan(set.Accounts.Count * 8 / 10);
        vats.Distinct().Count().ShouldBe(vats.Count);
        foreach (var vat in vats)
        {
            VatNumberRules.Validate(vat).ShouldBeNull(vat);
            VatNumberRules.Normalize(vat).ShouldBe(vat); // already in the stored form
        }

        // CK_Accounts_VatNumber: upper-case letters and digits, 4 to 20, EL + exactly 9 digits.
        vats.All(v => v.Length is >= 4 and <= 20 && v.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c))).ShouldBeTrue();
        vats.Where(v => v.StartsWith("EL")).All(v => v.Length == 11 && VatNumberRules.IsValidGreekAfm(v[2..])).ShouldBeTrue();
        vats.Count(v => v.StartsWith("EL")).ShouldBeGreaterThan(vats.Count / 2);
    }

    [Fact]
    public void Account_emails_websites_and_phones_have_the_right_shape()
    {
        var set = Generate(Options(0.3));

        set.Accounts.Where(a => a.Email is not null).ShouldAllBe(a => ContactRules.IsValidEmail(a.Email) && a.Email == a.Email!.ToLowerInvariant());
        set.Accounts.Where(a => a.Website is not null).ShouldAllBe(a => WebsiteRules.IsValid(a.Website) && a.Website!.Length <= 300);
        set.Accounts.Where(a => a.Phone is not null).ShouldAllBe(a => a.Phone!.Length <= 30 && a.Phone.Any(char.IsAsciiDigit));
        set.Accounts.Where(a => a.LegalName is not null).ShouldAllBe(a => a.LegalName!.Length <= 200);
        set.Accounts.Where(a => a.TaxOffice is not null).ShouldAllBe(a => a.TaxOffice!.Length <= 100);
    }

    [Fact]
    public void Every_account_has_a_billing_address_some_have_a_shipping_one_and_the_fields_fit_the_columns()
    {
        var set = Generate(Options(0.3));

        var byAccount = set.Addresses.GroupBy(a => a.AccountId).ToDictionary(g => g.Key, g => g.ToList());
        set.Accounts.ShouldAllBe(a => byAccount[a.Id].Count(x => x.AddressType == AddressType.Billing) == 1);
        byAccount.Values.ShouldAllBe(list => list.Count(x => x.AddressType == AddressType.Shipping) <= 1);
        set.Addresses.Count(a => a.AddressType == AddressType.Shipping).ShouldBeInRange(set.Accounts.Count / 5, set.Accounts.Count / 2);
        set.Addresses.ShouldAllBe(a => a.CountryCode!.Length == 2 && a.Street!.Length <= 200 && a.City!.Length <= 100 && a.Postcode!.Length <= 20);
    }

    [Fact]
    public void The_data_mixes_Greek_with_accents_and_English()
    {
        var set = Generate(Options(0.3));
        var greekLetters = set.Accounts.Count(a => a.Name.Any(c => c is >= 'Ͱ' and <= 'Ͽ' or >= 'ἀ' and <= '῿'));
        var accented = set.Accounts.Count(a => a.Name.Any(c => "άέήίόύώΆΈΉΊΌΎΏ".Contains(c)));
        var latin = set.Accounts.Count(a => a.Name.All(c => c < 'Ͱ'));

        greekLetters.ShouldBeGreaterThan(set.Accounts.Count / 3);
        accented.ShouldBeGreaterThan(set.Accounts.Count / 10);
        latin.ShouldBeGreaterThan(set.Accounts.Count / 5);
        set.Contacts.Count(c => c.LastName.Any(ch => "άέήίόύώ".Contains(ch))).ShouldBeGreaterThan(set.Contacts.Count / 10);
        set.Addresses.Count(a => a.City == "Αθήνα").ShouldBeGreaterThan(0);
    }

    // ---- Contacts ----

    [Fact]
    public void Contacts_belong_to_real_accounts_are_skewed_and_carry_the_demo_marker()
    {
        var set = Generate(Options(0.3));
        var accountIds = set.Accounts.Select(a => a.Id).ToHashSet();

        set.Contacts.ShouldAllBe(c => accountIds.Contains(c.AccountId));
        set.Accounts.ShouldAllBe(a => a.ImportBatchId == 7);
        set.Contacts.ShouldAllBe(c => c.ImportBatchId == 7);

        var perAccount = set.Contacts.GroupBy(c => c.AccountId).Select(g => g.Count()).ToList();
        perAccount.Max().ShouldBeGreaterThan(set.Contacts.Count / set.Accounts.Count * 4); // a few big accounts
        accountIds.Count(id => set.Contacts.All(c => c.AccountId != id)).ShouldBeGreaterThan(0); // and some with none
    }

    [Fact]
    public void Contact_fields_are_valid_and_fit_the_columns()
    {
        var set = Generate(Options(0.3));

        set.Contacts.ShouldAllBe(c => c.LastName.Length > 0 && c.LastName.Length <= 100 && (c.FirstName == null || c.FirstName.Length <= 100));
        set.Contacts.Where(c => c.Email is not null).ShouldAllBe(c => ContactRules.IsValidEmail(c.Email) && c.Email!.Length <= 254);
        set.Contacts.Count(c => c.Email is null).ShouldBeGreaterThan(0);
        set.Contacts.Where(c => c.Phone is not null).ShouldAllBe(c => c.Phone!.Length <= 30);
        set.Contacts.Where(c => c.Mobile is not null).ShouldAllBe(c => c.Mobile!.Length <= 30);
        set.Contacts.Where(c => c.JobTitle is not null).ShouldAllBe(c => c.JobTitle!.Length <= 100);
        set.Contacts.Where(c => c.Department is not null).ShouldAllBe(c => c.Department!.Length <= 100);
        set.Contacts.Where(c => c.SalutationId is not null).ShouldAllBe(c => c.SalutationId == 1 || c.SalutationId == 2 || c.SalutationId == 3);
    }

    [Fact]
    public void About_three_percent_of_contacts_are_marked_Do_not_contact_with_a_date_that_is_not_in_the_future()
    {
        var set = Generate(Options(0.3));

        var marked = set.Contacts.Where(c => c.DoNotContact).ToList();
        marked.Count.ShouldBeInRange(set.Contacts.Count * 15 / 1000, set.Contacts.Count * 5 / 100);
        marked.ShouldAllBe(c => c.DoNotContactSince != null && c.DoNotContactSince <= Today.ToDateTime(new TimeOnly(12, 0)) && c.DoNotContactSince >= c.CreatedAt);
        set.Contacts.Where(c => !c.DoNotContact).ShouldAllBe(c => c.DoNotContactSince == null);
    }

    // ---- Opportunities ----

    [Fact]
    public void Opportunities_cover_every_stage_and_agree_with_their_stage()
    {
        var set = Generate(Options(0.3));
        var stages = Reference.Stages.ToDictionary(s => s.Id);
        var contactAccount = set.Contacts.ToDictionary(c => c.Id, c => c.AccountId);

        set.Opportunities.Select(o => o.StageId).Distinct().Order().ShouldBe([1, 2, 3, 4, 5, 6]);
        foreach (var o in set.Opportunities)
        {
            var stage = stages[o.StageId];
            o.Amount.ShouldBeInRange(500m, 2_000_000m);
            o.Probability.ShouldBeInRange(0m, 100m);
            o.Name.Length.ShouldBeLessThanOrEqualTo(200);
            o.Currency.ShouldBe("EUR");
            if (o.PrimaryContactId is { } contactId)
            {
                contactAccount[contactId].ShouldBe(o.AccountId); // a contact of the same account
            }

            if (stage.IsWon || stage.IsLost)
            {
                o.ClosedAt.ShouldNotBeNull();
                o.CloseDate.ShouldBe(DateOnly.FromDateTime(o.ClosedAt!.Value));
                o.ClosedAt.Value.ShouldBeLessThanOrEqualTo(Today.ToDateTime(new TimeOnly(12, 0)));
                o.LostReasonId.HasValue.ShouldBe(stage.IsLost);
                o.Probability.ShouldBe(stage.DefaultProbability);
            }
            else
            {
                o.ClosedAt.ShouldBeNull();
                o.LostReasonId.ShouldBeNull();
            }
        }

        // Some open deals are overdue, some are due soon, and some probabilities were edited by hand.
        set.Opportunities.Count(o => o.ClosedAt is null && o.CloseDate < Today).ShouldBeGreaterThan(0);
        set.Opportunities.Count(o => o.ClosedAt is null && o.CloseDate >= Today).ShouldBeGreaterThan(0);
        set.Opportunities.Count(o => o.ProbabilityOverridden).ShouldBeGreaterThan(0);
        set.Opportunities.Where(o => o.ProbabilityOverridden).ShouldAllBe(o => o.ClosedAt == null);
    }

    // ---- Leads ----

    [Fact]
    public void Leads_use_real_statuses_and_converted_ones_point_at_the_records_they_made()
    {
        var set = Generate(Options(0.5));
        var accounts = set.Accounts.ToDictionary(a => a.Id);
        var contacts = set.Contacts.ToDictionary(c => c.Id);
        var opportunities = set.Opportunities.ToDictionary(o => o.Id);

        set.Leads.ShouldAllBe(l => Reference.LeadStatusIds.Contains(l.LeadStatusId));
        set.Leads.Select(l => l.LeadStatusId).Distinct().Count().ShouldBe(5);
        set.Leads.ShouldAllBe(l => l.Name.Length <= 200 && (l.Company == null || l.Company.Length <= 200));
        set.Leads.Where(l => l.Email != null).ShouldAllBe(l => ContactRules.IsValidEmail(l.Email));

        foreach (var lead in set.Leads)
        {
            if (lead.LeadStatusId == Reference.LeadStatusConvertedId)
            {
                lead.ConvertedAt.ShouldNotBeNull();
                accounts.ShouldContainKey(lead.ConvertedAccountId!.Value);
                if (lead.ConvertedContactId is { } contactId)
                {
                    contacts[contactId].AccountId.ShouldBe(lead.ConvertedAccountId.Value);
                }

                if (lead.ConvertedOpportunityId is { } opportunityId)
                {
                    opportunities[opportunityId].AccountId.ShouldBe(lead.ConvertedAccountId.Value);
                }
            }
            else
            {
                lead.ConvertedAt.ShouldBeNull();
                lead.ConvertedAccountId.ShouldBeNull();
                lead.ConvertedContactId.ShouldBeNull();
                lead.ConvertedOpportunityId.ShouldBeNull();
            }
        }
    }

    // ---- Activities ----

    [Fact]
    public void Every_activity_has_exactly_one_typed_link_to_a_record_that_exists_and_all_four_kinds_are_used()
    {
        var set = Generate(Options(0.3));
        var accounts = set.Accounts.Select(a => a.Id).ToHashSet();
        var contacts = set.Contacts.Select(c => c.Id).ToHashSet();
        var opportunities = set.Opportunities.Select(o => o.Id).ToHashSet();
        var leads = set.Leads.Select(l => l.Id).ToHashSet();

        foreach (var a in set.Activities)
        {
            new[] { a.AccountId, a.ContactId, a.OpportunityId, a.LeadId }.Count(x => x.HasValue).ShouldBe(1);
            (a.AccountId is null || accounts.Contains(a.AccountId.Value)).ShouldBeTrue();
            (a.ContactId is null || contacts.Contains(a.ContactId.Value)).ShouldBeTrue();
            (a.OpportunityId is null || opportunities.Contains(a.OpportunityId.Value)).ShouldBeTrue();
            (a.LeadId is null || leads.Contains(a.LeadId.Value)).ShouldBeTrue();
        }

        set.Activities.Count(a => a.AccountId != null).ShouldBeGreaterThan(0);
        set.Activities.Count(a => a.ContactId != null).ShouldBeGreaterThan(set.Activities.Count / 3);
        set.Activities.Count(a => a.OpportunityId != null).ShouldBeGreaterThan(0);
        set.Activities.Count(a => a.LeadId != null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Activities_have_valid_types_dates_durations_and_a_mix_of_done_open_and_overdue()
    {
        var set = Generate(Options(0.3));
        var now = Today.ToDateTime(new TimeOnly(12, 0));

        set.Activities.Select(a => a.ActivityTypeId).Distinct().Order().ShouldBe([1, 2, 3]);
        set.Activities.ShouldAllBe(a => a.Subject.Length > 0 && a.Subject.Length <= 200);
        set.Activities.ShouldAllBe(a => a.CreatedAt <= now && (a.DoneAt == null || (a.DoneAt >= a.CreatedAt && a.DoneAt <= now)));
        set.Activities.Where(a => a.DurationMinutes != null).ShouldAllBe(a => a.DurationMinutes > 0);
        set.Activities.Where(a => a.DoneAt == null).ShouldAllBe(a => a.DueAt != null); // an open activity has a due date

        set.Activities.Count(a => a.DoneAt != null).ShouldBeGreaterThan(set.Activities.Count / 2);
        set.Activities.Count(a => a.DoneAt == null && a.DueAt < now).ShouldBeGreaterThan(0); // overdue
        set.Activities.Count(a => a.DoneAt == null && a.DueAt >= now).ShouldBeGreaterThan(0); // upcoming
    }

    // ---- Dates ----

    [Fact]
    public void Every_timestamp_is_in_whole_seconds_and_not_in_the_future()
    {
        var set = Generate(Options(0.2));
        var now = Today.ToDateTime(new TimeOnly(12, 0));

        var times = set.Accounts.Select(a => a.CreatedAt)
            .Concat(set.Contacts.Select(c => c.CreatedAt))
            .Concat(set.Opportunities.SelectMany(o => new[] { o.CreatedAt, o.ClosedAt ?? o.CreatedAt }))
            .Concat(set.Leads.Select(l => l.CreatedAt))
            .Concat(set.Activities.SelectMany(a => new[] { a.CreatedAt, a.DoneAt ?? a.CreatedAt, a.DueAt ?? a.CreatedAt }));

        foreach (var time in times)
        {
            (time.Ticks % TimeSpan.TicksPerSecond).ShouldBe(0);
            time.Kind.ShouldBe(DateTimeKind.Utc);
        }

        set.Accounts.Concat<BaseEntity>(set.Contacts).Concat(set.Leads).ShouldAllBe(e => e.CreatedAt <= now && e.CreatedAt > now.AddDays(-740));
    }

    // ---- Full size ----

    [Fact]
    [Trait("Category", "Performance")]
    public void The_full_default_volume_generates_quickly_and_keeps_every_rule()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var set = Generate(new DemoOptions { Today = Today });
        clock.Stop();

        set.Accounts.Count.ShouldBe(10_000);
        set.Contacts.Count.ShouldBe(50_000);
        set.Opportunities.Count.ShouldBe(15_000);
        set.Leads.Count.ShouldBe(4_000);
        set.Activities.Count.ShouldBe(200_000);
        set.Accounts.Select(a => Pools.Fold(a.Name)).Distinct().Count().ShouldBe(10_000);
        set.Accounts.Where(a => a.VatNumber != null).Select(a => a.VatNumber).Distinct().Count()
            .ShouldBe(set.Accounts.Count(a => a.VatNumber != null));
        set.Activities.ShouldAllBe(a => new[] { a.AccountId, a.ContactId, a.OpportunityId, a.LeadId }.Count(x => x.HasValue) == 1);
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60), $"generating took {clock.Elapsed.TotalSeconds:N1} s");
    }
}
