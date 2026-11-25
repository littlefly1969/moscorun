using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificObservationConfiguration : IEntityTypeConfiguration<ScientificObservationRow>
{
    public void Configure(EntityTypeBuilder<ScientificObservationRow> builder)
    {
        builder.ToTable("scientific_observations", t =>
        {
            t.HasCheckConstraint("ck_scientific_observations_time", "available_at >= time");
            t.HasCheckConstraint("ck_scientific_observations_value", "value IS NULL OR (value > '-Infinity'::double precision AND value < 'Infinity'::double precision)");
        });
        builder.HasKey(o => new { o.OwnerId, o.Id });
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Metric).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Unit).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Source).HasMaxLength(200).IsRequired();
        builder.Property(o => o.InputIds).IsRequired();
        builder.HasIndex(o => new { o.OwnerId, o.Time, o.AvailableAt });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(o => o.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
