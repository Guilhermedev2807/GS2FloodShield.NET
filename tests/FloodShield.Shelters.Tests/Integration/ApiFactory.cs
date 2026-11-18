using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Data;
using FloodShield.Shelters.Api.Domain;
using FloodShield.Shelters.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FloodShield.Shelters.Tests.Integration;

public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>API em memória: ambiente "Testing" => Program não registra o SQL Server; usamos EF InMemory.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test@12345";
    private readonly string _dbName = $"floodshield-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName)));

        // Limites altos por padrão para não interferir nos demais testes.
        builder.ConfigureTestServices(services =>
            services.Configure<RateLimitSettings>(s => { s.PermitLimit = 10_000; s.AuthPermitLimit = 10_000; }));
    }

    /// <summary>Cria um usuário no banco com o papel informado, faz login pela API e devolve um client já autenticado.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        var email = $"{role}-{Guid.NewGuid():N}@test.local".ToLowerInvariant();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var user = new User { Name = $"Teste {role}", Email = email, Role = role };
            user.PasswordHash = hasher.HashPassword(user, Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = email, Password = Password }, TestJson.Options);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    public static HttpRequestMessage Json<T>(HttpMethod method, string url, T body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: TestJson.Options) };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }
}
