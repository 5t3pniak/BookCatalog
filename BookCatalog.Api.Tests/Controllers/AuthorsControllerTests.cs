using System.Net;
using BookCatalog.Api.Tests.Infrastructure;
using BookCatalog.Persistence.Models;

namespace BookCatalog.Api.Tests.Controllers;

[TestFixture]
public sealed class AuthorsControllerTests : ApiTestBase
{
    private async Task Seed(params Author[] authors)
    {
        await using var ctx = Factory.OpenDbContext();
        await ctx.Authors.AddRangeAsync(authors);
        await ctx.SaveChangesAsync();
    }

    [Test]
    public async Task GetAuthors_EmptyDatabase_Returns200WithEmptyList()
    {
        var (status, body) = await GetAsync<PagedResponse<AuthorResponse>>("/authors");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(body!.Items, Is.Empty);
            Assert.That(body.TotalItems, Is.EqualTo(0));
            Assert.That(body.Page, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetAuthors_SeededAuthors_ReturnsThemInResponse()
    {
        await Seed(
            MakeAuthor("adam-mickiewicz", "Adam Mickiewicz"),
            MakeAuthor("juliusz-slowacki", "Juliusz Slowacki"));

        var (status, body) = await GetAsync<PagedResponse<AuthorResponse>>("/authors");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body!.TotalItems, Is.EqualTo(2));
        Assert.That(body.Items.Select(a => a.Slug), Contains.Item("adam-mickiewicz"));
        Assert.That(body.Items.Select(a => a.Slug), Contains.Item("juliusz-slowacki"));
    }

    [Test]
    public async Task GetAuthors_DefaultPaging_UsesPage1Size20()
    {
        var authors = Enumerable.Range(1, 25)
            .Select(i => MakeAuthor($"author-{i:D2}", $"Author {i:D2}", $"author-{i:D2}"))
            .ToArray();
        await Seed(authors);

        var (status, body) = await GetAsync<PagedResponse<AuthorResponse>>("/authors");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(body!.Page, Is.EqualTo(1));
            Assert.That(body.PageSize, Is.EqualTo(20));
            Assert.That(body.Items, Has.Count.EqualTo(20));
            Assert.That(body.TotalItems, Is.EqualTo(25));
            Assert.That(body.TotalPages, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetAuthors_SecondPage_ReturnsRemainingItems()
    {
        var authors = Enumerable.Range(1, 5)
            .Select(i => MakeAuthor($"author-{i:D2}", $"Author {i:D2}", $"author-{i:D2}"))
            .ToArray();
        await Seed(authors);

        var (status, body) = await GetAsync<PagedResponse<AuthorResponse>>("/authors?page=2&page_size=3");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(body!.Page, Is.EqualTo(2));
            Assert.That(body.Items, Has.Count.EqualTo(2)); // 5 total, page_size=3 → page 2 has 2
            Assert.That(body.TotalItems, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetAuthors_SortByNameDesc_ReturnsDescendingBySortKey()
    {
        await Seed(
            MakeAuthor("author-a", "Alice", "a-alice"),
            MakeAuthor("author-b", "Bob",   "b-bob"),
            MakeAuthor("author-c", "Charlie", "c-charlie"));

        var (status, body) = await GetAsync<PagedResponse<AuthorResponse>>("/authors?sort_by=name:desc");

        Assert.That(status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(
            body!.Items.Select(a => a.Slug),
            Is.EqualTo(new[] { "author-c", "author-b", "author-a" }));
    }
}
