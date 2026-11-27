using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScienceDiagnosticConfiguration : IEntityTypeConfiguration<ScienceDiagnosticRow>
{
    public void Configure(EntityTypeBuilder<ScienceDiagnosticRow> builder)
    {
        builder.ToTable("science_diagnostics");
        builder.HasKey(x => new { x.OwnerId, x.DeviceId, x.Source });
        builder.Property(x => x.Source).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
