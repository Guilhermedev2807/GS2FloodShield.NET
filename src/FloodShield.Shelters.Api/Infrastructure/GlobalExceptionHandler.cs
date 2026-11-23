using FloodShield.Shelters.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FloodShield.Shelters.Api.Infrastructure;

/// <summary>Regra de negócio => 422; requisição malformada => 400; qualquer outra exceção => 500.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        int status;
        string title;
        string? detail = null;

        switch (exception)
        {
            case DomainException:
                status = StatusCodes.Status422UnprocessableEntity;
                title = "Regra de negócio violada";
                detail = exception.Message;
                break;
            case BadHttpRequestException bad:
                status = bad.StatusCode;
                title = "Requisição inválida";
                detail = "O corpo ou os parâmetros da requisição estão malformados (verifique o JSON).";
                break;
            default:
                logger.LogError(exception, "Erro não tratado em {Path}", context.Request.Path);
                status = StatusCodes.Status500InternalServerError;
                title = "Erro interno do servidor";
                break;
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        }, ct);
        return true;
    }
}