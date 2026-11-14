using Microsoft.AspNetCore.Identity;

namespace PandaHunt.Services;

/// <summary>Czech versions of the Identity errors players can actually run into.</summary>
public sealed class CzechIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Něco se pokazilo. Zkus to prosím znovu.");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Někdo mezitím změnil stejná data. Zkus to prosím znovu.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), $"Přezdívka „{userName}“ už je obsazená.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), $"E-mail {email} už používá jiný lovec.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "Přezdívka obsahuje nepovolené znaky.");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), $"E-mail {email} není platný.");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Nesprávné heslo.");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"Heslo musí mít aspoň {length} znaků.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), $"Heslo musí obsahovat aspoň {uniqueChars} různých znaků.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Heslo musí obsahovat aspoň jeden speciální znak.");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Heslo musí obsahovat aspoň jednu číslici.");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Heslo musí obsahovat aspoň jedno malé písmeno.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Heslo musí obsahovat aspoň jedno velké písmeno.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
