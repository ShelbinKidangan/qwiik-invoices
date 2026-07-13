using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Qwiik.Invoices.Api.Common;
using Qwiik.Invoices.Api.Features.Invoices;
using Qwiik.Invoices.Api.Features.Invoices.Dtos;
using Qwiik.Invoices.Api.Features.Invoices.Validation;
using Qwiik.Invoices.Api.Infrastructure;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Readable console in dev, JSON in production; every line enriched from the LogContext.
builder.Host.UseSerilog((context, config) =>
{
    config
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext();

    if (context.HostingEnvironment.IsDevelopment())
        config.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} " +
            "{{CorrelationId={CorrelationId}, TenantId={TenantId}}}{NewLine}{Exception}");
    else
        config.WriteTo.Console(new JsonFormatter(renderMessage: true));
});

builder.Services.AddOpenApi(o => o.AddDocumentTransformer<TenantHeaderDocumentTransformer>());

// RFC 7807 for all error responses; traceId correlates a client error to server logs.
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();

// FluentValidation owns request validation, so silence the [ApiController]
// automatic model-state 400 and let the controller produce the ProblemDetails.
builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);

builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceValidator>();

// Liveness has no checks (app-is-up only); readiness adds the DB check, tagged "ready".
builder.Services.AddHealthChecks()
    .AddDbContextCheck<InvoiceDbContext>("database", tags: ["ready"]);

// One tenant context per request: the middleware writes it, the DbContext reads it.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

// DbContext is scoped by default, so it resolves the same per-request TenantContext.
builder.Services.AddDbContext<InvoiceDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Outermost, so the correlation id is on the LogContext before the exception handler
// or Serilog's request log writes any line for this request.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapGet("/", () => Results.Redirect("/scalar"));
}

app.UseHttpsRedirection();

// Resolve and enforce the tenant before any endpoint can touch tenant-owned data.
app.UseMiddleware<TenantResolutionMiddleware>();

// Liveness runs no checks; readiness runs the "ready"-tagged DB check.
app.MapHealthChecks("/health", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });

app.MapControllers();

app.Run();

// Exposes the implicit Program class so the integration test project can bind
// WebApplicationFactory<Program> to this application's real pipeline.
public partial class Program;
