using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Infrastructure;

/// <summary>
/// The application's unit of work. EF Core's <see cref="DbContext"/> is used
/// directly — no repository wrapper — per the architecture guardrails.
/// </summary>
public sealed class InvoiceDbContext : DbContext
{
    public InvoiceDbContext(DbContextOptions<InvoiceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
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
