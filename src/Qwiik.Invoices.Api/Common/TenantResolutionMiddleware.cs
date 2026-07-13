using Microsoft.AspNetCore.Mvc;

namespace Qwiik.Invoices.Api.Common;

/// <summary>
/// Resolves the current tenant from the <c>X-Tenant-Id</c> request header and binds
/// it to the scoped <see cref="TenantContext"/>. Requests without a valid tenant are
/// rejected before they can touch any tenant-owned data.
/// </summary>
/// <remarks>
/// The header is a stand-in for a real identity source — in production the tenant
/// would come from a validated JWT claim, not a client-supplied header.
/// </remarks>
public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";

    // Documentation and health surfaces that must be reachable without a tenant.
    // Root ("/") is matched exactly; the rest match by leading path segment.
    private static readonly string[] AnonymousPaths = ["/scalar", "/openapi", "/health"];

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        if (IsAnonymousPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers[TenantHeader].ToString();
        if (!Guid.TryParse(header, out var tenantId) || tenantId == Guid.Empty)
        {
            await WriteMissingTenantAsync(context);
            return;
        }

        tenant.SetTenant(tenantId);
        await _next(context);
    }

    private static bool IsAnonymousPath(PathString path)
    {
        if (!path.HasValue || path == "/")
            return true;

        foreach (var anonymous in AnonymousPaths)
        {
            if (path.StartsWithSegments(anonymous, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task WriteMissingTenantAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Missing tenant",
            Detail = "A valid X-Tenant-Id header is required.",
            Instance = context.Request.Path
        };

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
