using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MoscoRun.Domain.Devices;
using MoscoRun.Domain.Measurements;
using MoscoRun.Infrastructure.Identity;

namespace MoscoRun.Infrastructure.Persistence;

public sealed class MoscoRunDbContext(DbContextOptions<MoscoRunDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Weighing> Weighings => Set<Weighing>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("moscorun");
        builder.ApplyConfigurationsFromAssembly(typeof(MoscoRunDbContext).Assembly);
    }
}
