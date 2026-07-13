using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Infrastructure;

/// <summary>
/// The application's unit of work. EF Core's <see cref="DbContext"/> is used
/// directly — no repository wrapper — per the architecture guardrails.
/// </summary>
public sealed class InvoiceDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceDbContext).Assembly);

        // Tenant isolation by construction: every read of Invoice is scoped to the
        // current tenant. Captures the injected context (not a snapshot value), so
        // the filter resolves the request's tenant each time a query is compiled.
        modelBuilder.Entity<Invoice>().HasQueryFilter(i => i.TenantId == _tenant.TenantId);
    }

    public override int SaveChanges()
    {
        StampTenant();
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps the current tenant onto newly inserted invoices. The tenant is taken
    /// from the request context — never from a request DTO — so it cannot be forged
    /// via mass-assignment. Rows that already carry a tenant are left untouched.
    /// </summary>
    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries<Invoice>())
        {
            if (entry.State == EntityState.Added &&
                (Guid)entry.Property(nameof(Invoice.TenantId)).CurrentValue! == Guid.Empty)
            {
                entry.Property(nameof(Invoice.TenantId)).CurrentValue = _tenant.TenantId;
            }
        }
    }

    /// <summary>
    /// Stamps audit timestamps on tracked invoices: both timestamps on insert,
    /// only <c>UpdatedAtUtc</c> on update. Applied here so no caller can forget it.
    /// </summary>
    private void StampTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Invoice>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(Invoice.CreatedAtUtc)).CurrentValue = now;
                    entry.Property(nameof(Invoice.UpdatedAtUtc)).CurrentValue = now;
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(Invoice.UpdatedAtUtc)).CurrentValue = now;
                    break;
            }
        }
    }
}
