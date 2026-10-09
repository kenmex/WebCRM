using Bogus;
using Bogus.DataSets;
using WebCRM.Core.Accounts;
using WebCRM.Core.Entities;
using Address = WebCRM.Core.Entities.Address;

namespace WebCRM.DemoData;

/// <summary>
/// Makes the demo data in memory. The same options (seed and Today) give the same data. Everything it makes already
/// satisfies the database rules (unique account names ignoring accents, valid VAT / Tax IDs, one typed link per activity),
/// because the writer bulk-copies it straight into the tables.
/// </summary>
public sealed class DemoDataGenerator(DemoOptions options, DemoReferenceData reference, IdBases ids)
{
    public const string UserIdPrefix = "demo-";
    public const string TeamNamePrefix = "Demo: ";
    public const string UserDomain = "demo.webcrm.local";

    private readonly Random _rng = new(options.Seed);
    private readonly DateTime _now = options.Today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    private readonly Faker _elFaker = new("el") { Random = new Randomizer(options.Seed + 1) };
    private readonly Faker _enFaker = new("en") { Random = new Randomizer(options.Seed + 2) };
    private readonly Faker _gbFaker = new("en_GB") { Random = new Randomizer(options.Seed + 3) };
    private readonly Faker _deFaker = new("de") { Random = new Randomizer(options.Seed + 4) };

    private readonly DemoDataSet _set = new();
    private readonly HashSet<string> _accountKeys = [];
    private readonly HashSet<string> _vatNumbers = [];
    private readonly List<(string Id, double Weight)> _owners = [];

    // Per account, filled while generating: the market it is in, its mail domain, its contacts and opportunities.
    private readonly List<AccountInfo> _accounts = [];

    private sealed class AccountInfo
    {
        public required Account Account { get; init; }

        public required bool Greek { get; init; }

        public required string Country { get; init; }

        public required string AreaCode { get; init; }

        public required string Domain { get; init; }

        public List<int> Contacts { get; } = [];

        public List<int> Opportunities { get; } = [];
    }

    public DemoDataSet Generate()
    {
        MakeUsers();
        MakeAccounts();
        MakeContacts();
        MakeOpportunities();
        MakeLeads();
        MakeActivities();
        return _set;
    }

    // ---- Users and teams ----

    private void MakeUsers()
    {
        var teams = new[] { (Key: "athens", Name: "Athens"), (Key: "thessaloniki", Name: "Thessaloniki") };
        var teamIds = new Dictionary<string, int>();
        var next = ids.Teams;
        foreach (var team in teams)
        {
            teamIds[team.Key] = ++next;
        }

        AddUser("admin", "Άννα Παπαδοπούλου", RoleNames.Admin, null, 0.2);
        foreach (var team in teams)
        {
            var managerName = team.Key == "athens" ? "Νίκος Καραγιάννης" : "Μαρία Μαυρίδου";
            AddUser($"manager-{team.Key}", managerName, RoleNames.Manager, teamIds[team.Key], 0.5);
            _set.Teams.Add(new Team
            {
                Id = teamIds[team.Key],
                Name = TeamNamePrefix + team.Name,
                ManagerId = $"{UserIdPrefix}manager-{team.Key}",
                CreatedAt = _now.AddDays(-400),
                CreatedBy = reference.SystemUserId,
            });
        }

        var salesNames = new Dictionary<string, string[]>
        {
            ["athens"] = ["Γιώργος Οικονόμου", "Ελένη Βασιλείου", "Peter Hartley", "Σοφία Ζαχαρίου"],
            ["thessaloniki"] = ["Δημήτρης Στεφανίδης", "Κατερίνα Μιχαηλίδου", "Anna Weber", "Θανάσης Λαμπράκης"],
        };
        foreach (var team in teams)
        {
            for (var i = 0; i < salesNames[team.Key].Length; i++)
            {
                AddUser($"sales-{team.Key}-{i + 1}", salesNames[team.Key][i], RoleNames.Sales, teamIds[team.Key], 1.0);
            }
        }
    }

    private void AddUser(string key, string displayName, string role, int? teamId, double weight)
    {
        var id = UserIdPrefix + key;
        var email = $"{key}@{UserDomain}";
        _set.Users.Add(new User
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            DisplayName = displayName,
            TeamId = teamId,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true,
        });
        _set.Roles[id] = role;
        _owners.Add((id, weight));
    }

    private string RandomOwner()
    {
        var total = _owners.Sum(o => o.Weight);
        var pick = _rng.NextDouble() * total;
        foreach (var (id, weight) in _owners)
        {
            pick -= weight;
            if (pick <= 0)
            {
                return id;
            }
        }

        return _owners[^1].Id;
    }

    // ---- Accounts and addresses ----

    private void MakeAccounts()
    {
        for (var i = 0; i < options.Accounts; i++)
        {
            var greek = _rng.NextDouble() < 0.6;
            string country;
            string city;
            string postcode;
            string areaCode;
            string phoneFormat;

            if (greek)
            {
                var (cityName, prefix, area) = Weighted(Pools.GreekCities, Pools.GreekCityWeights);
                country = "GR";
                city = cityName;
                postcode = prefix + Digits(5 - prefix.Length);
                areaCode = area;
                phoneFormat = "GR";
            }
            else
            {
                var market = Pick(Pools.ForeignMarkets);
                country = market.Country;
                city = market.City;
                postcode = Shape(market.PostcodeShape);
                areaCode = market.PhonePrefix;
                phoneFormat = "FOREIGN";
            }

            var name = MakeAccountName(greek, country, city);
            var domain = Domain(name, country);
            var created = Before(_now, 730);

            var account = new Account
            {
                Id = ids.Accounts + i + 1,
                Name = name,
                VatNumber = _rng.NextDouble() < 0.05 ? null : MakeVat(greek, country),
                IndustryId = Pick(reference.IndustryIds),
                AccountStatusId = PickStatus(),
                OwnerId = RandomOwner(),
                ImportBatchId = reference.ImportBatchId,
                CreatedAt = created,
                CreatedBy = reference.SystemUserId,
            };
            account.LegalName = _rng.NextDouble() < 0.7 ? LegalName(name, greek, country) : null;
            account.Email = _rng.NextDouble() < 0.8 ? $"info@{domain}" : null;
            account.Website = _rng.NextDouble() < 0.7 ? $"https://www.{domain}" : null;
            account.Phone = _rng.NextDouble() < 0.9 ? Phone(phoneFormat, areaCode) : null;
            account.TaxOffice = greek && _rng.NextDouble() < 0.9 ? Pick(Pools.TaxOffices) : null;

            _set.Accounts.Add(account);
            _accounts.Add(new AccountInfo { Account = account, Greek = greek, Country = country, AreaCode = areaCode, Domain = domain });

            AddAddress(account, AddressType.Billing, greek, country, city, postcode);
            if (_rng.NextDouble() < 0.3)
            {
                var (shipCity, shipPostcode) = greek
                    ? ShipTo(Pools.GreekCities[_rng.Next(Pools.GreekCities.Length)])
                    : (city, Shape(Pools.ForeignMarkets.First(m => m.City == city).PostcodeShape));
                AddAddress(account, AddressType.Shipping, greek, country, shipCity, shipPostcode);
            }
        }
    }

    private (string City, string Postcode) ShipTo((string City, string PostcodePrefix, string AreaCode) city) =>
        (city.City, city.PostcodePrefix + Digits(5 - city.PostcodePrefix.Length));

    private void AddAddress(Account account, AddressType type, bool greek, string country, string city, string postcode)
    {
        var street = greek ? Pick(Pools.GreekStreets) : Pick(Pools.EnglishStreets);
        _set.Addresses.Add(new Address
        {
            Id = ids.Addresses + _set.Addresses.Count + 1,
            AccountId = account.Id,
            AddressType = type,
            Street = $"{street} {_rng.Next(1, 150)}",
            City = city,
            Postcode = postcode,
            CountryCode = country,
        });
    }

    private string MakeAccountName(bool greek, string country, string city)
    {
        for (var attempt = 0; ; attempt++)
        {
            var name = attempt < 6 ? BaseAccountName(greek, country) : BaseAccountName(greek, country) + " " + _rng.Next(2, 99);
            if (attempt >= 12)
            {
                name = $"{name} ({city}) {_accountKeys.Count}";
            }

            if (name.Length > 190)
            {
                name = name[..190];
            }

            // Unique among active accounts, ignoring case and accents, exactly like the database index.
            if (_accountKeys.Add(Pools.Fold(name)))
            {
                return name;
            }
        }
    }

    private string BaseAccountName(bool greek, string country)
    {
        if (greek)
        {
            var surname = Pick(Pools.GreekSurnames);
            var form = Pick(Pools.GreekCompanyForms);
            return _rng.Next(5) switch
            {
                0 => $"{surname.Male} {Pick(Pools.GreekBusinessWords)} {form}",
                1 => $"{Pick(Pools.GreekBusinessWords)} {surname.Female} {form}",
                2 => $"{surname.Male} & Υιοί {form}",
                3 => $"{Pick(Pools.GreekBusinessWords)} {Pick(Pools.GreekSurnames).Female} & {surname.Male}",
                _ => _elFaker.Company.CompanyName(),
            };
        }

        var faker = country switch { "DE" => _deFaker, "GB" => _gbFaker, _ => _enFaker };
        var company = faker.Company.CompanyName();
        return _rng.Next(3) == 0 ? $"{faker.Name.LastName()} {Pick(Pools.EnglishBusinessWords)}" : company;
    }

    private string LegalName(string name, bool greek, string country)
    {
        if (greek)
        {
            return Pools.GreekCompanyForms.Any(name.EndsWith) ? name : $"{name} {Pick(Pools.GreekCompanyForms)}";
        }

        var form = country switch { "DE" => "GmbH", "GB" => "Ltd", "FR" => "SARL", "IT" => "S.r.l.", "NL" => "B.V.", _ => "Ltd" };
        return $"{name} {form}";
    }

    private string Domain(string name, string country)
    {
        var words = name.Split([' ', '-', '&', ',', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Pools.Slug).Where(w => w.Length > 1).Take(2);
        var slug = string.Concat(words);
        if (slug.Length < 3)
        {
            slug = "company" + _rng.Next(1000);
        }

        var tld = country switch { "GR" or "CY" => _rng.Next(4) == 0 ? "com" : "gr", "DE" => "de", "GB" => "co.uk", "FR" => "fr", "IT" => "it", "NL" => "nl", _ => "com" };
        return $"{(slug.Length > 24 ? slug[..24] : slug)}.{tld}";
    }

    private string? MakeVat(bool greek, string country)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            string vat;
            if (greek)
            {
                // 8 random digits plus the check digit, so the Greek check digit rule passes.
                var first8 = Digits(8);
                vat = "EL" + first8 + AfmCheckDigit(first8);
                if (vat == "EL000000000")
                {
                    continue;
                }
            }
            else
            {
                vat = country switch
                {
                    "DE" => "DE" + Digits(9),
                    "GB" => "GB" + Digits(9),
                    "FR" => "FR" + Digits(11),
                    "IT" => "IT" + Digits(11),
                    "NL" => "NL" + Digits(9) + "B" + Digits(2),
                    "CY" => "CY" + Digits(8) + "L",
                    _ => "XX" + Digits(9),
                };
            }

            if (_vatNumbers.Add(vat))
            {
                return vat;
            }
        }

        return null;
    }

    /// <summary>The Greek ΑΦΜ check digit: the first 8 digits weighted 256 down to 2, sum mod 11 mod 10.</summary>
    public static char AfmCheckDigit(string first8)
    {
        var sum = 0;
        var weight = 256;
        foreach (var digit in first8)
        {
            sum += (digit - '0') * weight;
            weight /= 2;
        }

        return (char)('0' + (sum % 11 % 10));
    }

    private int PickStatus()
    {
        var statuses = reference.AccountStatusIds;
        if (statuses.Count != 3)
        {
            return Pick(statuses);
        }

        // Prospect, Active, Inactive.
        var roll = _rng.NextDouble();
        return roll < 0.25 ? statuses[0] : roll < 0.85 ? statuses[1] : statuses[2];
    }

    // ---- Contacts ----

    private void MakeContacts()
    {
        if (_accounts.Count == 0)
        {
            return;
        }

        var order = Shuffled(_accounts.Count);
        for (var i = 0; i < options.Contacts; i++)
        {
            // Skewed: a few accounts have many contacts, some have none.
            var info = _accounts[order[Skewed(_accounts.Count, 1.3)]];
            var female = _rng.Next(2) == 0;
            var (first, last) = PersonName(info, female);

            var created = Between(info.Account.CreatedAt, _now);
            var contact = new Contact
            {
                Id = ids.Contacts + i + 1,
                FirstName = first,
                LastName = last,
                AccountId = info.Account.Id,
                OwnerId = _rng.NextDouble() < 0.75 ? info.Account.OwnerId : RandomOwner(),
                JobTitle = _rng.NextDouble() < 0.8 ? Pick(Pools.JobTitles) : null,
                Department = _rng.NextDouble() < 0.6 ? Pick(Pools.Departments) : null,
                Email = _rng.NextDouble() < 0.9 ? $"{Initial(first)}.{LocalPart(last)}@{info.Domain}" : null,
                Phone = _rng.NextDouble() < 0.6 ? Phone(info.Greek ? "GR" : "FOREIGN", info.AreaCode) : null,
                Mobile = _rng.NextDouble() < 0.75 ? Mobile(info.Greek) : null,
                SalutationId = SalutationFor(female),
                ImportBatchId = reference.ImportBatchId,
                CreatedAt = created,
                CreatedBy = reference.SystemUserId,
            };

            if (_rng.NextDouble() < 0.03)
            {
                contact.DoNotContact = true;
                contact.DoNotContactSince = Between(created, _now);
            }

            _set.Contacts.Add(contact);
            info.Contacts.Add(_set.Contacts.Count - 1);
        }
    }

    private (string First, string Last) PersonName(AccountInfo account, bool female)
    {
        var greekPerson = account.Greek ? _rng.NextDouble() < 0.9 : account.Country == "CY" || _rng.NextDouble() < 0.05;
        if (greekPerson)
        {
            var surname = Pick(Pools.GreekSurnames);
            return (female ? Pick(Pools.GreekFemaleFirstNames) : Pick(Pools.GreekMaleFirstNames), female ? surname.Female : surname.Male);
        }

        var faker = account.Country switch { "DE" => _deFaker, "GB" => _gbFaker, _ => _enFaker };
        var gender = female ? Name.Gender.Female : Name.Gender.Male;
        return (faker.Name.FirstName(gender), faker.Name.LastName(gender));
    }

    private int? SalutationFor(bool female)
    {
        var roll = _rng.NextDouble();
        return roll < 0.04 ? reference.DrId : roll < 0.72 ? (female ? reference.MsId : reference.MrId) : null;
    }

    // ---- Opportunities ----

    private void MakeOpportunities()
    {
        if (_accounts.Count == 0 || reference.Stages.Count == 0)
        {
            return;
        }

        var order = Shuffled(_accounts.Count);
        var openStages = reference.Stages.Where(s => !s.IsWon && !s.IsLost).ToList();
        var won = reference.Stages.FirstOrDefault(s => s.IsWon);
        var lost = reference.Stages.FirstOrDefault(s => s.IsLost);

        for (var i = 0; i < options.Opportunities; i++)
        {
            var info = _accounts[order[Skewed(_accounts.Count, 1.2)]];
            var account = info.Account;

            // Open stages 65 %, Won 20 %, Lost 15 %.
            var roll = _rng.NextDouble();
            var stage = roll < 0.65 || (won is null && lost is null) ? openStages[_rng.Next(openStages.Count)]
                : roll < 0.85 && won is not null ? won
                : lost ?? won ?? openStages[0];

            var created = Between(account.CreatedAt, _now);
            var opportunity = new Opportunity
            {
                Id = ids.Opportunities + i + 1,
                Name = Trim($"{Pick(Pools.OpportunityTopics)} – {account.Name}", 200),
                AccountId = account.Id,
                StageId = stage.Id,
                Amount = Amount(),
                Currency = "EUR",
                Probability = stage.DefaultProbability,
                OwnerId = _rng.NextDouble() < 0.8 ? account.OwnerId : RandomOwner(),
                CreatedAt = created,
                CreatedBy = reference.SystemUserId,
            };

            if (info.Contacts.Count > 0 && _rng.NextDouble() < 0.7)
            {
                opportunity.PrimaryContactId = _set.Contacts[info.Contacts[_rng.Next(info.Contacts.Count)]].Id;
            }

            if (stage.IsWon || stage.IsLost)
            {
                var closed = Min(created.AddDays(_rng.Next(7, 200)).AddMinutes(_rng.Next(1440)), _now);
                opportunity.ClosedAt = Trunc(closed);
                opportunity.CloseDate = DateOnly.FromDateTime(closed);
                if (stage.IsLost && reference.LostReasonIds.Count > 0)
                {
                    opportunity.LostReasonId = Pick(reference.LostReasonIds);
                }
            }
            else
            {
                opportunity.CloseDate = options.Today.AddDays(_rng.Next(-45, 150));
                if (_rng.NextDouble() < 0.1)
                {
                    opportunity.Probability = _rng.Next(1, 20) * 5;
                    opportunity.ProbabilityOverridden = true;
                }
            }

            _set.Opportunities.Add(opportunity);
            info.Opportunities.Add(_set.Opportunities.Count - 1);
        }
    }

    private decimal Amount()
    {
        // Log-normal: most deals a few thousand euro, a few big ones.
        var amount = Math.Exp(8.6 + Gaussian() * 1.15);
        return Math.Clamp(Math.Round((decimal)amount / 50m) * 50m, 500m, 2_000_000m);
    }

    // ---- Leads ----

    private void MakeLeads()
    {
        for (var i = 0; i < options.Leads; i++)
        {
            var greek = _rng.NextDouble() < 0.7;
            var female = _rng.Next(2) == 0;
            string first;
            string last;
            if (greek)
            {
                var surname = Pick(Pools.GreekSurnames);
                first = female ? Pick(Pools.GreekFemaleFirstNames) : Pick(Pools.GreekMaleFirstNames);
                last = female ? surname.Female : surname.Male;
            }
            else
            {
                var gender = female ? Name.Gender.Female : Name.Gender.Male;
                first = _enFaker.Name.FirstName(gender);
                last = _enFaker.Name.LastName(gender);
            }

            var company = _rng.NextDouble() < 0.6 ? (greek ? BaseAccountName(true, "GR") : _enFaker.Company.CompanyName()) : null;
            var created = Before(_now, 365);
            var lead = new Lead
            {
                Id = ids.Leads + i + 1,
                Name = $"{first} {last}",
                Company = company is null ? null : Trim(company, 200),
                Email = _rng.NextDouble() < 0.85 ? $"{LocalPart(first)}.{LocalPart(last)}@{(greek ? "gmail.com" : "example.com")}" : null,
                Phone = _rng.NextDouble() < 0.6 ? Mobile(greek) : null,
                LeadSourceId = reference.LeadSourceIds.Count == 0 ? null : Pick(reference.LeadSourceIds),
                OwnerId = RandomOwner(),
                CreatedAt = created,
                CreatedBy = reference.SystemUserId,
            };

            var roll = _rng.NextDouble();
            if (roll < 0.15 && _accounts.Count > 0)
            {
                // Converted: it points at the records the conversion made.
                var info = _accounts[_rng.Next(_accounts.Count)];
                lead.LeadStatusId = reference.LeadStatusConvertedId;
                lead.ConvertedAt = Trunc(Min(created.AddDays(_rng.Next(1, 60)), _now));
                lead.ConvertedAccountId = info.Account.Id;
                if (info.Contacts.Count > 0)
                {
                    lead.ConvertedContactId = _set.Contacts[info.Contacts[_rng.Next(info.Contacts.Count)]].Id;
                }

                if (info.Opportunities.Count > 0 && _rng.NextDouble() < 0.5)
                {
                    lead.ConvertedOpportunityId = _set.Opportunities[info.Opportunities[_rng.Next(info.Opportunities.Count)]].Id;
                }
            }
            else if (roll < 0.25)
            {
                lead.LeadStatusId = reference.LeadStatusDisqualifiedId;
            }
            else if (roll < 0.55)
            {
                lead.LeadStatusId = reference.LeadStatusNewId;
            }
            else
            {
                // Contacted or Qualified (or any other open status that exists).
                var open = reference.LeadStatusIds.Where(s => s != reference.LeadStatusConvertedId
                    && s != reference.LeadStatusDisqualifiedId && s != reference.LeadStatusNewId).ToList();
                lead.LeadStatusId = open.Count > 0 ? open[_rng.Next(open.Count)] : reference.LeadStatusNewId;
            }

            _set.Leads.Add(lead);
        }
    }

    // ---- Activities ----

    private void MakeActivities()
    {
        if (_accounts.Count == 0)
        {
            return;
        }

        var accountOrder = Shuffled(_accounts.Count);
        var contactOrder = Shuffled(_set.Contacts.Count);
        var oppOrder = Shuffled(_set.Opportunities.Count);
        var leadOrder = Shuffled(_set.Leads.Count);

        for (var i = 0; i < options.Activities; i++)
        {
            // Where it hangs: account 25 %, contact 45 %, opportunity 25 %, lead 5 %. Exactly one link is set.
            var roll = _rng.NextDouble();
            var activity = new Activity { Id = ids.Activities + i + 1, CreatedBy = reference.SystemUserId };
            DateTime parentCreated;
            string parentOwner;

            if (roll < 0.25 || (_set.Contacts.Count == 0 && _set.Opportunities.Count == 0 && _set.Leads.Count == 0))
            {
                var account = _accounts[accountOrder[Skewed(_accounts.Count, 1.15)]].Account;
                activity.AccountId = account.Id;
                (parentCreated, parentOwner) = (account.CreatedAt, account.OwnerId);
            }
            else if (roll < 0.70 && _set.Contacts.Count > 0)
            {
                var contact = _set.Contacts[contactOrder[Skewed(_set.Contacts.Count, 1.15)]];
                activity.ContactId = contact.Id;
                (parentCreated, parentOwner) = (contact.CreatedAt, contact.OwnerId);
            }
            else if (roll < 0.95 && _set.Opportunities.Count > 0)
            {
                var opportunity = _set.Opportunities[oppOrder[Skewed(_set.Opportunities.Count, 1.15)]];
                activity.OpportunityId = opportunity.Id;
                (parentCreated, parentOwner) = (opportunity.CreatedAt, opportunity.OwnerId);
            }
            else if (_set.Leads.Count > 0)
            {
                var lead = _set.Leads[leadOrder[_rng.Next(_set.Leads.Count)]];
                activity.LeadId = lead.Id;
                (parentCreated, parentOwner) = (lead.CreatedAt, lead.OwnerId);
            }
            else
            {
                var account = _accounts[accountOrder[_rng.Next(_accounts.Count)]].Account;
                activity.AccountId = account.Id;
                (parentCreated, parentOwner) = (account.CreatedAt, account.OwnerId);
            }

            activity.OwnerId = _rng.NextDouble() < 0.85 ? parentOwner : RandomOwner();
            Shape(activity, parentCreated);
            _set.Activities.Add(activity);
        }
    }

    /// <summary>Type, subject, dates and outcome: calls are logged, meetings are done or booked, tasks are done, open or overdue.</summary>
    private void Shape(Activity activity, DateTime parentCreated)
    {
        var created = Between(parentCreated, _now);
        activity.CreatedAt = created;

        var roll = _rng.NextDouble();
        if (roll < 0.40)
        {
            activity.ActivityTypeId = reference.CallTypeId;
            activity.Subject = Pick(Pools.CallSubjects);
            if (_rng.NextDouble() < 0.92)
            {
                activity.DoneAt = created;
                activity.DurationMinutes = _rng.Next(2, 45);
            }
            else
            {
                activity.DueAt = Trunc(_now.AddDays(_rng.Next(0, 10)));
            }
        }
        else if (roll < 0.65)
        {
            activity.ActivityTypeId = reference.MeetingTypeId;
            activity.Subject = Pick(Pools.MeetingSubjects);
            activity.DurationMinutes = _rng.Next(1, 8) * 15;
            if (_rng.NextDouble() < 0.7)
            {
                activity.DueAt = created;
                activity.DoneAt = created;
            }
            else
            {
                activity.DueAt = Trunc(_now.AddDays(_rng.Next(0, 30)).AddHours(_rng.Next(8, 18)));
            }
        }
        else
        {
            activity.ActivityTypeId = reference.TaskTypeId;
            activity.Subject = Pick(Pools.TaskSubjects);
            var due = created.AddDays(_rng.Next(1, 30));
            activity.DueAt = Trunc(due);
            if (_rng.NextDouble() < 0.6)
            {
                activity.DoneAt = Trunc(Min(due.AddHours(_rng.Next(-48, 24)), _now));
            }
        }

        if (activity.DoneAt is not null && _rng.NextDouble() < 0.45)
        {
            activity.Description = Pick(Pools.OutcomeNotes);
        }

        activity.CreatedAt = Trunc(created);
        if (activity.DoneAt is { } done && done < activity.CreatedAt)
        {
            activity.DoneAt = activity.CreatedAt;
        }
    }

    // ---- Small helpers ----

    /// <summary>First letter of a name as a Latin letter; never empty, so an unusual name cannot break an email address.</summary>
    private static string Initial(string name) => LocalPart(name)[..1];

    /// <summary>A name as plain Latin letters for the part of an email address before the @; "x" if nothing is left.</summary>
    private static string LocalPart(string name)
    {
        var slug = Pools.Slug(name);
        return slug.Length > 0 ? slug : "x";
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];

    private T Weighted<T>(IReadOnlyList<T> items, IReadOnlyList<int> weights)
    {
        var pick = _rng.Next(weights.Sum());
        for (var i = 0; i < items.Count; i++)
        {
            pick -= weights[i];
            if (pick < 0)
            {
                return items[i];
            }
        }

        return items[^1];
    }

    private string Digits(int count)
    {
        var chars = new char[Math.Max(0, count)];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)('0' + _rng.Next(10));
        }

        return new string(chars);
    }

    /// <summary>"#" becomes a digit and "A" a letter, anything else is kept: "A# #AA" gives "M1 1AE".</summary>
    private string Shape(string shape) => string.Concat(shape.Select(c => c switch
    {
        '#' => (char)('0' + _rng.Next(10)),
        'A' => (char)('A' + _rng.Next(26)),
        _ => c,
    }));

    private string Phone(string market, string areaCode)
    {
        if (market == "GR")
        {
            var rest = Digits(10 - areaCode.Length);
            return $"+30 {areaCode} {rest[..3]} {rest[3..]}";
        }

        return $"{areaCode} {Digits(3)} {Digits(4)}";
    }

    private string Mobile(bool greek) =>
        greek ? $"+30 69{_rng.Next(10)} {Digits(3)} {Digits(4)}" : $"+44 7{Digits(3)} {Digits(6)}";

    /// <summary>An index in [0, count) that favours low numbers: the larger the exponent, the stronger the skew.</summary>
    private int Skewed(int count, double exponent) =>
        Math.Min(count - 1, (int)(count * Math.Pow(_rng.NextDouble(), exponent)));

    private int[] Shuffled(int count)
    {
        var items = Enumerable.Range(0, count).ToArray();
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }

    private double Gaussian()
    {
        var u1 = 1.0 - _rng.NextDouble();
        var u2 = _rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private DateTime Before(DateTime point, int maxDays) => Trunc(point.AddDays(-_rng.NextDouble() * maxDays));

    private DateTime Between(DateTime from, DateTime to) =>
        to <= from ? Trunc(to) : Trunc(from.AddSeconds(_rng.NextDouble() * (to - from).TotalSeconds));

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    /// <summary>datetime2(0): whole seconds only, so nothing is rounded on the way into the database.</summary>
    private static DateTime Trunc(DateTime value) => new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    private static string Trim(string text, int max) => text.Length <= max ? text : text[..max];
}
