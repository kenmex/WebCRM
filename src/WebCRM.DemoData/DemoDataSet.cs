using WebCRM.Core.Entities;

namespace WebCRM.DemoData;

/// <summary>A pipeline stage as the generator needs to know it.</summary>
public sealed record StageInfo(int Id, decimal DefaultProbability, bool IsWon, bool IsLost);

/// <summary>
/// The ids of the lookup rows that already exist in the database (the generator never invents ids for lookups),
/// plus the system user that stamps every row and the "Demo" ImportBatch that marks demo accounts and contacts.
/// </summary>
public sealed record DemoReferenceData(
    IReadOnlyList<int> AccountStatusIds,
    IReadOnlyList<int> IndustryIds,
    int MrId,
    int MsId,
    int DrId,
    IReadOnlyList<int> LeadSourceIds,
    IReadOnlyList<int> LeadStatusIds,
    int LeadStatusNewId,
    int LeadStatusConvertedId,
    int LeadStatusDisqualifiedId,
    IReadOnlyList<int> LostReasonIds,
    int TaskTypeId,
    int CallTypeId,
    int MeetingTypeId,
    IReadOnlyList<StageInfo> Stages,
    string SystemUserId,
    int ImportBatchId);

/// <summary>The highest id each identity table already has; new rows are numbered from there, so children can point at parents without reading ids back.</summary>
public sealed record IdBases(int Teams, int Accounts, int Addresses, int Contacts, int Leads, int Opportunities, int Activities)
{
    public static IdBases None { get; } = new(0, 0, 0, 0, 0, 0, 0);
}

public sealed class DemoDataSet
{
    public List<Team> Teams { get; } = [];

    /// <summary>Users without a password hash; the writer adds that.</summary>
    public List<User> Users { get; } = [];

    /// <summary>User id to role name.</summary>
    public Dictionary<string, string> Roles { get; } = [];

    public List<Account> Accounts { get; } = [];

    public List<Address> Addresses { get; } = [];

    public List<Contact> Contacts { get; } = [];

    public List<Opportunity> Opportunities { get; } = [];

    public List<Lead> Leads { get; } = [];

    public List<Activity> Activities { get; } = [];
}
