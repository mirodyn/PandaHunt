using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PandaHunt.Account;
using PandaHunt.Components;
using PandaHunt.Data;
using PandaHunt.Services;

var czech = new CultureInfo("cs-CZ");
CultureInfo.DefaultThreadCurrentCulture = czech;
CultureInfo.DefaultThreadCurrentUICulture = czech;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Database (SQLite file; the path comes from ConnectionStrings:Default)
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing.");
EnsureSqliteDirectoryExists(connectionString, builder.Environment.ContentRootPath);
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(connectionString));

// Keys that sign the login and phase-0 cookies. In a container they must sit on the persistent volume
// (DataProtection:KeysPath), otherwise every deploy logs everyone out. Locally the default per-user store is fine.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("PandaHunt")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// Accounts
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.AddAuthorization();
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.AllowedUserNameCharacters = string.Empty; // any characters, incl. diacritics and spaces
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = AccountRules.MinPasswordLength;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders() // lets the admin page reset passwords
    .AddErrorDescriber<CzechIdentityErrorDescriber>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PandaHunt.Auth";
    options.LoginPath = AccountEndpoints.EntryPath; // decides between the phase-0 code page and login
    options.AccessDeniedPath = "/";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});
// Re-read the user (roles, nickname, deleted?) from the DB at most once a minute, so admin changes apply quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddSingleton<AdminBootstrap>();
builder.Services.AddSingleton<UserAdministration>();

// Game
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GameSettingsStore>();
builder.Services.AddSingleton<GameSchedule>();
builder.Services.AddSingleton<Phase0Gate>();
builder.Services.AddSingleton<MessageBoard>();
builder.Services.AddSingleton<Scoreboard>();
builder.Services.AddSingleton<HuntTasks>();
builder.Services.AddSingleton<RegistrationBonuses>();
builder.Services.AddSingleton<PandaFeeding>();
builder.Services.AddSingleton<PandaPestering>();
builder.Services.AddSingleton<BonusEvents>();

var app = builder.Build();

// Create / upgrade the database, load game settings and set up the admin role.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await app.Services.GetRequiredService<GameSettingsStore>().LoadAsync();
    await app.Services.GetRequiredService<AdminBootstrap>().InitializeAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRequestLocalization(czech.Name);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAccountEndpoints();

app.Run();

static void EnsureSqliteDirectoryExists(string connectionString, string contentRoot)
{
    var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
    if (string.IsNullOrEmpty(dataSource) || dataSource == ":memory:")
        return;

    var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource, contentRoot));
    if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
}
