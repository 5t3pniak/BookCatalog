namespace BookCatalog.ApplicationCore.Helpers;

public record Paging
{
    public int Page { get; }
    public int PageSize { get; }
    
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;

    public Paging(int page, int pageSize, int maxPageSize = 100)
    {
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, maxPageSize);
    }
}