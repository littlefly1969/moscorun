using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificLifecycleAuditConfiguration : IEntityTypeConfiguration<ScientificLifecycleAuditRow>
{
    public void Configure(EntityTypeBuilder<ScientificLifecycleAuditRow> builder)
    {
        builder.ToTable("scientific_lifecycle_audit");
        builder.HasKey(x => new { x.OwnerId, x.OperationId });
        builder.Property(x => x.OperationId).ValueGeneratedNever();
        builder.Property(x => x.Action).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CountsJson).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => new { x.OwnerId, x.At });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
