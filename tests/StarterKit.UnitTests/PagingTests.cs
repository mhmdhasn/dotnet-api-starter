using StarterKit.Api.Common.Pagination;

namespace StarterKit.UnitTests;

public class PagingTests
{
    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, -1, 1, 1)]
    [InlineData(1, 1000, 1, 100)]
    [InlineData(3, 50, 3, 50)]
    public void Normalize_ClampsInput(int? page, int? size, int expectedPage, int expectedSize)
    {
        (int normalizedPage, int normalizedSize) = Paging.Normalize(page, size);

        Assert.Equal(expectedPage, normalizedPage);
        Assert.Equal(expectedSize, normalizedSize);
    }

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(100, 100, 1)]
    [InlineData(101, 100, 2)]
    [InlineData(25, 10, 3)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expectedPages)
    {
        var result = new PagedResult<int>([], 1, pageSize, totalCount);

        Assert.Equal(expectedPages, result.TotalPages);
    }

    [Fact]
    public void TotalPages_IsZero_WhenPageSizeIsInvalid()
    {
        var result = new PagedResult<int>([], 1, 0, 50);

        Assert.Equal(0, result.TotalPages);
    }
}
