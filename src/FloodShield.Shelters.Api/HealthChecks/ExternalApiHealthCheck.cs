using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FloodShield.Shelters.Api.HealthChecks;

/// <summary>Verifica o backend Java da equipe (serviço externo). Se estiver fora, a API fica "Degraded" (HTTP 200).</summary>
public sealed class ExternalApiHealthCheck(IHttpClientFactory httpFactory, IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var url = config["ExternalServices:JavaBackendHealthUrl"];
        if (string.IsNullOrWhiteSpace(url))
            return HealthCheckResult.Healthy("Serviço externo não configurado (ignorado).");

        try
        {
            var client = httpFactory.CreateClient("java-backend");
            using var response = await client.GetAsync(url, ct);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Backend Java respondeu HTTP {(int)response.StatusCode}.")
                : HealthCheckResult.Degraded($"Backend Java respondeu HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Backend Java indisponível.", ex);
        }
    }
}
