using FloodShield.Shelters.Api.Services;

namespace FloodShield.Shelters.Tests.Unit;

public class GeoCalculatorTests
{
    [Fact]
    public void DistanceKm_SamePoint_IsZero()
        => Assert.Equal(0, GeoCalculator.DistanceKm(-23.55, -46.63, -23.55, -46.63), 6);

    [Fact]
    public void DistanceKm_SaoPauloToRio_IsAround360Km()
    {
        var km = GeoCalculator.DistanceKm(-23.5505, -46.6333, -22.9068, -43.1729);
        Assert.InRange(km, 350, 370);
    }

    [Fact]
    public void DistanceKm_IsSymmetric()
    {
        var a = GeoCalculator.DistanceKm(-23.55, -46.63, -22.90, -43.17);
        var b = GeoCalculator.DistanceKm(-22.90, -43.17, -23.55, -46.63);
        Assert.Equal(a, b, 6);
    }

    [Fact]
    public void BoundingBox_ContainsPointsInsideRadius()
    {
        var (minLat, maxLat, minLng, maxLng) = GeoCalculator.BoundingBox(-23.55, -46.63, 5);
        // ponto a ~3 km ao norte
        Assert.InRange(-23.523, minLat, maxLat);
        Assert.InRange(-46.63, minLng, maxLng);
    }
}
