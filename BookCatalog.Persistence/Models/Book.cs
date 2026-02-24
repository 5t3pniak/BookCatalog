namespace BookCatalog.Persistence.Models;

public sealed class Book
{
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? ThumbnailUrl { get; set; }
    public string PrimaryAuthorSortKey { get; set; } = "";
    public DateTimeOffset LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<BookTag> BookTags { get; set; } = new List<BookTag>();
}


