using System.Text.Json.Serialization;

namespace BookCatalog.Integrations.OpenBooks.Contract;

public class RemoteAuthorDetail
{
    [JsonPropertyName("sort_key")]
    public string SortKey { get; init; } = default!;
}