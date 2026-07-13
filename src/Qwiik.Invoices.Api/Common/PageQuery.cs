using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Common;

public sealed record PageQuery
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public InvoiceStatus? Status { get; init; }
    public DateOnly? IssueDateFrom { get; init; }
    public DateOnly? IssueDateTo { get; init; }

    public int NormalizedPage => Page < 1 ? 1 : Page;

    // Clamped server-side so a client cannot request an unbounded page.
    public int NormalizedPageSize =>
        PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);
}
