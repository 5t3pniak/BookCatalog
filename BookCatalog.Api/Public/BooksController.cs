using BookCatalog.ApplicationCore.Helpers;
using BookCatalog.ApplicationCore.QueryHandlers;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Public;

[ApiController]
[Route("[controller]")]
public class BooksController(IBooksHandler booksHandler) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBook(string slug, CancellationToken ct = default)
    {
        var book = await booksHandler.Handle(slug, ct);
        return book == null ? NotFound($"Book not found: {slug}") : Ok(book);
    }

    [HttpGet]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        [FromQuery] string author = default!,
        [FromQuery] string epoch = default!,
        [FromQuery] string kind = default!,
        [FromQuery] string genre = default!,
        [FromQuery(Name = "sort_by")] string? sortBy = null,
        CancellationToken ct = default)
    {
        var query = BooksQuery.Create(new Paging(page, pageSize), sortBy, author, epoch, kind, genre);
        var result = await booksHandler.Handle(query, ct);
        return Ok(result);
    }
}
