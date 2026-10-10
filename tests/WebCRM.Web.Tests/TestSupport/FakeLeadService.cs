using WebCRM.Core.Leads;
using WebCRM.Core.Querying;
using WebCRM.Core.Records;
using WebCRM.Core.Users;

namespace WebCRM.Web.Tests.TestSupport;

/// <summary>An in-memory ILeadService: tests choose what each call returns and read back what was asked.</summary>
public sealed class FakeLeadService : ILeadService
{
    public Dictionary<int, LeadDetail> Leads { get; } = [];

    public Dictionary<int, ConvertPrefill> Prefills { get; } = [];

    public List<(LeadEditModel Model, SaveOptions Options)> Saves { get; } = [];

    public List<LeadConvertRequest> Conversions { get; } = [];

    public List<int> Deleted { get; } = [];

    public List<LeadQuery> Searches { get; } = [];

    public Func<LeadEditModel, SaveResult> OnSave { get; set; } = _ => new SaveResult(SaveStatus.Saved, Id: 42);

    public Func<LeadQuery, PagedResult<LeadListItem>> OnSearch { get; set; } = _ => PagedResult<LeadListItem>.Empty;

    public Func<LeadConvertRequest, ConvertResult> OnConvert { get; set; } =
        _ => new ConvertResult(ConvertStatus.Converted, AccountId: 1, ContactId: 2, OpportunityId: 3);

    public Task<PagedResult<LeadListItem>> SearchAsync(
        LeadQuery query, UserContext user, CancellationToken cancellationToken = default)
    {
        Searches.Add(query);
        return Task.FromResult(OnSearch(query));
    }

    public Task<LeadDetail?> GetAsync(int id, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Leads.GetValueOrDefault(id));

    public Task<LeadEditModel> NewAsync(UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LeadEditModel { LeadStatusId = 1, OwnerId = user.UserId });

    public Task<SaveResult> SaveAsync(
        LeadEditModel model, UserContext user, SaveOptions? options = null, CancellationToken cancellationToken = default)
    {
        Saves.Add((model.Clone(), options ?? new SaveOptions()));
        return Task.FromResult(OnSave(model));
    }

    public Task<bool> DeleteAsync(int id, UserContext user, CancellationToken cancellationToken = default)
    {
        Deleted.Add(id);
        return Task.FromResult(true);
    }

    public Task<ConvertPrefill?> GetConvertPrefillAsync(int leadId, UserContext user, CancellationToken cancellationToken = default) =>
        Task.FromResult(Prefills.GetValueOrDefault(leadId));

    public Task<ConvertResult> ConvertAsync(
        LeadConvertRequest request, UserContext user, CancellationToken cancellationToken = default)
    {
        Conversions.Add(request);
        return Task.FromResult(OnConvert(request));
    }
}
