using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoscoRun.Domain.Devices;
using MoscoRun.Domain.Measurements;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence.Configurations;

internal sealed class WeighingConfiguration : IEntityTypeConfiguration<Weighing>
{
    public void Configure(EntityTypeBuilder<Weighing> builder)
    {
        builder.ToTable("weighings", t =>
        {
            t.HasCheckConstraint("ck_weighings_weight_kg", $"weight_kg BETWEEN {Weighing.MinWeightKg} AND {Weighing.MaxWeightKg}");
            t.HasCheckConstraint("ck_weighings_impedance_ohm", $"impedance_ohm IS NULL OR impedance_ohm BETWEEN {Weighing.MinImpedanceOhm} AND {Weighing.MaxImpedanceOhm}");
        });
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.SourceEventId).HasMaxLength(Weighing.SourceEventIdMaxLength).IsRequired();
        builder.Property(w => w.WeightKg).HasPrecision(5, 2);
        builder.Property(w => w.Attribution).HasConversion<string>().HasMaxLength(16);

        // Idempotenza: un evento dell'agente produce al più una pesata.
        builder.HasIndex(w => new { w.DeviceId, w.SourceEventId }).IsUnique();
        builder.HasIndex(w => new { w.OwnerId, w.MeasuredAt });

        builder.HasOne<Device>().WithMany().HasForeignKey(w => w.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(w => w.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
