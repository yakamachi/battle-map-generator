using System.Text.Json;
using BattleMapGenerator.Api.Maps;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BattleMapGenerator.Api.Data;

// The account store: Identity tables plus the Data Protection key ring, so keys survive
// App Service restarts and sessions are not invalidated. Each account's saved maps live here too.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public DbSet<SavedMap> SavedMaps { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity maps its own tables here; it must run first.
        base.OnModelCreating(builder);

        builder.Entity<SavedMap>(map =>
        {
            map.HasKey(m => m.Id);
            map.HasOne<IdentityUser>()
                .WithMany()
                .HasForeignKey(m => m.OwnerId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            map.Property(m => m.Title).HasMaxLength(SavedMap.MaxTitleLength).IsRequired();
            map.Property(m => m.Encounter)
                .HasConversion(new JsonEnumNameConverter<EncounterType>())
                .HasMaxLength(SavedMap.MaxEnumNameLength);
            map.Property(m => m.BossSize)
                .HasConversion(new JsonEnumNameConverter<BossSize>())
                .HasMaxLength(SavedMap.MaxEnumNameLength);
            map.Property(m => m.Cells).HasMaxLength(SavedMap.MaxCellBytes).IsRequired();
            map.Property(m => m.Rooms).HasMaxLength(SavedMap.MaxRoomBytes).IsRequired();
            // The library lists one account's maps, newest first.
            map.HasIndex(m => new { m.OwnerId, m.CreatedAt });
        });
    }

    // Stores an enum by its JSON name ("skirmish", "large"), so the column reads like the API and a
    // reordered enum cannot change what a stored row means. On a nullable property EF passes no nulls.
    private sealed class JsonEnumNameConverter<TEnum>() : ValueConverter<TEnum, string>(
        value => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()),
        name => Enum.Parse<TEnum>(name, true))
        where TEnum : struct, Enum;
}
