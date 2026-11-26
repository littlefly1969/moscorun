using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificModelArtifactConfiguration : IEntityTypeConfiguration<ScientificModelArtifactRow>
{
    public void Configure(EntityTypeBuilder<ScientificModelArtifactRow> builder)
    {
        builder.ToTable("scientific_model_artifacts", t =>
        {
            t.HasCheckConstraint("ck_scientific_model_artifact_hash", "octet_length(artifact_hash) = 32");
            t.HasCheckConstraint("ck_scientific_model_artifact_size", "octet_length(artifact) BETWEEN 1 AND 16777216");
            t.HasCheckConstraint("ck_scientific_model_training_time", "training_cutoff <= registered_at");
        });
        builder.HasKey(x => new { x.OwnerId, x.ModelId, x.Version });
        builder.Property(x => x.ModelId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FeatureSchemaVersion).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ValidationJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ConfigurationJson).HasColumnType("jsonb").IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
