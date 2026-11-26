using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class ScientificLifecycleFenceConfiguration : IEntityTypeConfiguration<ScientificLifecycleFenceRow>
{
    public void Configure(EntityTypeBuilder<ScientificLifecycleFenceRow> builder)
    {
        builder.ToTable("scientific_lifecycle_fences");
        builder.HasKey(x => x.OwnerId);
        builder.Property(x => x.OwnerId).ValueGeneratedNever();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
