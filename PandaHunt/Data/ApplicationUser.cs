using Microsoft.AspNetCore.Identity;

namespace PandaHunt.Data;

/// <summary>A registered login. <see cref="IdentityUser.UserName"/> is the public nickname.</summary>
public class ApplicationUser : IdentityUser
{
    public DateTime RegisteredAtUtc { get; set; }

    /// <summary>The player this login plays as (shared with teammates). Every login has one.</summary>
    public string PlayerId { get; set; } = "";

    public Player? Player { get; set; }
}
