namespace BookCatalog.Integrations.OpenBooks;

public class OpenBooksOptions
{
    public const string SectionName = "OpenBooks";

    public string BaseUrl { get; set; } = default!;
    public int TimeoutSeconds { get; set; } = 30;
}
