using Microsoft.AspNetCore.Identity;
using PandaHunt.Data;

namespace PandaHunt.Account;

/// <summary>
/// Solves "who is the first admin": accounts whose e-mail is listed in configuration "Admin:Emails"
/// always get the Admin role (at startup, registration and login). More admins can then be added on /admin.
/// The repo is public, so set the e-mails in user secrets / host app settings, not in appsettings.json.
/// </summary>
public sealed class AdminBootstrap(IConfiguration configuration)
{
    private readonly HashSet<string> emails = new(
        (configuration.GetSection("Admin:Emails").Get<string[]>() ?? []).Select(e => e.Trim()),
        StringComparer.OrdinalIgnoreCase);

    public bool IsConfiguredAdmin(string? email) => email is not null && emails.Contains(email.Trim());

    public async Task EnsureAdminRoleAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        if (IsConfiguredAdmin(user.Email) && !await users.IsInRoleAsync(user, AppRoles.Admin))
            await users.AddToRoleAsync(user, AppRoles.Admin);
    }

    /// <summary>Creates the Admin role and grants it to configured accounts that already exist.</summary>
    public async Task InitializeAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(AppRoles.Admin))
            await roles.CreateAsync(new IdentityRole(AppRoles.Admin));

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var email in emails)
        {
            if (await users.FindByEmailAsync(email) is { } user)
                await EnsureAdminRoleAsync(users, user);
        }
    }
}
