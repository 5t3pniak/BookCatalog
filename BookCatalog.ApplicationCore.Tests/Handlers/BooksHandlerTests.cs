using BookCatalog.ApplicationCore.Helpers;
using BookCatalog.ApplicationCore.QueryHandlers;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace BookCatalog.ApplicationCore.Tests.Handlers;

public class BooksHandlerTests
{
    private MsSqlContainer _sqlContainer = null!;
    private BookCatalogDbContext _context = null!;
    private BooksHandler _handler = null!;

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
    public void SetUp() => _handler = new BooksHandler(_context);

    [TearDown]
    public async Task TearDown()
    {
        
        _context.ChangeTracker.Clear();
        await _context.BookTags.ExecuteDeleteAsync();
        await _context.BookAuthors.ExecuteDeleteAsync();
        await _context.Books.ExecuteDeleteAsync();
        await _context.Authors.ExecuteDeleteAsync();
        await _context.Tags.ExecuteDeleteAsync();
    }

    private static Book MakeBook(
        string slug,
        string title,
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

    private static Author MakeAuthor(string slug, string name) => new()
    {
        Slug = slug,
        Name = name,
        SortKey = name.ToLowerInvariant(),
        LastSyncedAt = DateTimeOffset.UtcNow
    };
    
    private static Tag MakeTag(TagCategory category, string slug) => new()
    {
        Category = category,
        Slug = slug,
        Name = slug
    };

    [Test]
    public async Task HandleBySlug_WhenBookExists_ReturnsSummaryWithCorrectFields()
    {
        _context.Books.Add(MakeBook("crime-punishment", "Crime and Punishment"));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle("crime-punishment");

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Slug, Is.EqualTo("crime-punishment"));
            Assert.That(result.Title, Is.EqualTo("Crime and Punishment"));
            Assert.That(result.Url, Is.EqualTo("https://example.com/crime-punishment"));
            Assert.That(result.Authors, Is.Empty);
        });
    }

    [Test]
    public async Task HandleBySlug_WhenBookDoesNotExist_ReturnsNull()
    {
        var result = await _handler.Handle("nonexistent-slug");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task HandleBySlug_IncludesLinkedAuthors()
    {
        _context.Authors.AddRange(
            MakeAuthor("dostoevsky", "Fyodor Dostoevsky"),
            MakeAuthor("tolstoy", "Leo Tolstoy"));
        _context.Books.Add(MakeBook("collab", "A Collaboration"));
        await _context.SaveChangesAsync();

        _context.BookAuthors.AddRange(
            new BookAuthor { BookSlug = "collab", AuthorSlug = "dostoevsky" },
            new BookAuthor { BookSlug = "collab", AuthorSlug = "tolstoy" });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle("collab");

        Assert.That(result!.Authors, Has.Count.EqualTo(2));
        Assert.That(
            result.Authors.Select(a => a.Slug),
            Is.EquivalentTo(new[] { "dostoevsky", "tolstoy" }));
    }

    [Test]
    public async Task HandleQuery_WhenNoBooks_ReturnsEmptyPagedResult()
    {
        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(1, 10)));

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalItems, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleQuery_FiltersOutSoftDeletedBooks()
    {
        _context.Books.AddRange(
            MakeBook("book-1", "Book One"),
            MakeBook("book-2", "Book Two"),
            MakeBook("deleted", "Deleted", isDeleted: true));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(1, 10)));

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(b => b.Slug), Does.Not.Contain("deleted"));
    }

    [Test]
    public async Task HandleQuery_TotalItems_ExcludesSoftDeletedBooks()
    {
        _context.Books.AddRange(
            MakeBook("book-1", "Book One"),
            MakeBook("deleted", "Deleted", isDeleted: true));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(1, 10)));

        Assert.That(result.TotalItems, Is.EqualTo(1));
    }

    [Test]
    public async Task HandleQuery_FilterByAuthor_ReturnsOnlyBooksLinkedToThatAuthor()
    {
        _context.Authors.AddRange(
            MakeAuthor("dostoevsky", "Fyodor Dostoevsky"),
            MakeAuthor("tolstoy", "Leo Tolstoy"));
        _context.Books.AddRange(
            MakeBook("book-d", "Dostoevsky Book"),
            MakeBook("book-t", "Tolstoy Book"),
            MakeBook("book-both", "Collaboration"));
        await _context.SaveChangesAsync();

        _context.BookAuthors.AddRange(
            new BookAuthor { BookSlug = "book-d",    AuthorSlug = "dostoevsky" },
            new BookAuthor { BookSlug = "book-t",    AuthorSlug = "tolstoy" },
            new BookAuthor { BookSlug = "book-both", AuthorSlug = "dostoevsky" },
            new BookAuthor { BookSlug = "book-both", AuthorSlug = "tolstoy" });
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), null, "dostoevsky", "", "", "");
        var result = await _handler.Handle(query);

        Assert.That(
            result.Items.Select(b => b.Slug),
            Is.EquivalentTo(new[] { "book-d", "book-both" }));
    }

    [Test]
    public async Task HandleQuery_FilterByEpoch_ReturnsOnlyMatchingBooks()
    {
        _context.Books.AddRange(
            MakeBook("book-modernism", "Modern Book"),
            MakeBook("book-no-tag", "Untagged Book"));
        var tag = MakeTag(TagCategory.Epoch, "modernism");
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        _context.BookTags.Add(new BookTag { BookSlug = "book-modernism", TagId = tag.Id });
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), null, "", "modernism", "", "");
        var result = await _handler.Handle(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Slug, Is.EqualTo("book-modernism"));
    }

    [Test]
    public async Task HandleQuery_FilterByKind_ReturnsOnlyMatchingBooks()
    {
        _context.Books.AddRange(
            MakeBook("novel", "A Novel"),
            MakeBook("poem", "A Poem"));
        var tag = MakeTag(TagCategory.Kind, "novel");
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        _context.BookTags.Add(new BookTag { BookSlug = "novel", TagId = tag.Id });
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), null, "", "", "novel", "");
        var result = await _handler.Handle(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Slug, Is.EqualTo("novel"));
    }

    [Test]
    public async Task HandleQuery_FilterByGenre_ReturnsOnlyMatchingBooks()
    {
        _context.Books.AddRange(
            MakeBook("drama", "A Drama"),
            MakeBook("comedy", "A Comedy"));
        var tag = MakeTag(TagCategory.Genre, "drama");
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        _context.BookTags.Add(new BookTag { BookSlug = "drama", TagId = tag.Id });
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), null, "", "", "", "drama");
        var result = await _handler.Handle(query);

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Slug, Is.EqualTo("drama"));
    }

    [Test]
    public async Task HandleQuery_TagFilter_DoesNotMatchWrongCategory()
    {
        // Tag slug "drama" exists but as a Kind, not a Genre — genre filter must not match it
        _context.Books.Add(MakeBook("drama-book", "Drama Book"));
        var tag = MakeTag(TagCategory.Kind, "drama");
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();

        _context.BookTags.Add(new BookTag { BookSlug = "drama-book", TagId = tag.Id });
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), null, "", "", "", "drama");
        var result = await _handler.Handle(query);

        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public async Task HandleQuery_DefaultSort_SortsByTitleAscending()
    {
        _context.Books.AddRange(
            MakeBook("book-z", "Zorro"),
            MakeBook("book-a", "Alice in Wonderland"),
            MakeBook("book-m", "Moby Dick"));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(1, 10)));

        Assert.That(
            result.Items.Select(b => b.Slug),
            Is.EqualTo(new[] { "book-a", "book-m", "book-z" }));
    }

    [Test]
    public async Task HandleQuery_SortByTitleDesc_ReturnsTitlesDescending()
    {
        _context.Books.AddRange(
            MakeBook("book-z", "Zorro"),
            MakeBook("book-a", "Alice in Wonderland"),
            MakeBook("book-m", "Moby Dick"));
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), "title:desc", "", "", "", "");
        var result = await _handler.Handle(query);

        Assert.That(
            result.Items.Select(b => b.Slug),
            Is.EqualTo(new[] { "book-z", "book-m", "book-a" }));
    }

    [Test]
    public async Task HandleQuery_SortByAuthorAsc_SortsByPrimaryAuthorSortKeyAscending()
    {
        _context.Books.AddRange(
            MakeBook("book-t", "Tolstoy Book",  primaryAuthorSortKey: "c-tolstoy"),
            MakeBook("book-a", "Austen Book",   primaryAuthorSortKey: "a-austen"),
            MakeBook("book-m", "Melville Book", primaryAuthorSortKey: "b-melville"));
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), "author:asc", "", "", "", "");
        var result = await _handler.Handle(query);

        Assert.That(
            result.Items.Select(b => b.Slug),
            Is.EqualTo(new[] { "book-a", "book-m", "book-t" }));
    }

    [Test]
    public async Task HandleQuery_SortByAuthorDesc_SortsByPrimaryAuthorSortKeyDescending()
    {
        _context.Books.AddRange(
            MakeBook("book-t", "Tolstoy Book",  primaryAuthorSortKey: "c-tolstoy"),
            MakeBook("book-a", "Austen Book",   primaryAuthorSortKey: "a-austen"),
            MakeBook("book-m", "Melville Book", primaryAuthorSortKey: "b-melville"));
        await _context.SaveChangesAsync();

        var query = BooksQuery.Create(new Paging(1, 10), "author:desc", "", "", "", "");
        var result = await _handler.Handle(query);

        Assert.That(
            result.Items.Select(b => b.Slug),
            Is.EqualTo(new[] { "book-t", "book-m", "book-a" }));
    }

    [Test]
    public async Task HandleQuery_ReturnsCorrectPageMetadata()
    {
        _context.Books.AddRange(Enumerable.Range(1, 5)
            .Select(i => MakeBook($"book-{i}", $"Book {i:D2}")));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(1, 2)));

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
    public async Task HandleQuery_SecondPage_SkipsFirstPageItems()
    {
        _context.Books.AddRange(
            MakeBook("book-1", "Alpha"),
            MakeBook("book-2", "Beta"),
            MakeBook("book-3", "Gamma"));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(2, 2)));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Slug, Is.EqualTo("book-3"));
    }

    [Test]
    public async Task HandleQuery_PageBeyondTotal_ReturnsEmptyItemsButCorrectTotal()
    {
        _context.Books.AddRange(
            MakeBook("book-1", "Alpha"),
            MakeBook("book-2", "Beta"));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(99, 10)));

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalItems, Is.EqualTo(2));
    }

    [Test]
    public async Task HandleQuery_IncludesLinkedAuthorsInEachBookSummary()
    {
        _context.Authors.Add(MakeAuthor("austen", "Jane Austen"));
        _context.Books.Add(MakeBook("pride-prejudice", "Pride and Prejudice"));
        await _context.SaveChangesAsync();

        _context.BookAuthors.Add(new BookAuthor { BookSlug = "pride-prejudice", AuthorSlug = "austen" });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(BooksQuery.WithDefaults(new Paging(1, 10)));

        var book = result.Items.Single();
        Assert.That(book.Authors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(book.Authors[0].Slug, Is.EqualTo("austen"));
            Assert.That(book.Authors[0].Name, Is.EqualTo("Jane Austen"));
        });
    }
}
