using Microsoft.Data.Sqlite;

namespace PandaHunt.Services;

/// <summary>
/// Hard reset of the whole app (admin page, tab "Reset").
/// Asking for it only writes a marker file next to the database and shuts the app down. The wipe itself runs on the
/// next start, before anything opens the database or the login keys: the database files and the key files are
/// deleted, so the app comes up as on its very first deploy - an empty database (the migrations re-create the
/// default settings and the seeded tasks), everyone logged out, entered phase-0 codes forgotten.
/// On Fly.io the app is started again by the machine ([[restart]] policy "always" in fly.toml); locally start it again.
/// </summary>
public sealed class AppReset(IConfiguration configuration, IWebHostEnvironment environment,
    IHostApplicationLifetime lifetime, ILogger<AppReset> logger)
{
    public const string ConfirmWord = "SMAZAT";

    private const string MarkerFileName = "reset-requested.txt";
    private static readonly string[] DatabaseFileSuffixes = ["", "-wal", "-shm", "-journal"];

    private int requested;

    /// <summary>A reset was asked for and the app is shutting down.</summary>
    public bool IsRequested => Volatile.Read(ref requested) == 1;

    /// <summary>Full path of the SQLite file, or null for an in-memory database (nothing to reset).</summary>
    public string? DatabaseFile => DatabaseFileOf(configuration.GetConnectionString("Default") ?? "", environment.ContentRootPath);

    /// <summary>
    /// The app's own folder with login keys (DataProtection:KeysPath, set on Fly.io). Null = the default per-user
    /// store, which other local apps share too, so a reset leaves it alone.
    /// </summary>
    public string? KeysFolder => KeysFolderOf(configuration, environment.ContentRootPath);

    /// <summary>Fly.io starts the app again after it stops; anywhere else it has to be started by hand.</summary>
    public static bool RestartsByItself => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLY_APP_NAME"));

    public static bool IsConfirmation(string? text) =>
        string.Equals(text?.Trim(), ConfirmWord, StringComparison.OrdinalIgnoreCase);

    /// <summary>Marks the app for a reset and shuts it down. False when the confirmation word is wrong.</summary>
    public bool Request(string? confirmation, string requestedBy)
    {
        if (!IsConfirmation(confirmation) || DatabaseFile is not { } database)
            return false;
        if (Interlocked.Exchange(ref requested, 1) == 1)
            return true; // another admin was quicker

        try
        {
            File.WriteAllText(MarkerFileOf(database), $"{DateTimeOffset.UtcNow:O} {requestedBy}{Environment.NewLine}");
        }
        catch
        {
            Volatile.Write(ref requested, 0);
            throw;
        }

        logger.LogWarning("Hard reset requested by {Admin}. Shutting down; the data is wiped on the next start.", requestedBy);

        // Give the admin's page a moment to show what is going on, then stop.
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            lifetime.StopApplication();
        });
        return true;
    }

    /// <summary>
    /// Runs at startup before the database is opened (Program.cs): carries out a reset asked for before the restart.
    /// The marker goes last, so if anything fails the next start tries again.
    /// </summary>
    public static void ApplyPending(IConfiguration configuration, string contentRoot, ILogger logger)
    {
        if (DatabaseFileOf(configuration.GetConnectionString("Default") ?? "", contentRoot) is not { } database)
            return;
        var marker = MarkerFileOf(database);
        if (!File.Exists(marker))
            return;

        var requestedBy = File.ReadAllText(marker).Trim();
        foreach (var suffix in DatabaseFileSuffixes)
            File.Delete(database + suffix); // no-op when the file is not there

        var deletedKeys = 0;
        if (KeysFolderOf(configuration, contentRoot) is { } keys && Directory.Exists(keys))
        {
            foreach (var file in Directory.EnumerateFiles(keys, "*.xml"))
            {
                File.Delete(file);
                deletedKeys++;
            }
        }

        File.Delete(marker);
        logger.LogWarning("Hard reset ({RequestedBy}): deleted the database {Database} and {KeyCount} login key file(s).",
            requestedBy, database, deletedKeys);
    }

    public static string? DatabaseFileOf(string connectionString, string contentRoot)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        return string.IsNullOrEmpty(dataSource) || dataSource == ":memory:"
            ? null
            : Path.GetFullPath(dataSource, contentRoot);
    }

    private static string? KeysFolderOf(IConfiguration configuration, string contentRoot) =>
        configuration["DataProtection:KeysPath"] is { } path && !string.IsNullOrWhiteSpace(path)
            ? Path.GetFullPath(path, contentRoot)
            : null;

    private static string MarkerFileOf(string database) =>
        Path.Combine(Path.GetDirectoryName(database)!, MarkerFileName);
}
