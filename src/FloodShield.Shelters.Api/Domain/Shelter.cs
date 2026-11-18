namespace FloodShield.Shelters.Api.Domain;

public enum ShelterStatus { Open, Full, Closed }

public sealed class DomainException(string message) : Exception(message);

public class Shelter
{
    private Shelter() { } // EF Core

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int Capacity { get; private set; }
    public int CurrentOccupancy { get; private set; }
    public ShelterStatus Status { get; private set; }
    public string? Phone { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public int AvailableSpots => Math.Max(0, Capacity - CurrentOccupancy);

    public static Shelter Create(string name, string address, double latitude, double longitude,
        int capacity, string? phone, int currentOccupancy = 0)
    {
        var shelter = new Shelter { CreatedAt = DateTime.UtcNow };
        shelter.Apply(name, address, latitude, longitude, capacity, phone, currentOccupancy, closed: false);
        return shelter;
    }

    public void Update(string name, string address, double latitude, double longitude,
        int capacity, string? phone, bool closed)
        => Apply(name, address, latitude, longitude, capacity, phone, CurrentOccupancy, closed);

    /// <summary>Atualiza a ocupação respeitando a capacidade. Lotou => Full; liberou vaga => Open.</summary>
    public void SetOccupancy(int occupancy)
    {
        if (occupancy < 0)
            throw new DomainException("A ocupação não pode ser negativa.");
        if (occupancy > Capacity)
            throw new DomainException("A ocupação não pode ser maior que a capacidade.");

        CurrentOccupancy = occupancy;
        if (Status != ShelterStatus.Closed)
            Status = occupancy >= Capacity ? ShelterStatus.Full : ShelterStatus.Open;
    }

    private void Apply(string name, string address, double latitude, double longitude,
        int capacity, string? phone, int occupancy, bool closed)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do abrigo é obrigatório.");
        if (string.IsNullOrWhiteSpace(address))
            throw new DomainException("O endereço do abrigo é obrigatório.");
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
            throw new DomainException("Latitude deve estar entre -90 e 90.");
        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
            throw new DomainException("Longitude deve estar entre -180 e 180.");
        if (capacity <= 0)
            throw new DomainException("A capacidade deve ser maior que zero.");
        if (occupancy < 0)
            throw new DomainException("A ocupação não pode ser negativa.");
        if (occupancy > capacity)
            throw new DomainException("A ocupação não pode ser maior que a capacidade.");

        Name = name.Trim();
        Address = address.Trim();
        Latitude = latitude;
        Longitude = longitude;
        Capacity = capacity;
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        CurrentOccupancy = occupancy;
        Status = closed ? ShelterStatus.Closed
               : occupancy >= capacity ? ShelterStatus.Full
               : ShelterStatus.Open;
    }
}
