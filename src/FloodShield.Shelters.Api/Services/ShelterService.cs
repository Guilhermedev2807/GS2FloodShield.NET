using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Data;
using FloodShield.Shelters.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FloodShield.Shelters.Api.Services;

public interface IShelterService
{
    Task<PagedResponse<ShelterResponse>> ListAsync(int page, int pageSize, ShelterStatus? status, string? search, CancellationToken ct);
    Task<ShelterResponse?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<NearbyShelterResponse>> NearbyAsync(double lat, double lng, double radiusKm, int limit, bool onlyAvailable, CancellationToken ct);
    Task<ShelterResponse> CreateAsync(CreateShelterRequest request, CancellationToken ct);
    Task<ShelterResponse?> UpdateAsync(int id, UpdateShelterRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task<ShelterResponse?> SetOccupancyAsync(int id, int occupancy, CancellationToken ct);
}

public static class ShelterMapper
{
    public static ShelterResponse ToResponse(this Shelter s) => new(
        s.Id, s.Name, s.Address, s.Latitude, s.Longitude, s.Capacity, s.CurrentOccupancy,
        s.AvailableSpots, s.Status, s.Phone, s.CreatedAt);
}

public sealed class ShelterService(AppDbContext db) : IShelterService
{
    public async Task<PagedResponse<ShelterResponse>> ListAsync(
        int page, int pageSize, ShelterStatus? status, string? search, CancellationToken ct)
    {
        var query = db.Shelters.AsNoTracking();

        if (status.HasValue)
        {
            var st = status.Value;
            query = query.Where(s => s.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.Contains(term) || s.Address.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResponse<ShelterResponse>(items.Select(s => s.ToResponse()).ToList(), page, pageSize, total);
    }

    public async Task<ShelterResponse?> GetAsync(int id, CancellationToken ct)
        => (await db.Shelters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct))?.ToResponse();

    public async Task<IReadOnlyList<NearbyShelterResponse>> NearbyAsync(
        double lat, double lng, double radiusKm, int limit, bool onlyAvailable, CancellationToken ct)
    {
        var (minLat, maxLat, minLng, maxLng) = GeoCalculator.BoundingBox(lat, lng, radiusKm);

        var query = db.Shelters.AsNoTracking().Where(s =>
            s.Latitude >= minLat && s.Latitude <= maxLat &&
            s.Longitude >= minLng && s.Longitude <= maxLng);

        if (onlyAvailable)
            query = query.Where(s => s.Status == ShelterStatus.Open);

        var candidates = await query.ToListAsync(ct);

        return candidates
            .Select(s => new NearbyShelterResponse(
                s.ToResponse(),
                Math.Round(GeoCalculator.DistanceKm(lat, lng, s.Latitude, s.Longitude), 2)))
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .Take(limit)
            .ToList();
    }

    public async Task<ShelterResponse> CreateAsync(CreateShelterRequest r, CancellationToken ct)
    {
        var shelter = Shelter.Create(r.Name, r.Address, r.Latitude, r.Longitude, r.Capacity, r.Phone);
        db.Shelters.Add(shelter);
        await db.SaveChangesAsync(ct);
        return shelter.ToResponse();
    }

    public async Task<ShelterResponse?> UpdateAsync(int id, UpdateShelterRequest r, CancellationToken ct)
    {
        var shelter = await db.Shelters.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shelter is null) return null;

        shelter.Update(r.Name, r.Address, r.Latitude, r.Longitude, r.Capacity, r.Phone, r.Closed);
        await db.SaveChangesAsync(ct);
        return shelter.ToResponse();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var shelter = await db.Shelters.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shelter is null) return false;

        db.Shelters.Remove(shelter);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ShelterResponse?> SetOccupancyAsync(int id, int occupancy, CancellationToken ct)
    {
        var shelter = await db.Shelters.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (shelter is null) return null;

        shelter.SetOccupancy(occupancy);
        await db.SaveChangesAsync(ct);
        return shelter.ToResponse();
    }
}
