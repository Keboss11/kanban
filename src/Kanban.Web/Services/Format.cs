namespace Kanban.Web.Services;

public static class Format
{
    /// <summary>"hace 5 min", "hace 3 h", "hace 2 días" o la fecha si es más antigua.</summary>
    public static string Relative(DateTime utc, DateTime? nowUtc = null)
    {
        var diff = (nowUtc ?? DateTime.UtcNow) - utc.ToUniversalTime();
        return diff.TotalSeconds switch
        {
            < 60 => "ahora mismo",
            < 3600 => $"hace {(int)diff.TotalMinutes} min",
            < 86400 => $"hace {(int)diff.TotalHours} h",
            < 7 * 86400 => (int)diff.TotalDays == 1 ? "ayer" : $"hace {(int)diff.TotalDays} días",
            _ => utc.ToLocalTime().ToString("d MMM yyyy"),
        };
    }
}
