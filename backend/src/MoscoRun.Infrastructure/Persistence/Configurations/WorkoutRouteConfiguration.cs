using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Domain.Workouts;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class WorkoutRouteConfiguration : IEntityTypeConfiguration<WorkoutRoute>
{
    public void Configure(EntityTypeBuilder<WorkoutRoute> builder)
    {
        builder.ToTable("workout_routes");
        builder.HasKey(r => r.WorkoutId);
        builder.Property(r => r.Data).IsRequired();
        builder.HasOne<Workout>().WithOne().HasForeignKey<WorkoutRoute>(r => r.WorkoutId).OnDelete(DeleteBehavior.Cascade);
    }
}
