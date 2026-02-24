using BookCatalog.ApplicationCore.Jobs;
using BookCatalog.Integrations.OpenBooks.Contract;
using BookCatalog.Integrations.OpenBooks.HttpClient;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Testcontainers.MsSql;
using TickerQ.Utilities.Base;

namespace BookCatalog.ApplicationCore.Tests.Jobs;

public class RemainingSyncAuthorsDataSyncJobTests
{
    private MsSqlContainer _sqlContainer = null!;
    private BookCatalogDbContext _context = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _sqlContainer.StartAsync();

        var connStr = new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
        {
            InitialCatalog = "BookCatalogTests"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<BookCatalogDbContext>()
            .UseSqlServer(connStr)
            .Options;

        _context = new BookCatalogDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        await _sqlContainer.DisposeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        _context.ChangeTracker.Clear();
        await _context.Authors.ExecuteDeleteAsync();
    }

    private RemainingSyncAuthorsDataSyncJob BuildJob(IOpenBooksClient api) =>
        new(api, _context, NullLogger<RemainingSyncAuthorsDataSyncJob>.Instance);

    private static Author MakeAuthor(
        string slug,
        string name,
        string? sortKey = null,
        bool isDeleted = false) => new()
    {
        Slug = slug,
        Name = name,
        SortKey = sortKey,
        LastSyncedAt = DateTimeOffset.UtcNow,
        IsDeleted = isDeleted,
        DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
    };

    private async Task Seed(params Author[] authors)
    {
        _context.Authors.AddRange(authors);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }
    
    [Test]
    public async Task AuthorsSync_WhenNoAuthorsNeedUpdate_DoesNotCallApi()
    {
        var api = Substitute.For<IOpenBooksClient>();

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        await api.DidNotReceive().GetAuthorDetailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AuthorsSync_SkipsDeletedAuthors()
    {
        await Seed(MakeAuthor("deleted-author", "Deleted", sortKey: null, isDeleted: true));

        var api = Substitute.For<IOpenBooksClient>();

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        await api.DidNotReceive().GetAuthorDetailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AuthorsSync_SkipsAuthors_WhenSortKeyAlreadySet()
    {
        await Seed(MakeAuthor("austen", "Jane Austen", sortKey: "austen-jane"));

        var api = Substitute.For<IOpenBooksClient>();

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        await api.DidNotReceive().GetAuthorDetailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AuthorsSync_UpdatesSortKey_WhenApiReturnsDetail()
    {
        await Seed(MakeAuthor("dostoevsky", "Fyodor Dostoevsky", sortKey: null));

        var api = Substitute.For<IOpenBooksClient>();
        api.GetAuthorDetailAsync("dostoevsky", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RemoteAuthorDetail?>(new RemoteAuthorDetail { SortKey = "dostoevsky-fyodor" }));

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        var updated = await _context.Authors.AsNoTracking().SingleAsync();
        Assert.That(updated.SortKey, Is.EqualTo("dostoevsky-fyodor"));
    }

    [Test]
    public async Task AuthorsSync_DoesNotUpdateSortKey_WhenApiReturnsNull()
    {
        await Seed(MakeAuthor("unknown-author", "Unknown", sortKey: null));

        var api = Substitute.For<IOpenBooksClient>();
        api.GetAuthorDetailAsync("unknown-author", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RemoteAuthorDetail?>(null));

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        var author = await _context.Authors.AsNoTracking().SingleAsync();
        Assert.That(author.SortKey, Is.Null);
    }

    [Test]
    public async Task AuthorsSync_UpdatesMultipleAuthors()
    {
        await Seed(
            MakeAuthor("austen",     "Jane Austen",     sortKey: null),
            MakeAuthor("tolstoy",    "Leo Tolstoy",     sortKey: null),
            MakeAuthor("dostoevsky", "Fyodor Dostoevsky", sortKey: null));

        var api = Substitute.For<IOpenBooksClient>();
        api.GetAuthorDetailAsync("austen",     Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RemoteAuthorDetail?>(new RemoteAuthorDetail { SortKey = "austen-jane" }));
        api.GetAuthorDetailAsync("tolstoy",    Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RemoteAuthorDetail?>(new RemoteAuthorDetail { SortKey = "tolstoy-leo" }));
        api.GetAuthorDetailAsync("dostoevsky", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RemoteAuthorDetail?>(new RemoteAuthorDetail { SortKey = "dostoevsky-fyodor" }));

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        var authors = await _context.Authors.AsNoTracking()
            .OrderBy(a => a.Slug)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(authors.Single(a => a.Slug == "austen").SortKey,     Is.EqualTo("austen-jane"));
            Assert.That(authors.Single(a => a.Slug == "tolstoy").SortKey,    Is.EqualTo("tolstoy-leo"));
            Assert.That(authors.Single(a => a.Slug == "dostoevsky").SortKey, Is.EqualTo("dostoevsky-fyodor"));
        });
    }

    [Test]
    public async Task AuthorsSync_OnlyProcessesAuthors_WithNullSortKey()
    {
        await Seed(
            MakeAuthor("austen",  "Jane Austen",  sortKey: "austen-jane"),   // already set
            MakeAuthor("tolstoy", "Leo Tolstoy",  sortKey: null));            // needs update

        var api = Substitute.For<IOpenBooksClient>();
        api.GetAuthorDetailAsync("tolstoy", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<RemoteAuthorDetail?>(new RemoteAuthorDetail { SortKey = "tolstoy-leo" }));

        await BuildJob(api).AuthorsSync(default!, CancellationToken.None);

        await api.DidNotReceive().GetAuthorDetailAsync("austen", Arg.Any<CancellationToken>());
        await api.Received(1).GetAuthorDetailAsync("tolstoy", Arg.Any<CancellationToken>());

        var tolstoy = await _context.Authors.AsNoTracking().SingleAsync(a => a.Slug == "tolstoy");
        Assert.That(tolstoy.SortKey, Is.EqualTo("tolstoy-leo"));
    }
}
