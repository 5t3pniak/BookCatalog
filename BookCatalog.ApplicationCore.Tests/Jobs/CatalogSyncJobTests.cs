using BookCatalog.ApplicationCore.Jobs;
using BookCatalog.Integrations.OpenBooks.Contract;
using BookCatalog.Integrations.OpenBooks.HttpClient;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Testcontainers.MsSql;

namespace BookCatalog.ApplicationCore.Tests.Jobs;

public class CatalogSyncJobTests
{
    private MsSqlContainer _sqlContainer = null!;
    private DbContextOptions<BookCatalogDbContext> _dbOptions = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlContainer.StartAsync();

        var connStr = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
        {
            InitialCatalog = "BookCatalogTests"
        }.ConnectionString;

        _dbOptions = new DbContextOptionsBuilder<BookCatalogDbContext>()
            .UseSqlServer(connStr)
            .Options;

        await using var ctx = new BookCatalogDbContext(_dbOptions);
        await ctx.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await using var ctx = new BookCatalogDbContext(_dbOptions);
        await ctx.Database.EnsureDeletedAsync();
        await _sqlContainer.DisposeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var ctx = new BookCatalogDbContext(_dbOptions);
        await ctx.BookTags.ExecuteDeleteAsync();
        await ctx.BookAuthors.ExecuteDeleteAsync();
        await ctx.Books.ExecuteDeleteAsync();
        await ctx.Authors.ExecuteDeleteAsync();
        await ctx.Tags.ExecuteDeleteAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<BookCatalogDbContext> options)
        : IDbContextFactory<BookCatalogDbContext>
    {
        public BookCatalogDbContext CreateDbContext() => new(options);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private CatalogSync BuildSync(IOpenBooksClient api, DateTimeOffset? clockTime = null) =>
        new(api,
            new TestDbContextFactory(_dbOptions),
            new FakeClock(clockTime ?? DateTimeOffset.UtcNow),
            NullLogger<CatalogSync>.Instance);
    
    private static IOpenBooksClient MockApi(
        IReadOnlyList<RemoteBookListItem> list,
        params (string slug, RemoteBookDetail? detail)[] details)
    {
        var api = Substitute.For<IOpenBooksClient>();
        api.GetBooksAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(list));
        foreach (var (slug, detail) in details)
            api.GetBookDetailAsync(slug, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(detail));
        return api;
    }

    private static RemoteBookListItem ListItem(
        string slug, string author, string fullSortKey = "sort-key")
        => new(slug, $"/{slug}/", fullSortKey, author);

    private static RemoteBookDetail Detail(
        string slug,
        string title = "Book Title",
        string? url = null,
        IEnumerable<RemoteRef>? authors = null,
        IEnumerable<RemoteRef>? epochs = null,
        IEnumerable<RemoteRef>? genres = null,
        IEnumerable<RemoteRef>? kinds = null,
        string? simpleThumb = null,
        string? coverThumb = null) => new()
    {
        Slug = slug,
        Title = title,
        Url = url ?? $"https://example.com/{slug}",
        Authors = [.. (authors ?? [])],
        Epochs  = [.. (epochs  ?? [])],
        Genres  = [.. (genres  ?? [])],
        Kinds   = [.. (kinds   ?? [])],
        SimpleThumb = simpleThumb,
        CoverThumb  = coverThumb
    };

    private static RemoteRef Ref(string slug, string name) => new(slug, name);

    private BookCatalogDbContext OpenCtx() => new(_dbOptions);

    [Test]
    public async Task Rebuild_PersistsBook()
    {
        var api = MockApi(
            [ListItem("crime-punishment", "Fyodor Dostoevsky", "dostoevsky-fyodor")],
            ("crime-punishment", Detail("crime-punishment", "Crime and Punishment",
                url: "https://example.com/crime-punishment")));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var book = await ctx.Books.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(book.Slug, Is.EqualTo("crime-punishment"));
            Assert.That(book.Title, Is.EqualTo("Crime and Punishment"));
            Assert.That(book.Url, Is.EqualTo("https://example.com/crime-punishment"));
            Assert.That(book.IsDeleted, Is.False);
        });
    }

    [Test]
    public async Task Rebuild_SetsPrimaryAuthorSortKeyFromListItem()
    {
        var api = MockApi(
            [ListItem("book-1", "Jane Austen", "austen-jane")],
            ("book-1", Detail("book-1", authors: [Ref("jane-austen", "Jane Austen")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var book = await ctx.Books.SingleAsync();
        Assert.That(book.PrimaryAuthorSortKey, Is.EqualTo("austen-jane"));
    }

    [Test]
    public async Task Rebuild_PersistsAuthor()
    {
        var api = MockApi(
            [ListItem("book-1", "Leo Tolstoy")],
            ("book-1", Detail("book-1", authors: [Ref("leo-tolstoy", "Leo Tolstoy")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var author = await ctx.Authors.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(author.Slug, Is.EqualTo("leo-tolstoy"));
            Assert.That(author.Name, Is.EqualTo("Leo Tolstoy"));
        });
    }

    [Test]
    public async Task Rebuild_SetsSortKey_ForPrimaryAuthor()
    {
        var api = MockApi(
            [ListItem("book-1", "Jane Austen", "austen-jane")],
            ("book-1", Detail("book-1", authors: [Ref("jane-austen", "Jane Austen")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var author = await ctx.Authors.SingleAsync();
        Assert.That(author.SortKey, Is.EqualTo("austen-jane"));
    }

    [Test]
    public async Task Rebuild_SetsSortKeyNull_ForCoAuthor()
    {
        // Co-author name does NOT match the list item's Author field → SortKey stays null
        var api = MockApi(
            [ListItem("book-1", "Jane Austen", "austen-jane")],
            ("book-1", Detail("book-1", authors:
            [
                Ref("jane-austen",  "Jane Austen"),   // primary
                Ref("charles-lamb", "Charles Lamb")   // co-author
            ])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var coAuthor = await ctx.Authors.SingleAsync(a => a.Slug == "charles-lamb");
        Assert.That(coAuthor.SortKey, Is.Null);
    }

    [Test]
    public async Task Rebuild_CreatesBookAuthorLink()
    {
        var api = MockApi(
            [ListItem("book-1", "Jane Austen")],
            ("book-1", Detail("book-1", authors: [Ref("jane-austen", "Jane Austen")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var link = await ctx.BookAuthors.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(link.BookSlug, Is.EqualTo("book-1"));
            Assert.That(link.AuthorSlug, Is.EqualTo("jane-austen"));
        });
    }

    [Test]
    public async Task Rebuild_PersistsEpochTag_AndLinksToBook()
    {
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1", epochs: [Ref("romanticism", "Romanticism")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var tag = await ctx.Tags.SingleAsync();
        var bookTag = await ctx.BookTags.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(tag.Slug, Is.EqualTo("romanticism"));
            Assert.That(tag.Category, Is.EqualTo(TagCategory.Epoch));
            Assert.That(bookTag.BookSlug, Is.EqualTo("book-1"));
        });
    }

    [Test]
    public async Task Rebuild_PersistsGenreTag_AndLinksToBook()
    {
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1", genres: [Ref("drama", "Drama")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var tag = await ctx.Tags.SingleAsync();
        Assert.That(tag.Category, Is.EqualTo(TagCategory.Genre));
    }

    [Test]
    public async Task Rebuild_PersistsKindTag_AndLinksToBook()
    {
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1", kinds: [Ref("novel", "Novel")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var tag = await ctx.Tags.SingleAsync();
        Assert.That(tag.Category, Is.EqualTo(TagCategory.Kind));
    }

    [Test]
    public async Task Rebuild_DeduplicatesAuthors_AcrossBooks()
    {
        var api = MockApi(
            [
                ListItem("book-a", "Jane Austen"),
                ListItem("book-b", "Jane Austen")
            ],
            ("book-a", Detail("book-a", authors: [Ref("jane-austen", "Jane Austen")])),
            ("book-b", Detail("book-b", authors: [Ref("jane-austen", "Jane Austen")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That(await ctx.Authors.CountAsync(), Is.EqualTo(1));
        Assert.That(await ctx.BookAuthors.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Rebuild_DeduplicatesTags_AcrossBooks()
    {
        var api = MockApi(
            [
                ListItem("book-a", "Author A"),
                ListItem("book-b", "Author B")
            ],
            ("book-a", Detail("book-a", epochs: [Ref("romanticism", "Romanticism")])),
            ("book-b", Detail("book-b", epochs: [Ref("romanticism", "Romanticism")])));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That(await ctx.Tags.CountAsync(), Is.EqualTo(1));
        Assert.That(await ctx.BookTags.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Rebuild_ClearsAllExistingData_BeforeRebuild()
    {
        await using (var seed = OpenCtx())
        {
            seed.Authors.Add(new Author { Slug = "old-author", Name = "Old", LastSyncedAt = DateTimeOffset.UtcNow });
            seed.Books.Add(new Book { Slug = "old-book", Title = "Old", Url = "https://old", LastSyncedAt = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync();
        }

        var api = MockApi(
            [ListItem("new-book", "New Author")],
            ("new-book", Detail("new-book", "New Book")));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That(await ctx.Authors.AnyAsync(a => a.Slug == "old-author"), Is.False);
        Assert.That(await ctx.Books.AnyAsync(b => b.Slug == "old-book"), Is.False);
        Assert.That(await ctx.Books.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Rebuild_SetsLastSyncedAt_FromClock()
    {
        var syncTime = new DateTimeOffset(2025, 6, 15, 3, 0, 0, TimeSpan.Zero);
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1")));

        await BuildSync(api, syncTime).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var book = await ctx.Books.SingleAsync();
        Assert.That(book.LastSyncedAt, Is.EqualTo(syncTime));
    }

    [Test]
    public async Task Rebuild_SetsAuthorLastSyncedAt_FromClock()
    {
        var syncTime = new DateTimeOffset(2025, 6, 15, 3, 0, 0, TimeSpan.Zero);
        var api = MockApi(
            [ListItem("book-1", "Jane Austen")],
            ("book-1", Detail("book-1", authors: [Ref("jane-austen", "Jane Austen")])));

        await BuildSync(api, syncTime).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        var author = await ctx.Authors.SingleAsync();
        Assert.That(author.LastSyncedAt, Is.EqualTo(syncTime));
    }

    [Test]
    public async Task Rebuild_SkipsBook_WhenDetailReturnsNull()
    {
        var api = MockApi(
            [ListItem("missing-book", "Author")],
            ("missing-book", (RemoteBookDetail?)null));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That(await ctx.Books.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Rebuild_UsesSimpleThumb_WhenAvailable()
    {
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1", simpleThumb: "https://cdn/simple.jpg", coverThumb: "https://cdn/cover.jpg")));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That((await ctx.Books.SingleAsync()).ThumbnailUrl, Is.EqualTo("https://cdn/simple.jpg"));
    }

    [Test]
    public async Task Rebuild_FallsBackToCoverThumb_WhenSimpleThumbIsAbsent()
    {
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1", simpleThumb: null, coverThumb: "https://cdn/cover.jpg")));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That((await ctx.Books.SingleAsync()).ThumbnailUrl, Is.EqualTo("https://cdn/cover.jpg"));
    }

    [Test]
    public async Task Rebuild_ThumbnailIsNull_WhenBothThumbsAbsent()
    {
        var api = MockApi(
            [ListItem("book-1", "Author")],
            ("book-1", Detail("book-1", simpleThumb: null, coverThumb: null)));

        await BuildSync(api).RebuildFromBookDetailsAsync();

        await using var ctx = OpenCtx();
        Assert.That((await ctx.Books.SingleAsync()).ThumbnailUrl, Is.Null);
    }
}
