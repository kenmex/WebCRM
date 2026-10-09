using Microsoft.Extensions.DependencyInjection;
using WebCRM.Core.Accounts;
using WebCRM.Core.Contacts;
using WebCRM.Core.Lookups;
using WebCRM.Core.Users;
using WebCRM.Data.Services;

namespace WebCRM.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Registers the services that read and write CRM data. Needs an IDbContextFactory&lt;CrmDbContext&gt;.</summary>
    public static IServiceCollection AddCrmServices(this IServiceCollection services)
    {
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IOwnerService, OwnerService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IContactService, ContactService>();
        return services;
    }
}
