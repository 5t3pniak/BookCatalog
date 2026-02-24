using System.Runtime.CompilerServices;
using System.Text.Json;
using BookCatalog.Integrations.OpenBooks.Contract;

namespace BookCatalog.Integrations.OpenBooks.HttpClient;

public interface IOpenBooksClient
{
    Task<RemoteAuthorDetail?> GetAuthorDetailAsync(string slug, CancellationToken ct);
    Task<RemoteBookDetail?> GetBookDetailAsync(string slug, CancellationToken ct);

    Task<IReadOnlyList<RemoteBookListItem>> GetBooksAsync(CancellationToken ct);
}

public sealed class OpenBooksClient : IOpenBooksClient
{
    private readonly System.Net.Http.HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenBooksClient(System.Net.Http.HttpClient http)
    {
        _http = http;
    }

    public async Task<RemoteAuthorDetail?> GetAuthorDetailAsync(string slug, CancellationToken ct)
    {
        var path = $"authors/{Uri.EscapeDataString(slug)}/";

        using var resp = await _http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);

        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<RemoteAuthorDetail>(stream, JsonOptions, ct);
    }

    public async Task<RemoteBookDetail?> GetBookDetailAsync(string slug, CancellationToken ct)
    {
        var path = $"books/{Uri.EscapeDataString(slug)}/";

        using var resp = await _http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);

        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var detail = await JsonSerializer.DeserializeAsync<RemoteBookDetail>(stream, JsonOptions, ct);
        detail?.Slug = slug;
        return detail;
    }

    public async Task<IReadOnlyList<RemoteBookListItem>> GetBooksAsync(CancellationToken ct)
    {
        using var resp = await _http.GetAsync("books/", HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var list = await JsonSerializer.DeserializeAsync<List<RemoteBookListItem>>(stream, JsonOptions, ct);
        return list ?? [];
    }
}