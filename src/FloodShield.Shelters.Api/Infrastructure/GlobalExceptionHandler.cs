using FloodShield.Shelters.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FloodShield.Shelters.Api.Infrastructure;

/// <summary>Regra de negócio violada => 422; qualquer outra exceção => 500 sem vazar detalhes.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var isDomain = exception is DomainException;
        if (!isDomain)
            logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);

        context.Response.StatusCode = isDomain ? StatusCodes.Status422UnprocessableEntity : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = isDomain ? "Regra de negócio violada" : "Erro interno do servidor",
            Detail = isDomain ? exception.Message : null
        }, ct);
        return true;
    }
}
