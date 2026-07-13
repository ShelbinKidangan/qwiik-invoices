using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Domain;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;

namespace Qwiik.Invoices.Api.Features.Invoices;

// HTTP-only: parse, call InvoiceService, translate result/errors to a status code.
// Errors map to RFC 7807 locally for now; centralised middleware is deferred to Stage #5.
[ApiController]
[Route("api/v1/invoices")]
[Produces("application/json")]
public sealed class InvoicesController : ControllerBase
{
    private readonly InvoiceService _service;
    private readonly IValidator<CreateInvoiceRequest> _createValidator;

    public InvoicesController(InvoiceService service, IValidator<CreateInvoiceRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateInvoiceRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            foreach (var failure in validation.Errors)
                ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
            return ValidationProblem(ModelState);
        }

        try
        {
            var created = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var invoice = await _service.GetByIdAsync(id, ct);
        return invoice is null ? NotFoundProblem(id) : Ok(invoice);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<InvoiceListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] PageQuery query, CancellationToken ct)
    {
        var page = await _service.ListAsync(query, ct);
        return Ok(page);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(Guid id, UpdateStatusRequest request, CancellationToken ct)
    {
        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
            return Problem(
                title: "Invalid concurrency token",
                detail: "The supplied RowVersion is not valid base64.",
                statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var updated = await _service.ChangeStatusAsync(id, request.Status, rowVersion, ct);
            return updated is null ? NotFoundProblem(id) : Ok(updated);
        }
        catch (DomainException ex)
        {
            return DomainProblem(ex);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "Concurrency conflict",
                detail: "The invoice was modified by another request. Re-read it and retry.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(InvoiceSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var summary = await _service.GetSummaryAsync(ct);
        return Ok(summary);
    }

    private ObjectResult DomainProblem(DomainException ex) => Problem(
        title: "Invalid invoice operation",
        detail: ex.Message,
        statusCode: StatusCodes.Status400BadRequest);

    private ObjectResult NotFoundProblem(Guid id) => Problem(
        title: "Invoice not found",
        detail: $"No invoice with id '{id}' exists for this tenant.",
        statusCode: StatusCodes.Status404NotFound);

    private static bool TryParseRowVersion(string value, out byte[] rowVersion)
    {
        rowVersion = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
