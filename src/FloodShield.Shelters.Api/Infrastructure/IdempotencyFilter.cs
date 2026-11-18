using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Data;
using FloodShield.Shelters.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FloodShield.Shelters.Api.Infrastructure;

/// <summary>Marca o endpoint como idempotente (usado pelo Swagger para exibir o header Idempotency-Key).</summary>
public sealed record IdempotentEndpointMetadata;

/// <summary>
/// Idempotência via header "Idempotency-Key":
///  - chave nova            => executa e guarda status + corpo da resposta;
///  - mesma chave/payload   => devolve a resposta guardada, sem executar de novo;
///  - mesma chave/payload diferente => 422.
/// Apenas respostas 2xx são guardadas (erros podem ser repetidos após correção).
/// O lock em memória cobre concorrência numa instância; entre instâncias vale o índice único (Key, UserId).
/// </summary>
public sealed class IdempotencyFilter : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;

        var key = http.Request.Headers[HeaderName].ToString().Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 100)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key ausente ou inválido",
                detail: $"Envie o header '{HeaderName}' (ex.: um GUID, até 100 caracteres).");

        var userId = http.User.FindFirst("sub")?.Value ?? "anonymous";
        var jsonOptions = http.RequestServices.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var hash = ComputeHash(http.Request.Method, http.Request.Path.Value ?? "", context.Arguments, jsonOptions);
        var db = http.RequestServices.GetRequiredService<AppDbContext>();

        var gate = Locks.GetOrAdd($"{userId}:{key}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var existing = await db.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == key && r.UserId == userId, ct);

            if (existing is not null)
            {
                if (existing.RequestHash != hash)
                    return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
                        title: "Idempotency-Key reutilizada com outro payload",
                        detail: "Esta chave já foi usada em uma requisição diferente. Gere uma nova chave.");

                http.Response.Headers["Idempotent-Replayed"] = "true";
                return new RawJsonResult(existing.StatusCode, existing.ResponseBody);
            }

            var result = await next(context);

            if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } sc)
            {
                var body = result is IValueHttpResult { Value: not null } vr
                    ? JsonSerializer.Serialize(vr.Value, vr.Value.GetType(), jsonOptions)
                    : "";

                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = key,
                    UserId = userId,
                    RequestHash = hash,
                    StatusCode = sc.StatusCode!.Value,
                    ResponseBody = body
                });

                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException) { /* corrida entre instâncias: o índice único garante um único registro */ }
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string ComputeHash(string method, string path, IList<object?> arguments, JsonSerializerOptions options)
    {
        var payload = arguments.OfType<IRequestDto>()
            .Select(a => JsonSerializer.Serialize(a, a.GetType(), options));
        var raw = $"{method}|{path}|{string.Join("|", payload)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private sealed class RawJsonResult(int statusCode, string json) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            return httpContext.Response.WriteAsync(json);
        }
    }
}

public static class IdempotencyExtensions
{
    public static RouteHandlerBuilder RequireIdempotency(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<IdempotencyFilter>().WithMetadata(new IdempotentEndpointMetadata());
}
