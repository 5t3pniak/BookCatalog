namespace BookCatalog.Persistence.Models;

public sealed class BookAuthor
{
    public string BookSlug { get; set; } = default!;
    public Book Book { get; set; } = default!;

    public string AuthorSlug { get; set; } = default!;
    public Author Author { get; set; } = default!;
}