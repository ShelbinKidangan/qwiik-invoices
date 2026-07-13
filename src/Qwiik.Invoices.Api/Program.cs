using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// One tenant context per request: the middleware writes it, the DbContext reads it.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// DbContext is scoped by default, so it resolves the same per-request TenantContext.
builder.Services.AddDbContext<InvoiceDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapGet("/", () => Results.Redirect("/scalar"));
}

app.UseHttpsRedirection();

// Resolve and enforce the tenant before any endpoint can touch tenant-owned data.
app.UseMiddleware<TenantResolutionMiddleware>();

app.Run();
