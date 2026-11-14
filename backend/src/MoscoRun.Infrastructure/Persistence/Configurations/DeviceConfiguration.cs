using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Domain.Devices;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).HasMaxLength(Device.NameMaxLength).IsRequired();
        builder.Property(d => d.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.TokenHash).HasMaxLength(32).IsRequired();
        builder.Ignore(d => d.IsActive);
        builder.HasIndex(d => d.OwnerId);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(d => d.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
