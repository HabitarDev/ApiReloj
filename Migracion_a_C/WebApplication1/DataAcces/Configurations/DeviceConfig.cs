using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class DeviceConfig : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(x => x.DeviceId);
        builder.Property(x => x.DeviceId).ValueGeneratedNever();

        builder.Property(x => x.SecretKey).IsRequired();
        builder.Property(x => x.ResidentialId).IsRequired();
        builder.HasIndex(x => x.ResidentialId);
    }
}
