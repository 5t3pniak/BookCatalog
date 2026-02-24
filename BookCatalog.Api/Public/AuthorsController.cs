using BookCatalog.ApplicationCore.Helpers;
using BookCatalog.ApplicationCore.QueryHandlers;
using Microsoft.AspNetCore.Mvc;

namespace BookCatalog.Api.Public;

[ApiController]
[Route("[controller]")]
public class AuthorsController(IAuthorsHandler authorsHandler) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAuthors(
        [FromQuery] int page = 1,
        [FromQuery(Name = "page_size")] int pageSize = 20,
        [FromQuery(Name = "sort_by")] string? sortBy = null,
        CancellationToken ct = default)
    {
        var query = AuthorsQuery.Create(new Paging(page, pageSize), sortBy);
        var result = await authorsHandler.Handle(query, ct);
        return Ok(result);
    }
}
