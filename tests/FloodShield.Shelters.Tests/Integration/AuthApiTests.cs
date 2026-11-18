using System.Net;
using System.Net.Http.Json;
using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Domain;
using FloodShield.Shelters.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FloodShield.Shelters.Tests.Integration;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static RegisterRequest NewUser(string? email = null, string password = "Senha@1234") => new()
    {
        Name = "Maria Teste",
        Email = email ?? $"user-{Guid.NewGuid():N}@test.local",
        Password = password
    };

    [Fact]
    public async Task Register_WithValidData_Returns201AsCitizen()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", NewUser(), TestJson.Options);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options);
        Assert.Equal(UserRole.Citizen, body!.Role);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var client = factory.CreateClient();
        var user = NewUser();

        await client.PostAsJsonAsync("/auth/register", user, TestJson.Options);
        var second = await client.PostAsJsonAsync("/auth/register", user, TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register", NewUser(password: "123"), TestJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndToken()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await client.PostAsJsonAsync("/auth/register", user, TestJson.Options);

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = user.Email, Password = user.Password }, TestJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestJson.Options);
        Assert.False(string.IsNullOrWhiteSpace(token!.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = factory.CreateClient();
        var user = NewUser();
        await client.PostAsJsonAsync("/auth/register", user, TestJson.Options);

        var response = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Email = user.Email, Password = "errada-123" }, TestJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ExceedingRateLimit_Returns429WithRetryAfter()
    {
        using var limited = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.Configure<RateLimitSettings>(s => s.AuthPermitLimit = 3)));
        var client = limited.CreateClient();
        var attempt = new LoginRequest { Email = "ninguem@test.local", Password = "qualquer-123" };

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.PostAsJsonAsync("/auth/login", attempt, TestJson.Options);
            Assert.Equal(HttpStatusCode.Unauthorized, ok.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/auth/login", attempt, TestJson.Options);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
    }
}
