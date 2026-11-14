using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PandaHunt.Account;
using PandaHunt.Data;

namespace PandaHunt.Services;

public sealed record UserRow(
    string Id,
    string UserName,
    string? Email,
    DateTime RegisteredAtUtc,
    bool IsAdmin,
    bool IsConfiguredAdmin,
    int MessageCount);

public sealed record UserEdit(string Id, string UserName, string Email, string? NewPassword, bool IsAdmin);

/// <summary>
/// Player management for the admin page. Each call uses its own scope, so a long-lived admin page
/// never works with stale Identity/EF state. Methods return Czech error messages; empty means success.
/// </summary>
public sealed class UserAdministration(IServiceScopeFactory scopes, AdminBootstrap bootstrap, MessageBoard board, Scoreboard scoreboard)
{
    public async Task<IReadOnlyList<UserRow>> ListAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminRoleId = await db.Roles
            .Where(r => r.Name == AppRoles.Admin)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        var users = await db.Users
            .OrderBy(u => u.RegisteredAtUtc)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.RegisteredAtUtc,
                IsAdmin = db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId),
                MessageCount = db.Messages.Count(m => m.AuthorId == u.Id),
            })
            .ToListAsync();

        return users
            .Select(u => new UserRow(u.Id, u.UserName ?? "", u.Email, u.RegisteredAtUtc, u.IsAdmin,
                bootstrap.IsConfiguredAdmin(u.Email), u.MessageCount))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> UpdateAsync(UserEdit edit, string actingUserId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var errors = new List<string>();

        var user = await users.FindByIdAsync(edit.Id);
        if (user is null)
            return ["Tenhle hráč už neexistuje."];

        var userName = edit.UserName.Trim();
        var email = edit.Email.Trim();

        if (userName.Length is < AccountRules.MinUserNameLength or > AccountRules.MaxUserNameLength)
            errors.Add($"Přezdívka musí mít {AccountRules.MinUserNameLength} až {AccountRules.MaxUserNameLength} znaků.");
        if (!string.IsNullOrEmpty(edit.NewPassword) && edit.NewPassword.Length < AccountRules.MinPasswordLength)
            errors.Add($"Heslo musí mít aspoň {AccountRules.MinPasswordLength} znaků.");
        if (!edit.IsAdmin && await users.IsInRoleAsync(user, AppRoles.Admin))
        {
            if (user.Id == actingUserId)
                errors.Add("Sám sobě admina odebrat nemůžeš.");
            else if (bootstrap.IsConfiguredAdmin(user.Email))
                errors.Add("Tenhle hráč je admin z konfigurace (Admin:Emails), tady mu admina odebrat nejde.");
        }
        if (errors.Count > 0)
            return errors;

        var renamed = user.UserName != userName;
        if (renamed)
            Collect(errors, await users.SetUserNameAsync(user, userName));
        if (errors.Count == 0 && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            Collect(errors, await users.SetEmailAsync(user, email));
        if (errors.Count == 0 && !string.IsNullOrEmpty(edit.NewPassword))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            Collect(errors, await users.ResetPasswordAsync(user, token, edit.NewPassword));
        }
        if (errors.Count == 0)
        {
            var isAdmin = await users.IsInRoleAsync(user, AppRoles.Admin);
            if (edit.IsAdmin && !isAdmin)
            {
                Collect(errors, await users.AddToRoleAsync(user, AppRoles.Admin));
            }
            else if (!edit.IsAdmin && isAdmin)
            {
                Collect(errors, await users.RemoveFromRoleAsync(user, AppRoles.Admin));
                // Kick them out of open admin pages right away instead of waiting for their next login.
                Collect(errors, await users.UpdateSecurityStampAsync(user));
            }
        }

        if (renamed)
            board.NotifyChanged();
        scoreboard.NotifyChanged(); // name or admin role (admins are not on the leaderboard) may have changed
        return errors;
    }

    public async Task<IReadOnlyList<string>> DeleteAsync(string userId, string actingUserId)
    {
        if (userId == actingUserId)
            return ["Sám sebe smazat nemůžeš."];

        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return [];
        if (bootstrap.IsConfiguredAdmin(user.Email))
            return ["Tenhle hráč je admin z konfigurace (Admin:Emails), smazat ho tady nejde."];

        var errors = new List<string>();
        Collect(errors, await users.DeleteAsync(user)); // their messages and points are deleted too (cascade)
        board.NotifyChanged();
        scoreboard.NotifyChanged();
        return errors;
    }

    private static void Collect(List<string> errors, IdentityResult result) =>
        errors.AddRange(result.Errors.Select(e => e.Description));
}
