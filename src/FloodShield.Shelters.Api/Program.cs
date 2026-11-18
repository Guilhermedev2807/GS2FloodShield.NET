using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FloodShield.Shelters.Api.Data;
using FloodShield.Shelters.Api.Domain;
using FloodShield.Shelters.Api.Endpoints;
using FloodShield.Shelters.Api.HealthChecks;
using FloodShield.Shelters.Api.Infrastructure;
using FloodShield.Shelters.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

// ---------------- Configuração tipada ----------------
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .PostConfigure(s =>
    {
        if (string.IsNullOrWhiteSpace(s.Key))
        {
            if (builder.Environment.IsProduction())
                throw new InvalidOperationException(
                    "Jwt:Key não configurada. Defina a variável de ambiente Jwt__Key (mín. 32 caracteres).");

            // Dev/Test: chave efêmera, gerada a cada execução (nada fixo no código).
            s.Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }

        if (s.Key.Length < 32)
            throw new InvalidOperationException("Jwt:Key deve ter pelo menos 32 caracteres.");
    });

builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection("RateLimiting"));

// ---------------- Banco de dados (SQL Server) ----------------
if (!isTesting)
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
}

// ---------------- Serviços ----------------
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IShelterService, ShelterService>();
builder.Services.AddHttpClient("java-backend", c => c.Timeout = TimeSpan.FromSeconds(3));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---------------- JWT + autorização ----------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwt) =>
    {
        var s = jwt.Value;
        options.MapInboundClaims = false; // mantém "sub" e "role" como estão no token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = s.Issuer,
            ValidAudience = s.Audience,
            IssuerSigningKey = s.CreateSigningKey(),
            RoleClaimType = "role",
            NameClaimType = "name",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)));

// ---------------- Rate limiting (HTTP 429) ----------------
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>()
    .Configure<IOptions<RateLimitSettings>>((options, settings) =>
    {
        var cfg = settings.Value;
        static string Partition(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(Policies.RateLimitDefault, ctx =>
            RateLimitPartition.GetFixedWindowLimiter(Partition(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = cfg.PermitLimit,
                Window = TimeSpan.FromSeconds(cfg.WindowSeconds),
                QueueLimit = 0
            }));

        // Mais restritivo no login/cadastro (proteção contra força bruta)
        options.AddPolicy(Policies.RateLimitAuth, ctx =>
            RateLimitPartition.GetFixedWindowLimiter(Partition(ctx), _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = cfg.AuthPermitLimit,
                Window = TimeSpan.FromSeconds(cfg.WindowSeconds),
                QueueLimit = 0
            }));

        options.OnRejected = async (context, ct) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Muitas requisições",
                Detail = "Limite de requisições excedido. Tente novamente em instantes."
            }, ct);
        };
    });

// ---------------- Health checks ----------------
builder.Services.AddHealthChecks()
    .AddCheck("api", () => HealthCheckResult.Healthy("API no ar"))
    .AddDbContextCheck<AppDbContext>("database")
    .AddCheck<ExternalApiHealthCheck>("java-backend");

// ---------------- Swagger / OpenAPI ----------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FloodShield Shelters API",
        Version = "v1",
        Description = "API de abrigos e pontos de apoio do FloodShield (CitizenTech). Consumida pelo app Mobile. " +
                      "Fluxo: POST /auth/login -> botão Authorize -> informe o token."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token JWT (sem o prefixo 'Bearer')."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
    c.OperationFilter<IdempotencyHeaderOperationFilter>();
});

var app = builder.Build();

// ---------------- Banco: criação automática + seed ----------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}
if (!isTesting)
    await DbSeeder.SeedAsync(app.Services, app.Environment, app.Configuration, app.Logger);

// ---------------- Pipeline ----------------
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "FloodShield Shelters API v1"));
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapAuthEndpoints();
app.MapShelterEndpoints();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync })
    .WithTags("Health")
    .AllowAnonymous();

app.Run();

// Necessário para WebApplicationFactory<Program> nos testes de integração
public partial class Program { }
