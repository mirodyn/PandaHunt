using Microsoft.AspNetCore.Identity;

namespace PandaHunt.Data;

/// <summary>A registered login. <see cref="IdentityUser.UserName"/> is the public nickname.</summary>
public class ApplicationUser : IdentityUser
{
    public DateTime RegisteredAtUtc { get; set; }

    /// <summary>The player this login plays as (shared with teammates). Every login has one.</summary>
    public string PlayerId { get; set; } = "";

    public Player? Player { get; set; }

    /// <summary>Send notifications to the login e-mail.</summary>
    public bool NotifyByEmail { get; set; }

    /// <summary>Send notifications by SMS to <see cref="IdentityUser.PhoneNumber"/>. Prepared, not built yet.</summary>
    public bool NotifyBySms { get; set; }

    /// <summary>Browsers allowed to show push notifications; having at least one means "browser notifications on".</summary>
    public List<WebPushDevice> PushDevices { get; set; } = [];
}
