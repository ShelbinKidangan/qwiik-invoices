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
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = Map(exception);

        Log(exception, status);

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

    // A 500 is a genuine fault (log the exception); mapped 4xx cases are expected.
    private void Log(Exception exception, int status)
    {
        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception processing the request");
        else
            _logger.LogWarning(
                "Request failed with {StatusCode}: {Message}", status, exception.Message);
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
