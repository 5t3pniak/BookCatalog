namespace BookCatalog.Persistence.Models;

public sealed class Author
{
    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? SortKey { get; set; } = null; 
    
    public DateTimeOffset LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}