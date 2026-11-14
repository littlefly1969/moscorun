using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class WorkoutSeriesConfiguration : IEntityTypeConfiguration<WorkoutSeries>
{
    public void Configure(EntityTypeBuilder<WorkoutSeries> builder)
    {
        builder.ToTable("workout_series");
        builder.HasKey(s => new { s.WorkoutId, s.Metric });
        builder.Property(s => s.Metric).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.Data).IsRequired();
        builder.HasOne<Workout>().WithMany().HasForeignKey(s => s.WorkoutId).OnDelete(DeleteBehavior.Cascade);
    }
}
