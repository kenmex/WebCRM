using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using WebCRM.Core.Entities;
using WebCRM.Core.Interfaces;
using WebCRM.Core.Users;
using WebCRM.Data;
using WebCRM.Data.Interceptors;
using WebCRM.Data.Seeding;
using WebCRM.Web.Components;
using WebCRM.Web.Components.Account;
using WebCRM.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Authentication state for components: cascaded to every component, and re-checked
// against the database every 30 minutes for interactive circuits.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Connection string lives in user-secrets (dev), never in appsettings.json.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. Set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"");

// Who is saving and when: used by the interceptor that fills CreatedBy/UpdatedBy.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<AuditFieldsInterceptor>();

// Factory instead of a scoped DbContext: in Interactive Server a scope lasts for the whole
// circuit (browser tab), so components create a short-lived context per operation.
// The factory itself is scoped (one per circuit or request) so each context gets the
// interceptor for the right user. AddDbContextFactory also registers a scoped
// CrmDbContext, which the Identity stores use.
builder.Services.AddDbContextFactory<CrmDbContext>(
    (services, options) => options
        .UseSqlServer(connectionString)
        .AddInterceptors(services.GetRequiredService<AuditFieldsInterceptor>()),
    ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Data services (accounts, lookups, owners) and the signed-in user's role and team.
builder.Services.AddCrmServices();
builder.Services.AddScoped<IUserContextProvider, UserContextProvider>();

builder.Services.AddIdentityCore<User>(options =>
    {
        // No SMTP until Phase 6, so accounts cannot be confirmed by email yet.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

        options.Password.RequiredLength = 12;

        // P1: lock out for 15 minutes after 5 failed attempts.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<CrmDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Idempotent seed (system user, roles, CompanySetting, first Admin). Runs on every start;
// it needs the migrations to be applied already.
await DatabaseSeeder.SeedAsync(app.Services, connectionString);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Additional endpoints the Identity /Account components post to (logout, external login, passkeys).
app.MapAdditionalIdentityEndpoints();

app.Run();
