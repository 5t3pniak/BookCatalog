using BookCatalog.ApplicationCore.Helpers;

namespace BookCatalog.ApplicationCore.Tests.Helpers;

public class SortDescriptorTests
{
    private static readonly IReadOnlySet<string> AllowedFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "title", "author", "year" };

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_WhenRawIsNullOrWhiteSpace_ReturnsNull(string? raw)
    {
        var result = SortDescriptor.Parse(raw, AllowedFields);

        Assert.That(result, Is.Null);
    }

    [TestCase("unknown")]
    [TestCase("pages")]
    [TestCase("unknown:asc")]
    public void Parse_WhenFieldNotAllowed_ReturnsNull(string raw)
    {
        var result = SortDescriptor.Parse(raw, AllowedFields);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Parse_WhenFieldAllowed_WithoutDirection_ReturnsAscending()
    {
        var result = SortDescriptor.Parse("title", AllowedFields);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Field, Is.EqualTo("title"));
        Assert.That(result.Direction, Is.EqualTo(SortDirection.Asc));
    }

    [TestCase("title:asc")]
    [TestCase("title:ASC")]
    [TestCase("title:Asc")]
    public void Parse_WhenDirectionIsAsc_ReturnsSortDirectionAsc(string raw)
    {
        var result = SortDescriptor.Parse(raw, AllowedFields);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Direction, Is.EqualTo(SortDirection.Asc));
    }

    [TestCase("title:desc")]
    [TestCase("title:DESC")]
    [TestCase("title:Desc")]
    public void Parse_WhenDirectionIsDesc_ReturnsSortDirectionDesc(string raw)
    {
        var result = SortDescriptor.Parse(raw, AllowedFields);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Direction, Is.EqualTo(SortDirection.Desc));
    }

    [TestCase("TITLE")]
    [TestCase("Title")]
    [TestCase("AUTHOR")]
    public void Parse_WhenFieldCaseVaries_IsAccepted(string raw)
    {
        var result = SortDescriptor.Parse(raw, AllowedFields);

        Assert.That(result, Is.Not.Null);
    }

    [TestCase("TITLE", "title")]
    [TestCase("AUTHOR", "author")]
    [TestCase("YEAR", "year")]
    public void Parse_FieldIsAlwaysLowercased(string raw, string expectedField)
    {
        var result = SortDescriptor.Parse(raw, AllowedFields);

        Assert.That(result!.Field, Is.EqualTo(expectedField));
    }

    [Test]
    public void Parse_WhenUnrecognizedDirection_DefaultsToAsc()
    {
        var result = SortDescriptor.Parse("title:random", AllowedFields);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Direction, Is.EqualTo(SortDirection.Asc));
    }
}
