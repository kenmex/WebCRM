namespace WebCRM.Core.Entities;

public class Industry : Lookup;

public class LeadSource : Lookup;

public class LeadStatus : Lookup
{
    public const string New = "NEW";
    public const string Converted = "CONVERTED";
    public const string Disqualified = "DISQUALIFIED";
}

public class ActivityType : Lookup
{
    public const string Task = "TASK";
    public const string Call = "CALL";
    public const string Meeting = "MEETING";
}

public class AccountStatus : Lookup;

public class LostReason : Lookup;
