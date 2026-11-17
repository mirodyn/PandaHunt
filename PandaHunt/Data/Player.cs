namespace PandaHunt.Data;

/// <summary>
/// Who actually plays: points, stars, completions and claims all belong to a player. Normally a player is one
/// login (<see cref="ApplicationUser"/>); an admin can join several logins into one team, which then share a
/// single player. <see cref="Name"/> is what everybody sees: the nickname for a single login, the team name
/// for a team.
/// </summary>
public class Player
{
    public const int MaxNameLength = 40;

    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>More than one login shares this player; <see cref="Name"/> is then the team name.</summary>
    public bool IsTeam { get; set; }

    public List<ApplicationUser> Members { get; set; } = [];
}
