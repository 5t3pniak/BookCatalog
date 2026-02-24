namespace BookCatalog.Persistence.Models;

public sealed class BookTag
{
    public string BookSlug { get; set; } = default!;
    public Book Book { get; set; } = default!;

    public long TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}