using Microsoft.AspNetCore.Identity;

namespace PandaHunt.Data;

/// <summary>A registered hunter. <see cref="IdentityUser.UserName"/> is the public nickname.</summary>
public class ApplicationUser : IdentityUser
{
    public DateTime RegisteredAtUtc { get; set; }
}
