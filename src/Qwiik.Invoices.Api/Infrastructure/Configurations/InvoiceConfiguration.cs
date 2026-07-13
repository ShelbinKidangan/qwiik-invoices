using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Infrastructure.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(i => i.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.CustomerEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(i => i.Notes)
            .HasMaxLength(1000);

        builder.Property(i => i.Subtotal).HasColumnType("decimal(18,2)");
        builder.Property(i => i.TaxTotal).HasColumnType("decimal(18,2)");
        builder.Property(i => i.Total).HasColumnType("decimal(18,2)");

        builder.Property(i => i.RowVersion).IsRowVersion();

        // The aggregate owns its line items through the private _lineItems field;
        // there is no back-navigation or explicit FK property on the line item,
        // so EF introduces a shadow "InvoiceId" foreign key on InvoiceLineItem.
        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey("InvoiceId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.LineItems)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Every query is tenant-scoped, so composite indexes lead with TenantId.
        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.IssueDate });
        builder.HasIndex(i => new { i.TenantId, i.InvoiceNumber }).IsUnique();
    }
}
