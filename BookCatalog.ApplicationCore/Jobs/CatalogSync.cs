using System.Collections.Concurrent;
using BookCatalog.Integrations.OpenBooks.Contract;
using BookCatalog.Integrations.OpenBooks.HttpClient;
using BookCatalog.Persistence;
using BookCatalog.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace BookCatalog.ApplicationCore.Jobs;

public class CatalogSync(
    IOpenBooksClient api,
    IDbContextFactory<BookCatalogDbContext> dbFactory,
    ISystemClock clock,
    ILogger<CatalogSync> logger)
{
    public async Task RebuildFromBookDetailsAsync(int maxDop = 10, int saveBatchSize = 200,
        CancellationToken ct = default)
    {
        var list = await api.GetBooksAsync(ct);
        var dictBySlugs = list.ToDictionary(x => x.Slug);

        var details = new ConcurrentBag<RemoteBookDetail>();

        await Parallel.ForEachAsync(dictBySlugs, new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDop,
                CancellationToken = ct
            },
            async (kv, token) =>
            {
                var d = await api.GetBookDetailAsync(kv.Key, token);
                if (d != null)
                    details.Add(d);
            });

        logger.LogInformation("Rebuilding database...");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        await db.BookTags.ExecuteDeleteAsync(ct);
        await db.BookAuthors.ExecuteDeleteAsync(ct);
        await db.Tags.ExecuteDeleteAsync(ct);
        await db.Authors.ExecuteDeleteAsync(ct);
        await db.Books.ExecuteDeleteAsync(ct);

        var authorsBySlug = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tagsByKey = new Dictionary<(TagCategory Cat, string Slug), Tag>();


        logger.LogInformation("Saving 200 items ...");
        foreach (var batch in details.Chunk(saveBatchSize))
        {
            await using var dbBatch = await dbFactory.CreateDbContextAsync(ct);
            foreach (var remoteBookDetail in batch)
            {
                var book = new Book
                {
                    Slug = remoteBookDetail.Slug,
                    Title = remoteBookDetail.Title,
                    Url = remoteBookDetail.Url,
                    ThumbnailUrl = ChooseThumb(remoteBookDetail),
                    IsDeleted = false,
                    LastSyncedAt = clock.UtcNow,
                    PrimaryAuthorSortKey = dictBySlugs[remoteBookDetail.Slug].FullSortKey
                };
                dbBatch.Books.Add(book);

                AddAuthors(dbBatch, dictBySlugs, remoteBookDetail, authorsBySlug);

                AddTags(dbBatch, book.Slug, TagCategory.Epoch, remoteBookDetail.Epochs, tagsByKey);
                AddTags(dbBatch, book.Slug, TagCategory.Genre, remoteBookDetail.Genres, tagsByKey);
                AddTags(dbBatch, book.Slug, TagCategory.Kind, remoteBookDetail.Kinds, tagsByKey);
            }

            await dbBatch.SaveChangesAsync(ct);
        }
    }

    private void AddAuthors(
        BookCatalogDbContext dbBatch,
        Dictionary<string, RemoteBookListItem> remoteBookDictBySlugs,
        RemoteBookDetail remoteBookDetail,
        HashSet<string> authorsBySlug
        )
    {
        foreach (var remoteAuthorRef in remoteBookDetail.Authors)
        {
            if (authorsBySlug.Add(remoteAuthorRef.Slug))
            {
                dbBatch.Authors.Add(new Author
                {
                    Slug = remoteAuthorRef.Slug, Name = remoteAuthorRef.Name, SortKey = DetermineAuthorSortKey(remoteAuthorRef),
                    LastSyncedAt = clock.UtcNow
                });
            }

            dbBatch.BookAuthors.Add(new BookAuthor { BookSlug = remoteBookDetail.Slug, AuthorSlug = remoteAuthorRef.Slug });
        }

        string? DetermineAuthorSortKey(RemoteRef remoteAuthorRef)
        {
            var remoteBook = remoteBookDictBySlugs[remoteBookDetail.Slug];
            return string.Equals(remoteAuthorRef.Name, remoteBook.Author, StringComparison.InvariantCultureIgnoreCase)
                ? remoteBook.FullSortKey
                : null;
        }
    }

    private static void AddTags(
        BookCatalogDbContext db,
        string bookSlug,
        TagCategory cat,
        List<RemoteRef> refs,
        Dictionary<(TagCategory Cat, string Slug), Tag> tagsByKey)
    {
        foreach (var t in refs)
        {
            var key = (cat, t.Slug);

            if (!tagsByKey.TryGetValue(key, out var tag))
            {
                tag = new Tag { Category = cat, Slug = t.Slug, Name = t.Name, };
                tagsByKey[key] = tag;
                db.Tags.Add(tag);
            }

            if (tag.Id != 0)
                db.BookTags.Add(new BookTag { BookSlug = bookSlug, TagId = tag.Id });
            else
                db.BookTags.Add(new BookTag { BookSlug = bookSlug, Tag = tag });
        }
    }

    private static string? ChooseThumb(RemoteBookDetail d)
        => !string.IsNullOrWhiteSpace(d.SimpleThumb) ? d.SimpleThumb
            : !string.IsNullOrWhiteSpace(d.CoverThumb) ? d.CoverThumb
            : null;
}