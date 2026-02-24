namespace BookCatalog.Integrations.OpenBooks.Contract;

public record RemoteRef(
    string Slug,
    string Name,
    string? Href = null,
    string? Url  = null
);