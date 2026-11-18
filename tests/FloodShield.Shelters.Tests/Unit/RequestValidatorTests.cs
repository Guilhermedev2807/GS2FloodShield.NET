using FloodShield.Shelters.Api.Contracts;
using FloodShield.Shelters.Api.Infrastructure;

namespace FloodShield.Shelters.Tests.Unit;

public class RequestValidatorTests
{
    [Fact]
    public void ValidShelter_Passes()
    {
        var ok = RequestValidator.TryValidate(
            new CreateShelterRequest { Name = "A", Address = "B", Latitude = -23, Longitude = -46, Capacity = 10 },
            out var errors);

        Assert.True(ok);
        Assert.Empty(errors);
    }

    [Fact]
    public void InvalidShelter_ReportsEachField()
    {
        var ok = RequestValidator.TryValidate(
            new CreateShelterRequest { Name = "", Address = "B", Latitude = 200, Longitude = -46, Capacity = 0 },
            out var errors);

        Assert.False(ok);
        Assert.Contains("Name", errors.Keys);
        Assert.Contains("Latitude", errors.Keys);
        Assert.Contains("Capacity", errors.Keys);
    }

    [Fact]
    public void Register_WithShortPasswordAndBadEmail_Fails()
    {
        var ok = RequestValidator.TryValidate(
            new RegisterRequest { Name = "Ana", Email = "nao-e-email", Password = "123" }, out var errors);

        Assert.False(ok);
        Assert.Contains("Email", errors.Keys);
        Assert.Contains("Password", errors.Keys);
    }
}
