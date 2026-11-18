using FloodShield.Shelters.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FloodShield.Shelters.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<User> Users => Set<User>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Shelter>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(120).IsRequired();
            e.Property(s => s.Address).HasMaxLength(250).IsRequired();
            e.Property(s => s.Phone).HasMaxLength(30);
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Ignore(s => s.AvailableSpots);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => new { s.Latitude, s.Longitude });
        });

        b.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).HasMaxLength(120).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(u => u.Email).IsUnique();
        });

        b.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Key).HasMaxLength(100).IsRequired();
            e.Property(r => r.UserId).HasMaxLength(64).IsRequired();
            e.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
            e.HasIndex(r => new { r.Key, r.UserId }).IsUnique();
        });
    }
}
