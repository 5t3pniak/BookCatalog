namespace BookCatalog.ApplicationCore.QueryHandlers.Models;

public sealed record BookSummary(
    string Slug,
    string Title,
    string Url,
    string? ThumbnailUrl,
    IReadOnlyList<AuthorSummary> Authors
    );