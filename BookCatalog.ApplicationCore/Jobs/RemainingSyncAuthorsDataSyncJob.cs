using System.Collections.Concurrent;
using BookCatalog.Integrations.OpenBooks.HttpClient;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickerQ.Utilities.Base;

namespace BookCatalog.ApplicationCore.Jobs;

public class RemainingSyncAuthorsDataSyncJob(IOpenBooksClient openBooksClient, BookCatalogDbContext dbContext, ILogger<RemainingSyncAuthorsDataSyncJob> logger)
{
    [TickerFunction(functionName: nameof(RemainingSyncAuthorsDataSyncJob.AuthorsSync))]
    public async Task AuthorsSync(TickerFunctionContext context, CancellationToken cancellationToken)
    {
        var authorsSlugsMissingSortingKey = await dbContext.Authors
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.SortKey == null)
            .Select(x => x.Slug).
            ToListAsync(cancellationToken);
        
        logger.LogInformation("Found {remainingAuthors} authors to update sorting key", authorsSlugsMissingSortingKey.Count);
        
        var sortKeys = new ConcurrentBag<(string authorSlug, string  sortKey)>();

        await Parallel.ForEachAsync(authorsSlugsMissingSortingKey, new ParallelOptions
            {
                MaxDegreeOfParallelism = 10,
                CancellationToken = cancellationToken
            },
            async (slug, token) =>
            {
                var d = await openBooksClient.GetAuthorDetailAsync(slug, token);
                if (d != null)
                    sortKeys.Add((slug, d.SortKey));
            });

        logger.LogInformation("Updating authors sorting key");
        
        foreach (var item in sortKeys)
        {
            var author = new Author { Slug = item.authorSlug, SortKey = item.sortKey };
            dbContext.Authors.Attach(author);
            dbContext.Entry(author).Property(a => a.SortKey).IsModified = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}