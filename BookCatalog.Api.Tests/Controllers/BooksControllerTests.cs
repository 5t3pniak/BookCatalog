using System.Net;
using BookCatalog.Api.Tests.Infrastructure;
using BookCatalog.Persistence.Models;

namespace BookCatalog.Api.Tests.Controllers;

[TestFixture]
public sealed class BooksControllerTests : ApiTestBase
{
    private async Task SeedBook(Book book, params Author[] authors)
    {
        await using var ctx = Factory.OpenDbContext();
        if (authors.Length > 0)
            await ctx.Authors.AddRangeAsync(authors);
        await ctx.Books.AddAsync(book);
        foreach (var a in authors)
            ctx.BookAuthors.Add(new BookAuthor { BookSlug = book.Slug, AuthorSlug = a.Slug });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedBooks(params (Book Book, Author[] Authors)[] items)
    {
        await using var ctx = Factory.OpenDbContext();
        var allAuthors = items.SelectMany(i => i.Authors).DistinctBy(a => a.Slug).ToList();
        if (allAuthors.Count > 0)
            await ctx.Authors.AddRangeAsync(allAuthors);
        foreach (var (book, authors) in items)
        {
            ctx.Books.Add(book);
            foreach (var a in authors)
                ctx.BookAuthors.Add(new BookAuthor { BookSlug = book.Slug, AuthorSlug = a.Slug });
        }
        await ctx.SaveChangesAsync();
    }

    [Test]
    public async Task GetBook_ExistingSlug_Returns200WithBook()
    {
        await SeedBook(MakeBook("pan-tadeusz", "Pan Tadeusz"));

        var (status, body) = await GetAsync<BookResponse>("/books/pan-tadeusz");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(body!.Slug, Is.EqualTo("pan-tadeusz"));
            Assert.That(body.Title, Is.EqualTo("Pan Tadeusz"));
        });
    }

    [Test]
    public async Task GetBook_NonExistentSlug_Returns404()
    {
        var (status, _) = await GetAsync<BookResponse>("/books/does-not-exist");

        Assert.That(status, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetBook_IncludesLinkedAuthors()
    {
        var author = MakeAuthor("adam-mickiewicz", "Adam Mickiewicz");
        await SeedBook(MakeBook("pan-tadeusz", "Pan Tadeusz"), author);

        var (status, body) = await GetAsync<BookResponse>("/books/pan-tadeusz");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body!.Authors, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(body.Authors[0].Slug, Is.EqualTo("adam-mickiewicz"));
            Assert.That(body.Authors[0].Name, Is.EqualTo("Adam Mickiewicz"));
        });
    }

    [Test]
    public async Task GetBook_DeletedBook_IsStillReturnedBySlug()
    {
        await SeedBook(MakeBook("old-book", "Old Book", isDeleted: true));

        var (status, body) = await GetAsync<BookResponse>("/books/old-book");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body!.Slug, Is.EqualTo("old-book"));
    }

    [Test]
    public async Task GetBooks_EmptyDatabase_Returns200WithEmptyList()
    {
        var (status, body) = await GetAsync<PagedResponse<BookResponse>>("/books");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(body!.Items, Is.Empty);
            Assert.That(body.TotalItems, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetBooks_FilterByAuthor_ReturnsOnlyBooksOfThatAuthor()
    {
        var mickiewicz = MakeAuthor("adam-mickiewicz", "Adam Mickiewicz");
        var slowacki   = MakeAuthor("juliusz-slowacki", "Juliusz Slowacki");

        await SeedBooks(
            (MakeBook("pan-tadeusz", "Pan Tadeusz"), [mickiewicz]),
            (MakeBook("dziady",      "Dziady"),      [mickiewicz]),
            (MakeBook("balladyna",   "Balladyna"),   [slowacki]));

        var (status, body) = await GetAsync<PagedResponse<BookResponse>>("/books?author=adam-mickiewicz");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body!.TotalItems, Is.EqualTo(2));
        Assert.That(body.Items.Select(b => b.Slug), Does.Not.Contain("balladyna"));
    }

    [Test]
    public async Task GetBooks_Pagination_ReturnsSecondPage()
    {
        var books = Enumerable.Range(1, 5)
            .Select(i => MakeBook($"book-{i:D2}", $"Book {i:D2}"))
            .ToArray();
        await SeedBooks(books.Select(b => (b, Array.Empty<Author>())).ToArray());

        var (status, body) = await GetAsync<PagedResponse<BookResponse>>("/books?page=2&page_size=3");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(body!.Page, Is.EqualTo(2));
            Assert.That(body.Items, Has.Count.EqualTo(2)); // 5 total, page_size=3 → page 2 has 2
            Assert.That(body.TotalItems, Is.EqualTo(5));
        });
    }
}
