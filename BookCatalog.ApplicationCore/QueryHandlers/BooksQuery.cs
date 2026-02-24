using BookCatalog.ApplicationCore.Helpers;

namespace BookCatalog.ApplicationCore.QueryHandlers;

public sealed record BooksQuery(
    Paging Paging,
    SortDescriptor? Sort,
    string Author,
    string Epoch,
    string Kind,
    string Genre
)
{
    public static readonly IReadOnlySet<string> AllowedSortFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "title", "author" };

    public static readonly SortDescriptor DefaultSort = new("title", SortDirection.Asc);

    public static BooksQuery Create(
        Paging paging,
        string? sortParam,
        string author,
        string epoch,
        string kind,
        string genre)
    {
        var sortDescriptor = SortDescriptor.Parse(sortParam, AllowedSortFields);
        return new(paging, sortDescriptor ?? DefaultSort, author, epoch, kind, genre);
    }

    public static BooksQuery WithDefaults(Paging paging) =>
        new(paging, DefaultSort, string.Empty, string.Empty, string.Empty, string.Empty);
}