namespace PandaHunt.Services.Notifications;

/// <summary>What <c>notifications.js</c> reports about the player's browser (Platform: ios, android, windows, mac, linux, other).</summary>
public sealed record PushInfo(
    string Platform, string Browser, bool Standalone, bool InApp, string Label, bool Secure, bool Supported,
    string Permission, string? Endpoint);
