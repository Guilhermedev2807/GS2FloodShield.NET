using System.ComponentModel.DataAnnotations;

namespace FloodShield.Shelters.Api.Infrastructure;

public static class RequestValidator
{
    public static bool TryValidate<T>(T model, out Dictionary<string, string[]> errors) where T : notnull
    {
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        errors = results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { "" })
                .Select(member => (Member: member, Message: r.ErrorMessage ?? "Valor inválido.")))
            .GroupBy(x => x.Member)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

        return ok;
    }
}
