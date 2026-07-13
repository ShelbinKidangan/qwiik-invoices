using Qwiik.Invoices.Api.Common;

namespace Qwiik.Invoices.Tests.Common;

/// <summary>
/// Paging inputs are normalized server-side so a client cannot request page zero or an
/// unbounded page size.
/// </summary>
public class PageQueryTests
{
    [Fact]
    public void NormalizedPage_PageBelowOne_ReturnsOne()
    {
        var query = new PageQuery { Page = 0 };
        query.NormalizedPage.Should().Be(1);
    }

    [Fact]
    public void NormalizedPageSize_BelowOne_ReturnsDefault()
    {
        var query = new PageQuery { PageSize = 0 };
        query.NormalizedPageSize.Should().Be(PageQuery.DefaultPageSize);
    }

    [Fact]
    public void NormalizedPageSize_AboveMax_ClampsTo100()
    {
        var query = new PageQuery { PageSize = 500 };
        query.NormalizedPageSize.Should().Be(100);
        PageQuery.MaxPageSize.Should().Be(100);
    }
}
