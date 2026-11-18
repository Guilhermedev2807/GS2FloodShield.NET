using System.ComponentModel.DataAnnotations;
using FloodShield.Shelters.Api.Domain;

namespace FloodShield.Shelters.Api.Contracts;

/// <summary>Marcador de DTOs de entrada (usado pelo filtro de idempotência para calcular o hash do payload).</summary>
public interface IRequestDto;

// ---------- Auth ----------
public sealed class RegisterRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string Name, string Email, UserRole Role);
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAt, UserRole Role);

// ---------- Shelters ----------
public class ShelterInput : IRequestDto
{
    [Required, StringLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(250)]
    public string Address { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(1, 100000)]
    public int Capacity { get; init; }

    [StringLength(30)]
    public string? Phone { get; init; }
}

public sealed class CreateShelterRequest : ShelterInput;

public sealed class UpdateShelterRequest : ShelterInput
{
    /// <summary>true = abrigo fechado manualmente.</summary>
    public bool Closed { get; init; }
}

public sealed class OccupancyRequest : IRequestDto
{
    [Range(0, 100000)]
    public int CurrentOccupancy { get; init; }
}

public sealed record ShelterResponse(
    int Id, string Name, string Address, double Latitude, double Longitude,
    int Capacity, int CurrentOccupancy, int AvailableSpots, ShelterStatus Status,
    string? Phone, DateTime CreatedAt);

public sealed record NearbyShelterResponse(ShelterResponse Shelter, double DistanceKm);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
