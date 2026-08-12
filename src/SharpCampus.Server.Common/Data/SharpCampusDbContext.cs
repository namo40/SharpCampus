using Microsoft.EntityFrameworkCore;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public sealed class SharpCampusDbContext(DbContextOptions<SharpCampusDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // UnitGenerator emits these converters, but EF never finds them on its own: without this registration
        // every value object would be mapped as an unsupported entity type instead of its underlying column.
        configurationBuilder.Properties<UserId>().HaveConversion<UserId.UserIdValueConverter>();
        configurationBuilder.Properties<Coins>().HaveConversion<Coins.CoinsValueConverter>();
        configurationBuilder.Properties<Rating>().HaveConversion<Rating.RatingValueConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The schema script owns the table, so this mapping only has to agree with it: no migrations are generated
        // from the model, and columns the server never reads (equipped_skin_id) stay out of the model entirely.
        var profile = modelBuilder.Entity<Profile>();

        profile.ToTable("profiles");
        profile.HasKey(p => p.UserId);

        // The account provider supplies the id, so EF must not try to generate one.
        profile.Property(p => p.UserId).HasColumnName("user_id").ValueGeneratedNever();
        profile.Property(p => p.Nickname).HasColumnName("nickname");

        // Left to the database: EF omits these from the INSERT while they still hold their default value,
        // which is what makes a new profile start at the column defaults rather than at zero.
        profile.Property(p => p.Coins).HasColumnName("coins").ValueGeneratedOnAdd();
        profile.Property(p => p.Rating).HasColumnName("rating").ValueGeneratedOnAdd();
        profile.Property(p => p.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        profile.Property(p => p.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd();
    }
}
