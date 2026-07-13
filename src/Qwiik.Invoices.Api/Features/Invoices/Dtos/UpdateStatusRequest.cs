using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Features.Invoices.Dtos;

// Id comes from the route, never the body. RowVersion is the base64 concurrency token
// the client last read.
public sealed record UpdateStatusRequest(
    InvoiceStatus Status,
    string RowVersion);
