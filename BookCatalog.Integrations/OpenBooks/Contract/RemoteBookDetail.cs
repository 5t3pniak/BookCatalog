using System.Text.Json.Serialization;

namespace BookCatalog.Integrations.OpenBooks.Contract;

public class RemoteBookDetail
{
    public string Slug { get; set; } = default!;
    
    public string Title { get; init; } = default!;
    
    public string Url { get; init; } = default!;
    
    public List<RemoteRef> Epochs { get; init; } = new();

    public List<RemoteRef> Genres { get; init; } = new();
    
    public List<RemoteRef> Kinds { get; init; } = new();

    public List<RemoteRef> Authors { get; init; } = new();
    
    [JsonPropertyName("cover_thumb")]
    public string? CoverThumb { get; init; }
    
    [JsonPropertyName("simple_thumb")]
    public string? SimpleThumb { get; init; }
}