using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Common;

// Maps unhandled exceptions to RFC 7807 ProblemDetails centrally, so no controller
// translates errors and no stack trace ever reaches the client.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails) =>
        _problemDetails = problemDetails;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = Map(exception);

        context.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            }
        });
    }

    // Unmapped exceptions get a generic 500 detail — the real message is never leaked.
    private static (int Status, string Title, string Detail) Map(Exception exception) =>
        exception switch
        {
            DomainException ex => (
                StatusCodes.Status400BadRequest,
                "Invalid invoice operation",
                ex.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency conflict",
                "The invoice was modified by another request. Re-read it and retry."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "An unexpected error occurred while processing the request.")
        };
}
