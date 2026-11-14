using Microsoft.AspNetCore.Identity;
using PandaHunt.Data;
using PandaHunt.Services;

namespace PandaHunt.Account;

public static class AccountEndpoints
{
    public const string EntryPath = "/vstup";
    public const string LogoutPath = "/ucet/odhlasit";
    public const string CodePath = "/kod";
    public const string LoginPath = "/prihlaseni";
    public const string RegisterPath = "/registrace";

    /// <summary>
    /// Login and registration are statically rendered pages (Components/Pages/Account) because they set cookies.
    /// These endpoints cover the remaining cookie work and the "where should this visitor go" decision.
    /// </summary>
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Every anonymous request to a protected page lands here (it is the cookie LoginPath).
        endpoints.MapGet(EntryPath, (HttpContext context, Phase0Gate gate, string? returnUrl) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
                return TypedResults.LocalRedirect(IsLocalUrl(returnUrl) ? returnUrl! : "/");

            if (!gate.HasPassed(context))
                return TypedResults.LocalRedirect(CodePath);

            return TypedResults.LocalRedirect(IsLocalUrl(returnUrl) && returnUrl != "/"
                ? $"{LoginPath}?returnUrl={Uri.EscapeDataString(returnUrl!)}"
                : LoginPath);
        });

        endpoints.MapPost(LogoutPath, async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect(LoginPath);
        });

        return endpoints;
    }

    public static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
}
