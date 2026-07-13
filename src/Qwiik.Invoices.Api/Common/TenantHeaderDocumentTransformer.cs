using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Qwiik.Invoices.Api.Common;

// Advertises X-Tenant-Id as a global API-key security scheme so the API reference UI
// (Scalar) offers one place to enter the tenant and applies it to every request.
public sealed class TenantHeaderDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "TenantId";
    private const string HeaderName = "X-Tenant-Id";

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = HeaderName,
            Description = "Tenant GUID sent as the X-Tenant-Id header on every request."
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document, null)] = new List<string>()
        });

        return Task.CompletedTask;
    }
}
