using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qwiik.Invoices.Api.Domain;

namespace Qwiik.Invoices.Api.Infrastructure.Configurations;

public sealed class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.HasKey(li => li.Id);

        builder.Property(li => li.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(li => li.Quantity).HasColumnType("decimal(18,2)");
        builder.Property(li => li.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(li => li.TaxRate).HasColumnType("decimal(18,2)");

        // LineTotal is derived from the other columns — computed in the domain,
        // never persisted.
        builder.Ignore(li => li.LineTotal);
    }
}
