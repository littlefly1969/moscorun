using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificSignalChunkConfiguration : IEntityTypeConfiguration<ScientificSignalChunkRow>
{
    public void Configure(EntityTypeBuilder<ScientificSignalChunkRow> builder)
    {
        builder.ToTable("scientific_signal_chunks", t =>
        {
            t.HasCheckConstraint("ck_scientific_signal_time", "first_ticks <= last_ticks AND available_ticks >= last_ticks");
            t.HasCheckConstraint("ck_scientific_signal_count", "sample_count BETWEEN 1 AND 16384");
            t.HasCheckConstraint("ck_scientific_signal_hash", "octet_length(content_hash) = 32");
        });
        builder.HasKey(x => new { x.OwnerId, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Source).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalId).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Revision).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Metric).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.OwnerId, x.Source, x.Metric, x.FirstTicks, x.Id }).HasDatabaseName("ix_scientific_signal_chunks_owner_source_metric_start");
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
