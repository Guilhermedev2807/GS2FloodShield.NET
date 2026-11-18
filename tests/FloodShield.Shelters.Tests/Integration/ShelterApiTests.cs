using System.Net;
using System.Net.Http.Json;
using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Domain;

namespace FloodShield.Shelters.Tests.Integration;

public class ShelterApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static CreateShelterRequest NewShelter(string? name = null, double lat = -23.69, double lng = -46.56, int capacity = 10) => new()
    {
        Name = name ?? $"Abrigo {Guid.NewGuid():N}",
        Address = "Rua de Teste, 123",
        Latitude = lat,
        Longitude = lng,
        Capacity = capacity
    };

    private static Task<HttpResponseMessage> PostShelter(HttpClient client, CreateShelterRequest body, string? key = null)
        => client.SendAsync(ApiFactory.Json(HttpMethod.Post, "/shelters", body, key ?? Guid.NewGuid().ToString()));

    private static async Task<ShelterResponse> Created(HttpClient admin, CreateShelterRequest body)
    {
        var response = await PostShelter(admin, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShelterResponse>(TestJson.Options))!;
    }

    // ---------- Segurança ----------
    [Fact]
    public async Task GetShelters_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/shelters");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetShelters_AsCitizen_Returns200()
    {
        var citizen = await factory.CreateAuthenticatedClientAsync(UserRole.Citizen);

        var response = await citizen.GetAsync("/shelters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateShelter_AsCitizen_Returns403()
    {
        var citizen = await factory.CreateAuthenticatedClientAsync(UserRole.Citizen);

        var response = await PostShelter(citizen, NewShelter());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsCitizen_Returns403()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var citizen = await factory.CreateAuthenticatedClientAsync(UserRole.Citizen);
        var shelter = await Created(admin, NewShelter());

        var response = await citizen.DeleteAsync($"/shelters/{shelter.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- CRUD ----------
    [Fact]
    public async Task CreateShelter_AsAdmin_Returns201WithLocation()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await PostShelter(admin, NewShelter());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<ShelterResponse>(TestJson.Options);
        Assert.Equal(ShelterStatus.Open, body!.Status);
    }

    [Fact]
    public async Task CreateShelter_WithInvalidPayload_Returns400()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await PostShelter(admin, NewShelter(lat: 200));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await admin.GetAsync("/shelters/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_Returns200AndPersistsChanges()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var shelter = await Created(admin, NewShelter());

        var update = new UpdateShelterRequest
        {
            Name = "Nome Atualizado", Address = "Av. Nova, 10", Latitude = -23.7, Longitude = -46.6, Capacity = 50, Closed = true
        };
        var response = await admin.SendAsync(ApiFactory.Json(HttpMethod.Put, $"/shelters/{shelter.Id}", update));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShelterResponse>(TestJson.Options);
        Assert.Equal("Nome Atualizado", body!.Name);
        Assert.Equal(ShelterStatus.Closed, body.Status);
    }

    [Fact]
    public async Task Delete_AsAdmin_Returns204ThenGetReturns404()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var shelter = await Created(admin, NewShelter());

        var delete = await admin.DeleteAsync($"/shelters/{shelter.Id}");
        var get = await admin.GetAsync($"/shelters/{shelter.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task List_WithInvalidPageSize_Returns400()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await admin.GetAsync("/shelters?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Idempotência ----------
    [Fact]
    public async Task CreateShelter_WithoutIdempotencyKey_Returns400()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await admin.SendAsync(ApiFactory.Json(HttpMethod.Post, "/shelters", NewShelter()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateShelter_RepeatedWithSameKey_DoesNotDuplicate()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var body = NewShelter();
        var key = Guid.NewGuid().ToString();

        var first = await PostShelter(admin, body, key);
        var second = await PostShelter(admin, body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));

        var a = await first.Content.ReadFromJsonAsync<ShelterResponse>(TestJson.Options);
        var b = await second.Content.ReadFromJsonAsync<ShelterResponse>(TestJson.Options);
        Assert.Equal(a!.Id, b!.Id);

        var list = await admin.GetFromJsonAsync<PagedResponse<ShelterResponse>>(
            $"/shelters?search={body.Name}", TestJson.Options);
        Assert.Equal(1, list!.TotalCount);
    }

    [Fact]
    public async Task CreateShelter_SameKeyWithDifferentPayload_Returns422()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var key = Guid.NewGuid().ToString();

        await PostShelter(admin, NewShelter(), key);
        var conflict = await PostShelter(admin, NewShelter(), key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode);
    }

    // ---------- Ocupação (regra de negócio via API) ----------
    [Fact]
    public async Task Occupancy_ReachingCapacity_SetsStatusFull()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var shelter = await Created(admin, NewShelter(capacity: 10));

        var response = await admin.SendAsync(ApiFactory.Json(HttpMethod.Patch,
            $"/shelters/{shelter.Id}/occupancy", new OccupancyRequest { CurrentOccupancy = 10 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShelterResponse>(TestJson.Options);
        Assert.Equal(ShelterStatus.Full, body!.Status);
        Assert.Equal(0, body.AvailableSpots);
    }

    [Fact]
    public async Task Occupancy_AboveCapacity_Returns422()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var shelter = await Created(admin, NewShelter(capacity: 10));

        var response = await admin.SendAsync(ApiFactory.Json(HttpMethod.Patch,
            $"/shelters/{shelter.Id}/occupancy", new OccupancyRequest { CurrentOccupancy = 11 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ---------- Nearby ----------
    [Fact]
    public async Task Nearby_ReturnsOnlySheltersInRadiusOrderedByDistance()
    {
        var admin = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var tag = Guid.NewGuid().ToString("N")[..6];
        // Coordenadas isoladas (região de Manaus) para não misturar com outros testes
        var near = await Created(admin, NewShelter($"Perto {tag}", lat: -3.1000, lng: -60.0));
        var mid = await Created(admin, NewShelter($"Medio {tag}", lat: -3.1300, lng: -60.0));
        await Created(admin, NewShelter($"Longe {tag}", lat: -3.5000, lng: -60.0));

        var result = await admin.GetFromJsonAsync<List<NearbyShelterResponse>>(
            "/shelters/nearby?lat=-3.1&lng=-60.0&radiusKm=10", TestJson.Options);

        Assert.Equal(new[] { near.Id, mid.Id }, result!.Select(r => r.Shelter.Id).ToArray());
        Assert.True(result[0].DistanceKm < result[1].DistanceKm);
    }

    [Fact]
    public async Task Nearby_WithInvalidCoordinates_Returns400()
    {
        var citizen = await factory.CreateAuthenticatedClientAsync(UserRole.Citizen);

        var response = await citizen.GetAsync("/shelters/nearby?lat=999&lng=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Health ----------
    [Fact]
    public async Task Health_IsPublicAndReportsDatabase()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("database", json);
        Assert.Contains("java-backend", json);
    }
}
