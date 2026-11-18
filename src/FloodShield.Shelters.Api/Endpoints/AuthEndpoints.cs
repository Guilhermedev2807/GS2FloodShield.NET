using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Data;
using FloodShield.Shelters.Api.Domain;
using FloodShield.Shelters.Api.Infrastructure;
using FloodShield.Shelters.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FloodShield.Shelters.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(Policies.RateLimitAuth);

        group.MapPost("/register", Register)
            .WithSummary("Cadastra um cidadão (role Citizen)")
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", Login)
            .WithSummary("Autentica e devolve um token JWT")
            .Produces<TokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, AppDbContext db, IPasswordHasher<User> hasher, CancellationToken ct)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "E-mail já cadastrado");

        var user = new User { Name = request.Name.Trim(), Email = email, Role = UserRole.Citizen };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Results.Json(new UserResponse(user.Id, user.Name, user.Email, user.Role),
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(
        LoginRequest request, AppDbContext db, IPasswordHasher<User> hasher, ITokenService tokens, CancellationToken ct)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null ||
            hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credenciais inválidas");

        var (token, expiresAt) = tokens.Create(user);
        return Results.Ok(new TokenResponse(token, expiresAt, user.Role));
    }
}
