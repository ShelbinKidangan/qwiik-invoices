using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Features.Invoices;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;
using Qwiik.Invoices.Api.Features.Invoices.Validation;
using Qwiik.Invoices.Api.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<TenantHeaderDocumentTransformer>());

// RFC 7807 for all error responses, no leaked stack traces.
builder.Services.AddProblemDetails();

builder.Services.AddControllers();

// FluentValidation owns request validation, so silence the [ApiController]
// automatic model-state 400 and let the controller produce the ProblemDetails.
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);

builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceValidator>();

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

app.MapControllers();

app.Run();
