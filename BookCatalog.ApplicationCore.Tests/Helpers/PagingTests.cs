using BookCatalog.ApplicationCore.Helpers;

namespace BookCatalog.ApplicationCore.Tests.Helpers;

public class PagingTests
{
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-100)]
    public void Page_WhenLessThanOne_ClampsToOne(int page)
    {
        var paging = new Paging(page, 10);

        Assert.That(paging.Page, Is.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(100)]
    public void Page_WhenGreaterThanOrEqualToOne_IsUnchanged(int page)
    {
        var paging = new Paging(page, 10);

        Assert.That(paging.Page, Is.EqualTo(page));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void PageSize_WhenLessThanOne_ClampsToOne(int pageSize)
    {
        var paging = new Paging(1, pageSize);

        Assert.That(paging.PageSize, Is.EqualTo(1));
    }

    [TestCase(101)]
    [TestCase(500)]
    public void PageSize_WhenExceedsDefaultMaxPageSize_ClampsTo100(int pageSize)
    {
        var paging = new Paging(1, pageSize);

        Assert.That(paging.PageSize, Is.EqualTo(100));
    }

    [Test]
    public void PageSize_WhenExceedsCustomMaxPageSize_ClampsToCustomMax()
    {
        var paging = new Paging(1, 50, maxPageSize: 20);

        Assert.That(paging.PageSize, Is.EqualTo(20));
    }

    [Test]
    public void PageSize_WhenWithinCustomMaxPageSize_IsUnchanged()
    {
        var paging = new Paging(1, 15, maxPageSize: 20);

        Assert.That(paging.PageSize, Is.EqualTo(15));
    }

    [TestCase(1, 10, 0)]
    [TestCase(2, 10, 10)]
    [TestCase(3, 10, 20)]
    [TestCase(5, 25, 100)]
    public void Skip_IsCalculatedCorrectly(int page, int pageSize, int expectedSkip)
    {
        var paging = new Paging(page, pageSize);

        Assert.That(paging.Skip, Is.EqualTo(expectedSkip));
    }

    [TestCase(10)]
    [TestCase(25)]
    [TestCase(50)]
    public void Take_EqualsPageSize(int pageSize)
    {
        var paging = new Paging(1, pageSize);

        Assert.That(paging.Take, Is.EqualTo(paging.PageSize));
    }
}
