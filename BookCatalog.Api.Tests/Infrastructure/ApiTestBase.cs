using System.Net.Http.Json;
using System.Text.Json;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace BookCatalog.Api.Tests.Infrastructure;

public sealed record PagedResponse<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record AuthorResponse(string Slug, string Name);

public sealed record BookResponse(
    string Slug,
    string Title,
    string Url,
    string? ThumbnailUrl,
    List<AuthorResponse> Authors);

public abstract class ApiTestBase
{
    private MsSqlContainer _container = null!;
    protected BookCatalogApiFactory Factory = null!;
    protected HttpClient Client = null!;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [OneTimeSetUp]
    public async Task BaseOneTimeSetUp()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();

        var connStr = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "BookCatalogTests"
        }.ConnectionString;

        Factory = new BookCatalogApiFactory(connStr);
        Client = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task BaseOneTimeTearDown()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    [TearDown]
    public async Task BaseTearDown()
    {
        await using var ctx = Factory.OpenDbContext();
        ctx.ChangeTracker.Clear();
        await ctx.BookTags.ExecuteDeleteAsync();
        await ctx.BookAuthors.ExecuteDeleteAsync();
        await ctx.Books.ExecuteDeleteAsync();
        await ctx.Authors.ExecuteDeleteAsync();
        await ctx.Tags.ExecuteDeleteAsync();
    }

    protected static Author MakeAuthor(
        string slug, string name, string? sortKey = null, bool isDeleted = false) => new()
    {
        Slug = slug,
        Name = name,
        SortKey = sortKey ?? name.ToLowerInvariant(),
        LastSyncedAt = DateTimeOffset.UtcNow,
        IsDeleted = isDeleted,
        DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
    };

    protected static Book MakeBook(
        string slug, string title,
        string primaryAuthorSortKey = "",
        bool isDeleted = false) => new()
    {
        Slug = slug,
        Title = title,
        Url = $"https://example.com/{slug}",
        PrimaryAuthorSortKey = primaryAuthorSortKey,
        LastSyncedAt = DateTimeOffset.UtcNow,
        IsDeleted = isDeleted,
        DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
    };

    protected async Task<(System.Net.HttpStatusCode Status, T? Body)> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, default);

        var body = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (response.StatusCode, body);
    }
}
