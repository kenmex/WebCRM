using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using WebCRM.Core.Entities;
using WebCRM.Data.Conventions;

namespace WebCRM.Data;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : IdentityDbContext<User>(options)
{
    // Accounts and contacts
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Contact> Contacts => Set<Contact>();

    // Sales pipeline
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<Stage> Stages => Set<Stage>();

    // Activities, notes and attachments
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    // Teams and lookups
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Industry> Industries => Set<Industry>();
    public DbSet<LeadSource> LeadSources => Set<LeadSource>();
    public DbSet<LeadStatus> LeadStatuses => Set<LeadStatus>();
    public DbSet<ActivityType> ActivityTypes => Set<ActivityType>();
    public DbSet<AccountStatus> AccountStatuses => Set<AccountStatus>();
    public DbSet<LostReason> LostReasons => Set<LostReason>();

    // System tables
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<Favourite> Favourites => Set<Favourite>();
    public DbSet<RecentView> RecentViews => Set<RecentView>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<CompanySetting> CompanySettings => Set<CompanySetting>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Times are UTC to the second (data dictionary); date-only fields use DateOnly, which maps to date.
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(0)");

        // FKs on CreatedBy/UpdatedBy, but no indexes on them.
        configurationBuilder.Conventions.Replace<ForeignKeyIndexConvention>(services =>
            new NoAuditColumnIndexesConvention(services.GetRequiredService<ProviderConventionSetBuilderDependencies>()));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity tables first, then our configurations can extend them.
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
