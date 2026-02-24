using BookCatalog.ApplicationCore.Helpers;
using BookCatalog.ApplicationCore.QueryHandlers;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace BookCatalog.ApplicationCore.Tests.Handlers;

public class AuthorHandlerTests
{
    private MsSqlContainer _sqlContainer = null!;
    private BookCatalogDbContext _context = null!;
    private AuthorsHandler _handler = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

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

    [SetUp]
    public void SetUp()
    {
        _handler = new AuthorsHandler(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        _context.Authors.RemoveRange(_context.Authors);
        await _context.SaveChangesAsync();
    }

    private static Author MakeAuthor(
        string slug,
        string name,
        string? sortKey = null,
        bool isDeleted = false) => new()
    {
        Slug = slug,
        Name = name,
        SortKey = sortKey ?? name.ToLowerInvariant(),
        LastSyncedAt = DateTimeOffset.UtcNow,
        IsDeleted = isDeleted,
        DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
    };

    private async Task Seed(params Author[] authors)
    {
        await _context.Authors.AddRangeAsync(authors);
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Handle_WhenNoAuthors_ReturnsEmptyPagedResult()
    {
        var query = AuthorsQuery.WithDefaults(new Paging(1, 10));

        var result = await _handler.Handle(query);

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalItems, Is.EqualTo(0));
    }

    [Test]
    public async Task Handle_TotalItems_ExcludesSoftDeletedAuthors()
    {
        await Seed(
            MakeAuthor("active-1", "Alice"),
            MakeAuthor("active-2", "Bob"),
            MakeAuthor("deleted-1", "Charlie", isDeleted: true));

        var query = AuthorsQuery.WithDefaults(new Paging(1, 10));
        var result = await _handler.Handle(query);

        Assert.That(result.TotalItems, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_ReturnsCorrectPageMetadata()
    {
        await Seed(
            MakeAuthor("a1", "Author 1", "a-1"),
            MakeAuthor("a2", "Author 2", "a-2"),
            MakeAuthor("a3", "Author 3", "a-3"),
            MakeAuthor("a4", "Author 4", "a-4"),
            MakeAuthor("a5", "Author 5", "a-5"));

        var query = AuthorsQuery.WithDefaults(new Paging(1, 2));
        var result = await _handler.Handle(query);

        Assert.Multiple(() =>
        {
            Assert.That(result.Page, Is.EqualTo(1));
            Assert.That(result.PageSize, Is.EqualTo(2));
            Assert.That(result.TotalItems, Is.EqualTo(5));
            Assert.That(result.TotalPages, Is.EqualTo(3));
            Assert.That(result.Items, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Handle_SecondPage_SkipsFirstPageItems()
    {
        await Seed(
            MakeAuthor("slug-a", "Alice", "a-alice"),
            MakeAuthor("slug-b", "Bob", "b-bob"),
            MakeAuthor("slug-c", "Charlie", "c-charlie"));

        var query = AuthorsQuery.WithDefaults(new Paging(2, 2));
        var result = await _handler.Handle(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Slug, Is.EqualTo("slug-c"));
    }

    [Test]
    public async Task Handle_PageBeyondTotal_ReturnsEmptyItems()
    {
        await Seed(
            MakeAuthor("a1", "Alice"),
            MakeAuthor("a2", "Bob"));

        var query = AuthorsQuery.WithDefaults(new Paging(99, 10));
        var result = await _handler.Handle(query);

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalItems, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_DefaultSort_ReturnsAscendingBySortKey()
    {
        await Seed(
            MakeAuthor("slug-c", "Charlie", "c-charlie"),
            MakeAuthor("slug-a", "Alice", "a-alice"),
            MakeAuthor("slug-b", "Bob", "b-bob"));

        var query = AuthorsQuery.WithDefaults(new Paging(1, 10));
        var result = await _handler.Handle(query);

        Assert.That(
            result.Items.Select(a => a.Slug),
            Is.EqualTo(new[] { "slug-a", "slug-b", "slug-c" }));
    }

    [Test]
    public async Task Handle_SortNameDesc_ReturnsDescendingBySortKey()
    {
        await Seed(
            MakeAuthor("slug-c", "Charlie", "c-charlie"),
            MakeAuthor("slug-a", "Alice", "a-alice"),
            MakeAuthor("slug-b", "Bob", "b-bob"));

        var query = AuthorsQuery.Create(new Paging(1, 10), "name:desc");
        var result = await _handler.Handle(query);

        Assert.That(
            result.Items.Select(a => a.Slug),
            Is.EqualTo(new[] { "slug-c", "slug-b", "slug-a" }));
    }

    [Test]
    public async Task Handle_MapsToCorrectSummaryFields()
    {
        await Seed(MakeAuthor("jane-austen", "Jane Austen", "austen-jane"));

        var query = AuthorsQuery.WithDefaults(new Paging(1, 10));
        var result = await _handler.Handle(query);

        var item = result.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Slug, Is.EqualTo("jane-austen"));
            Assert.That(item.Name, Is.EqualTo("Jane Austen"));
        });
    }
}
