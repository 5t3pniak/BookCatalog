namespace BookCatalog.Persistence.Models;

public sealed class Tag
{
    public long Id { get; set; }
    public TagCategory Category { get; set; }

    public string Slug { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string SortKey { get; set; } = ""; // exists on epoch detail and similar tag details :contentReference[oaicite:4]{index=4}

    public ICollection<BookTag> BookTags { get; set; } = new List<BookTag>();
}

public enum TagCategory
{
    Epoch = 1,
    Genre = 2,
    Kind = 3
}