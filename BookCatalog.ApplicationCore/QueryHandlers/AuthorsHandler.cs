using BookCatalog.ApplicationCore.Helpers;
using BookCatalog.ApplicationCore.QueryHandlers.Models;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace BookCatalog.ApplicationCore.QueryHandlers;

public interface IAuthorsHandler
{
    Task<PagedResult<AuthorSummary>> Handle(AuthorsQuery query, CancellationToken ct = default);
}

public class AuthorsHandler(BookCatalogDbContext context) : IAuthorsHandler
{
    public async Task<PagedResult<AuthorSummary>> Handle(AuthorsQuery query, CancellationToken ct = default)
    {
        var q = context.Authors.AsNoTracking().Where(a => !a.IsDeleted);
        var total = await q.CountAsync(ct);
        var sort = query.Sort ?? AuthorsQuery.DefaultSort;
        var sorted = ApplySort(q, sort);

        var items = await sorted
            .Skip(query.Paging.Skip)
            .Take(query.Paging.Take)
            .Select(a => new AuthorSummary(a.Slug, a.Name))
            .ToListAsync(ct);
        
        return new PagedResult<AuthorSummary>(items, query.Paging.Page, query.Paging.PageSize, total);
    }

    private static IOrderedQueryable<Author> ApplySort(
        IQueryable<Author> q,
        SortDescriptor sort)
    {
        var ordered = sort.Field switch
        {
            "name" => sort.Direction == SortDirection.Asc ? q.OrderBy(a => a.SortKey) : q.OrderByDescending(a => a.SortKey),
            _ => q.OrderBy(a => a.SortKey)
        };
        
        return ordered;
    }
}