using Serilog.Context;

namespace Qwiik.Invoices.Api.Common;

/// <summary>
/// Assigns each request a correlation id — honouring an inbound <c>X-Correlation-Id</c>
/// or generating one — echoes it on the response, and pushes it onto the Serilog
/// <see cref="LogContext"/> so every log line for the request carries it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
