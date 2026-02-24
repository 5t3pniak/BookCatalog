using BookCatalog.ApplicationCore.Helpers;

namespace BookCatalog.ApplicationCore.Tests.Helpers;

public class PagedResultTests
{
    [Test]
    public void TotalPages_WhenPageSizeIsZero_ReturnsZero()
    {
        var result = new PagedResult<int>([], 1, 0, 50);

        Assert.That(result.TotalPages, Is.EqualTo(0));
    }

    [Test]
    public void TotalPages_WhenTotalItemsIsZero_ReturnsZero()
    {
        var result = new PagedResult<int>([], 1, 10, 0);

        Assert.That(result.TotalPages, Is.EqualTo(0));
    }

    [Test]
    public void TotalPages_WhenTotalItemsDivisibleByPageSize_ReturnsExactQuotient()
    {
        var result = new PagedResult<int>([], 1, 5, 10);

        Assert.That(result.TotalPages, Is.EqualTo(2));
    }

    [Test]
    public void TotalPages_WhenTotalItemsNotDivisibleByPageSize_ReturnsCeiling()
    {
        var result = new PagedResult<int>([], 1, 5, 11);

        Assert.That(result.TotalPages, Is.EqualTo(3));
    }

    [Test]
    public void TotalPages_WhenSingleItem_ReturnsSinglePage()
    {
        var result = new PagedResult<string>([], 1, 10, 1);

        Assert.That(result.TotalPages, Is.EqualTo(1));
    }

    [Test]
    public void Items_AreStoredCorrectly()
    {
        List<string> items = ["a", "b", "c"];
        var result = new PagedResult<string>(items, 2, 3, 9);

        Assert.That(result.Items, Is.EqualTo(items));
    }
}
