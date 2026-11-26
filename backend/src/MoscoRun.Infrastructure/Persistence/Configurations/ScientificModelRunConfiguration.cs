using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificModelRunConfiguration : IEntityTypeConfiguration<ScientificModelRunRow>
{
    public void Configure(EntityTypeBuilder<ScientificModelRunRow> builder)
    {
        builder.ToTable("scientific_model_runs", t =>
        {
            t.HasCheckConstraint("ck_scientific_model_run_time", "available_at >= decision_at AND (target_at IS NULL OR target_at >= decision_at)");
        });
        builder.HasKey(x => new { x.OwnerId, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ModelId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(200).IsRequired();
        builder.Property(x => x.OutputsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.StateJson).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.OwnerId, x.ModelId, x.Version, x.DecisionAt, x.AvailableAt }).HasDatabaseName("ix_scientific_model_runs_state_lookup");
        builder.HasOne<ScientificModelArtifactRow>().WithMany().HasForeignKey(x => new { x.OwnerId, x.ModelId, x.Version }).OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_scientific_model_runs_scientific_model_artifacts_model");
    }
}
