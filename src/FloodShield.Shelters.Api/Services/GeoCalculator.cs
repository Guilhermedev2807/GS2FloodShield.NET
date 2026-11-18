namespace FloodShield.Shelters.Api.Services;

public static class GeoCalculator
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Distância em km entre dois pontos (fórmula de Haversine).</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Caixa delimitadora aproximada, usada para pré-filtrar no banco antes do Haversine.</summary>
    public static (double MinLat, double MaxLat, double MinLng, double MaxLng) BoundingBox(
        double lat, double lng, double radiusKm)
    {
        var dLat = radiusKm / 111.32;
        var cos = Math.Cos(ToRad(lat));
        var dLng = cos < 1e-6 ? 180 : radiusKm / (111.32 * cos);
        return (Math.Max(-90, lat - dLat), Math.Min(90, lat + dLat),
                Math.Max(-180, lng - dLng), Math.Min(180, lng + dLng));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
