namespace Qwiik.Invoices.Api.Common;

/// <summary>
/// Scoped (per-request) tenant holder. The middleware sets the tenant exactly once
/// after resolving it from the request; everything downstream reads it.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public bool HasTenant => TenantId != Guid.Empty;

    /// <summary>
    /// Binds the current request to a tenant. Callable once per request — a second
    /// call throws, so the tenant cannot be silently reassigned mid-request.
    /// </summary>
    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        if (HasTenant)
            throw new InvalidOperationException("The tenant has already been set for this request.");

        TenantId = tenantId;
    }
}
