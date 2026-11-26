using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class AthleteFeedbackEntryConfiguration : IEntityTypeConfiguration<AthleteFeedbackEntryRow>
{
    public void Configure(EntityTypeBuilder<AthleteFeedbackEntryRow> builder)
    {
        builder.ToTable("scientific_feedback_entries", t =>
            t.HasCheckConstraint("ck_scientific_feedback_time", "available_at >= time"));
        builder.HasKey(x => new { x.OwnerId, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EntryId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Revision).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(4000);
        builder.Property(x => x.ObservationIds).IsRequired();
        builder.HasIndex(x => new { x.OwnerId, x.EntryId, x.AvailableAt });
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
