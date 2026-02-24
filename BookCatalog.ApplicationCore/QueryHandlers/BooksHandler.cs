using BookCatalog.ApplicationCore.Helpers;
using BookCatalog.ApplicationCore.QueryHandlers.Models;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.ApplicationCore.QueryHandlers;

public interface IBooksHandler
{
    Task<BookSummary?> Handle(string slug, CancellationToken ct = default);
    Task<PagedResult<BookSummary>> Handle(BooksQuery query, CancellationToken ct = default);
}

public class BooksHandler(BookCatalogDbContext context) : IBooksHandler
{
    public async Task<BookSummary?> Handle(string slug, CancellationToken ct = default)
    {
        return await context.Books.AsNoTracking()
            .Where(b => b.Slug == slug)
            .Select(b => new BookSummary(b.Slug, b.Title, b.Url, b.ThumbnailUrl,
                b.BookAuthors.Select(a => new AuthorSummary(a.Author.Slug, a.Author.Name)).ToList()))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<BookSummary>> Handle(BooksQuery query, CancellationToken ct = default)
    {
        var q = context.Books.AsNoTracking().Where(b => !b.IsDeleted);

        if (!string.IsNullOrEmpty(query.Author))
            q = q.Where(b => b.BookAuthors.Any(a => a.Author.Slug == query.Author));

        if (!string.IsNullOrEmpty(query.Epoch))
            q = q.Where(b => b.BookTags.Any(bt =>
                bt.Tag.Category == TagCategory.Epoch && query.Epoch == bt.Tag.Slug));

        if (!string.IsNullOrEmpty(query.Kind))
            q = q.Where(b => b.BookTags.Any(bt =>
                bt.Tag.Category == TagCategory.Kind && query.Kind == bt.Tag.Slug));

        if (!string.IsNullOrEmpty(query.Genre))
            q = q.Where(b => b.BookTags.Any(bt =>
                bt.Tag.Category == TagCategory.Genre && query.Genre == bt.Tag.Slug));

        var total = await q.CountAsync(ct);
        
        var sorted = ApplySort(q, query.Sort ?? BooksQuery.DefaultSort);

        var items = await sorted
            .Skip(query.Paging.Skip)
            .Take(query.Paging.Take)
            .Select(b => new BookSummary(
                b.Slug,
                b.Title,
                b.Url,
                b.ThumbnailUrl,
                b.BookAuthors.Select(a => new AuthorSummary(a.Author.Slug, a.Author.Name)).ToList()))
            .ToListAsync(ct);
        
        return new PagedResult<BookSummary>(items, query.Paging.Page, query.Paging.Take, total);
    }

    private static IOrderedQueryable<Book> ApplySort(
        IQueryable<Book> q,
        SortDescriptor sort)
    {
        var ordered = sort.Field switch
        {
            "title" => sort.Direction == SortDirection.Asc
                ? q.OrderBy(b => b.Title)
                : q.OrderByDescending(b => b.Title),
            "author" => sort.Direction == SortDirection.Asc
                ? q.OrderBy(b => b.PrimaryAuthorSortKey)
                : q.OrderByDescending(b => b.PrimaryAuthorSortKey),
            _ => q.OrderBy(b => b.PrimaryAuthorSortKey)
        };
        
        return ordered;
    }
}