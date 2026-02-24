using System.Text.Json.Serialization;

namespace BookCatalog.Integrations.OpenBooks.Contract;

public sealed record RemoteBookListItem(
    string Slug, 
    string Href,
    [property: JsonPropertyName("full_sort_key")]
    string FullSortKey,
    string Author);