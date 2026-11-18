using FloodShield.Shelters.Api.Domain;

namespace FloodShield.Shelters.Tests.Unit;

public class ShelterTests
{
    private static Shelter NewShelter(int capacity = 10, int occupancy = 0)
        => Shelter.Create("Abrigo Teste", "Rua A, 1", -23.69, -46.56, capacity, "(11) 99999-0000", occupancy);

    [Fact]
    public void Create_WithValidData_StartsOpen()
    {
        var shelter = NewShelter();

        Assert.Equal(ShelterStatus.Open, shelter.Status);
        Assert.Equal(10, shelter.AvailableSpots);
    }

    [Fact]
    public void Create_WithOccupancyEqualToCapacity_IsFull()
        => Assert.Equal(ShelterStatus.Full, NewShelter(capacity: 5, occupancy: 5).Status);

    [Theory]
    [InlineData(91.0, 0.0)]
    [InlineData(-91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    [InlineData(0.0, -181.0)]
    public void Create_WithInvalidCoordinates_Throws(double lat, double lng)
        => Assert.Throws<DomainException>(() => Shelter.Create("A", "B", lat, lng, 10, null));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositiveCapacity_Throws(int capacity)
        => Assert.Throws<DomainException>(() => Shelter.Create("A", "B", 0, 0, capacity, null));

    [Fact]
    public void Create_WithBlankName_Throws()
        => Assert.Throws<DomainException>(() => Shelter.Create("  ", "B", 0, 0, 10, null));

    [Fact]
    public void SetOccupancy_AboveCapacity_Throws()
    {
        var shelter = NewShelter(capacity: 10);
        Assert.Throws<DomainException>(() => shelter.SetOccupancy(11));
    }

    [Fact]
    public void SetOccupancy_Negative_Throws()
        => Assert.Throws<DomainException>(() => NewShelter().SetOccupancy(-1));

    [Fact]
    public void SetOccupancy_ReachingCapacity_BecomesFull()
    {
        var shelter = NewShelter(capacity: 10);
        shelter.SetOccupancy(10);

        Assert.Equal(ShelterStatus.Full, shelter.Status);
        Assert.Equal(0, shelter.AvailableSpots);
    }

    [Fact]
    public void SetOccupancy_FreeingSpot_ReopensShelter()
    {
        var shelter = NewShelter(capacity: 10, occupancy: 10);
        shelter.SetOccupancy(9);

        Assert.Equal(ShelterStatus.Open, shelter.Status);
    }

    [Fact]
    public void SetOccupancy_OnClosedShelter_KeepsItClosed()
    {
        var shelter = NewShelter(capacity: 10);
        shelter.Update("Abrigo Teste", "Rua A, 1", -23.69, -46.56, 10, null, closed: true);
        shelter.SetOccupancy(3);

        Assert.Equal(ShelterStatus.Closed, shelter.Status);
    }

    [Fact]
    public void Update_ReducingCapacityBelowOccupancy_Throws()
    {
        var shelter = NewShelter(capacity: 10, occupancy: 8);
        Assert.Throws<DomainException>(() =>
            shelter.Update("Abrigo Teste", "Rua A, 1", -23.69, -46.56, 5, null, closed: false));
    }
}
