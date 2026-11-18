using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FloodShield.Shelters.Api.Infrastructure;

public sealed class IdempotencyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var idempotent = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IdempotentEndpointMetadata>().Any();
        if (!idempotent) return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyFilter.HeaderName,
            In = ParameterLocation.Header,
            Required = true,
            Description = "Chave única da operação (ex.: GUID). Repetir a mesma chave com o mesmo corpo devolve a resposta original sem duplicar.",
            Schema = new OpenApiSchema { Type = "string", MaxLength = 100 }
        });
    }
}
