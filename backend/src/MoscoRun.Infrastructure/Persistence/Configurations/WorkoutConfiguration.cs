using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Domain.Devices;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class WorkoutConfiguration : IEntityTypeConfiguration<Workout>
{
    public void Configure(EntityTypeBuilder<Workout> builder)
    {
        builder.ToTable("workouts", t =>
        {
            t.HasCheckConstraint("ck_workouts_time", "ended_at > started_at");
            t.HasCheckConstraint("ck_workouts_distance_m", $"distance_m IS NULL OR distance_m BETWEEN 0 AND {Workout.MaxDistanceM}");
            t.HasCheckConstraint("ck_workouts_track_point_count", $"track_point_count BETWEEN 0 AND {Workout.MaxTrackPoints}");
            t.HasCheckConstraint("ck_workouts_content_hash", "octet_length(content_hash) = 32");
        });
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Source).HasMaxLength(Workout.SourceMaxLength).IsRequired();
        builder.Property(w => w.SourceId).HasMaxLength(Workout.SourceIdMaxLength).IsRequired();
        builder.Property(w => w.ActivityType).HasMaxLength(Workout.ActivityTypeMaxLength).IsRequired();
        builder.Property(w => w.ContentHash).IsRequired();
        builder.Ignore(w => w.Bounds);
        builder.Ignore(w => w.HasTrack);

        // Idempotenza: lo stesso allenamento della stessa fonte esiste una sola volta per persona,
        // anche se arriva da due telefoni diversi.
        builder.HasIndex(w => new { w.OwnerId, w.Source, w.SourceId }).IsUnique();
        builder.HasIndex(w => new { w.OwnerId, w.StartedAt });

        builder.HasOne<Device>().WithMany().HasForeignKey(w => w.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(w => w.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
