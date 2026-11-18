using System.Security.Cryptography;
using FloodShield.Shelters.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FloodShield.Shelters.Api.Data;

/// <summary>
/// Cria o usuário Admin (senha vinda de configuração/segredo; nenhuma credencial fixa no código)
/// e, apenas em Development, abrigos de exemplo.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment env,
        IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var email = (config["Seed:AdminEmail"] ?? "admin@floodshield.local").Trim().ToLowerInvariant();
        if (!await db.Users.AnyAsync(u => u.Email == email))
        {
            var password = config["Seed:AdminPassword"];
            var generated = false;

            if (string.IsNullOrWhiteSpace(password))
            {
                if (env.IsDevelopment())
                {
                    password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12)) + "aA1!";
                    generated = true;
                }
                else
                {
                    logger.LogWarning("Seed:AdminPassword não configurado: usuário admin NÃO foi criado.");
                }
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                var admin = new User { Name = "Administrador", Email = email, Role = UserRole.Admin };
                admin.PasswordHash = hasher.HashPassword(admin, password);
                db.Users.Add(admin);
                await db.SaveChangesAsync();

                if (generated)
                    logger.LogWarning(
                        "Admin de DESENVOLVIMENTO criado -> e-mail: {Email} | senha: {Password} (gerada aleatoriamente). " +
                        "Para uma senha fixa use: dotnet user-secrets set \"Seed:AdminPassword\" \"<senha>\"",
                        email, password);
            }
        }

        if (env.IsDevelopment() && !await db.Shelters.AnyAsync())
        {
            db.Shelters.AddRange(
                Shelter.Create("Abrigo Exemplo - Centro", "Rua Exemplo, 100 - Centro, São Bernardo do Campo/SP", -23.6939, -46.5650, 200, "(11) 4000-0001"),
                Shelter.Create("Abrigo Exemplo - Rudge Ramos", "Av. Exemplo, 250 - Rudge Ramos, São Bernardo do Campo/SP", -23.6500, -46.5800, 120, "(11) 4000-0002"),
                Shelter.Create("Ponto de Apoio Exemplo - Alvarenga", "Estrada Exemplo, 900 - Alvarenga, São Bernardo do Campo/SP", -23.7400, -46.6000, 80, null));
            await db.SaveChangesAsync();
        }
    }
}
