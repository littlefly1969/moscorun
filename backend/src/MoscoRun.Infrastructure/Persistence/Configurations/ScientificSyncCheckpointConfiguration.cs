using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificSyncCheckpointConfiguration : IEntityTypeConfiguration<ScientificSyncCheckpointRow>
{
    public void Configure(EntityTypeBuilder<ScientificSyncCheckpointRow> builder)
    {
        builder.ToTable("scientific_sync_checkpoints");
        builder.HasKey(x => new { x.OwnerId, x.Source, x.Stream });
        builder.Property(x => x.Source).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Stream).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Cursor).HasMaxLength(2048).IsRequired().IsConcurrencyToken();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
