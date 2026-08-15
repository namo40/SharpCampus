using Microsoft.EntityFrameworkCore;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public sealed class SharpCampusDbContext(DbContextOptions<SharpCampusDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<MatchRecord> MatchRecords => Set<MatchRecord>();

    public DbSet<OwnedSkin> OwnedSkins => Set<OwnedSkin>();

    public DbSet<MissionProgress> MissionProgress => Set<MissionProgress>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // UnitGenerator emits these converters, but EF never finds them on its own: without this registration
        // every value object would be mapped as an unsupported entity type instead of its underlying column.
        configurationBuilder.Properties<UserId>().HaveConversion<UserId.UserIdValueConverter>();
        configurationBuilder.Properties<Coins>().HaveConversion<Coins.CoinsValueConverter>();
        configurationBuilder.Properties<Rating>().HaveConversion<Rating.RatingValueConverter>();
        configurationBuilder.Properties<MatchId>().HaveConversion<MatchId.MatchIdValueConverter>();
        configurationBuilder.Properties<SkinId>().HaveConversion<SkinId.SkinIdValueConverter>();
        configurationBuilder.Properties<MissionId>().HaveConversion<MissionId.MissionIdValueConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The schema script owns the tables, so this mapping only has to agree with it: no migrations are
        // generated from the model.
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
        profile.Property(p => p.EquippedSkinId).HasColumnName("equipped_skin_id").ValueGeneratedOnAdd();
        profile.Property(p => p.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();
        profile.Property(p => p.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd();

        var record = modelBuilder.Entity<MatchRecord>();

        record.ToTable("match_records");

        // A game writes one row per player, which is what makes the pair the key.
        record.HasKey(r => new { r.MatchId, r.UserId });

        record.Property(r => r.MatchId).HasColumnName("match_id");
        record.Property(r => r.UserId).HasColumnName("user_id");
        record.Property(r => r.Outcome).HasColumnName("outcome");
        record.Property(r => r.EndReason).HasColumnName("end_reason");
        record.Property(r => r.RatingBefore).HasColumnName("rating_before");
        record.Property(r => r.RatingAfter).HasColumnName("rating_after");
        record.Property(r => r.CoinsAwarded).HasColumnName("coins_awarded");
        record.Property(r => r.LinesCleared).HasColumnName("lines_cleared");
        record.Property(r => r.Quads).HasColumnName("quads");
        record.Property(r => r.GarbageSent).HasColumnName("garbage_sent");
        record.Property(r => r.HardDrops).HasColumnName("hard_drops");
        record.Property(r => r.MaxCombo).HasColumnName("max_combo");
        record.Property(r => r.DurationTicks).HasColumnName("duration_ticks");
        record.Property(r => r.CreatedAt).HasColumnName("created_at").ValueGeneratedOnAdd();

        var owned = modelBuilder.Entity<OwnedSkin>();

        owned.ToTable("owned_skins");

        // The pair is the key, which is what makes a second purchase of the same skin a violation.
        owned.HasKey(o => new { o.UserId, o.SkinId });

        owned.Property(o => o.UserId).HasColumnName("user_id");
        owned.Property(o => o.SkinId).HasColumnName("skin_id");
        owned.Property(o => o.AcquiredAt).HasColumnName("acquired_at").ValueGeneratedOnAdd();

        var mission = modelBuilder.Entity<MissionProgress>();

        mission.ToTable("mission_progress");

        // The day belongs in the key: progress is per account, per mission and per date, and yesterday's
        // row stays where it is once the date moves on.
        mission.HasKey(m => new { m.UserId, m.MissionDate, m.MissionId });

        mission.Property(m => m.UserId).HasColumnName("user_id");
        mission.Property(m => m.MissionDate).HasColumnName("mission_date");
        mission.Property(m => m.MissionId).HasColumnName("mission_id");
        mission.Property(m => m.Progress).HasColumnName("progress");
        mission.Property(m => m.Claimed).HasColumnName("claimed").ValueGeneratedOnAdd();
        mission.Property(m => m.UpdatedAt).HasColumnName("updated_at").ValueGeneratedOnAdd();
    }
}
