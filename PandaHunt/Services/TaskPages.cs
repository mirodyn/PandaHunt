using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace PandaHunt.Services;

/// <summary>
/// The task pages that exist in the app: every routable component in Components/Pages/Tasks.
/// The admin page offers these addresses when linking a task to its page.
/// </summary>
public static class TaskPages
{
    private const string Namespace = "PandaHunt.Components.Pages.Tasks";

    public static IReadOnlyList<string> Routes { get; } = typeof(TaskPages).Assembly.GetTypes()
        .Where(t => t.Namespace == Namespace && typeof(IComponent).IsAssignableFrom(t))
        .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
        .Select(r => r.Template)
        .Where(template => !template.Contains('{'))
        .OrderBy(template => template, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static bool IsExternal(string link) =>
        link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || link.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trims, adds the leading "/" to local paths and drops a trailing one. Empty → null.</summary>
    public static string? Normalize(string? link)
    {
        link = link?.Trim();
        if (string.IsNullOrEmpty(link))
            return null;
        if (IsExternal(link))
            return link;
        link = "/" + link.Trim('/');
        return link;
    }

    public static bool SameLink(string? a, string? b) =>
        a is not null && b is not null && string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

    public static bool Exists(string? link) => Routes.Any(route => SameLink(route, link));
}
