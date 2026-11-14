using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MoscoRun.Api.Hosting;

/// <summary>Descrive nel contratto OpenAPI i due modi di autenticazione: cookie per il web, token per gli agenti.</summary>
internal sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Info = new OpenApiInfo
        {
            Title = "MoscoRun API",
            Version = "v1",
            Description = "API dell'assistente per allenamenti e forma fisica MoscoRun.",
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["cookie"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = ".MoscoRun.Session",
            Description = "Sessione del cruscotto web, ottenuta con POST /api/auth/login.",
        };
        document.Components.SecuritySchemes["deviceToken"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Token di un dispositivo (mr1_...), creato dal cruscotto e mostrato una sola volta.",
        };
        return Task.CompletedTask;
    }
}
