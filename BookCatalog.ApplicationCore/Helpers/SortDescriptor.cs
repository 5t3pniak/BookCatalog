namespace BookCatalog.ApplicationCore.Helpers;

public enum SortDirection { Asc, Desc }

public sealed record SortDescriptor(string Field, SortDirection Direction)
{
    public static SortDescriptor? Parse(
        string? raw,
        IReadOnlySet<string> allowedFields)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var parts = raw.Split(':', 2);
        if(!allowedFields.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
            return null;

        return new SortDescriptor(
            parts[0].ToLowerInvariant(),
            parts.Length == 2 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)
                ? SortDirection.Desc
                : SortDirection.Asc);
    }
}