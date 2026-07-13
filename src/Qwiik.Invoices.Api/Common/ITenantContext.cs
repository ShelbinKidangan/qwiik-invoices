namespace Qwiik.Invoices.Api.Common;

/// <summary>
/// Ambient tenant for the current request. Resolved once by the tenant middleware
/// and consumed by the <c>DbContext</c> to scope every query and stamp writes.
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant. <see cref="Guid.Empty"/> until resolved.</summary>
    Guid TenantId { get; }

    /// <summary><c>true</c> once a tenant has been resolved for this request.</summary>
    bool HasTenant { get; }
}
