using BookCatalog.ApplicationCore.Helpers;

namespace BookCatalog.ApplicationCore.QueryHandlers;

public record AuthorsQuery(
    Paging Paging,
    SortDescriptor Sort)
{
    private static readonly IReadOnlySet<string> AllowedSortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name" };

    public static readonly SortDescriptor DefaultSort = new("name", SortDirection.Asc);

    public static AuthorsQuery Create(Paging paging, string? sortParam)
    {
        var sortDescriptor = SortDescriptor.Parse(sortParam, AllowedSortFields);
        return new(paging, sortDescriptor ?? DefaultSort);
    } 
    public static AuthorsQuery WithDefaults(Paging paging) => new(paging, DefaultSort);
}