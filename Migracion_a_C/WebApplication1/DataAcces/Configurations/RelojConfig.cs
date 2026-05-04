using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class RelojConfig: IEntityTypeConfiguration<Reloj>
{
    public void Configure(EntityTypeBuilder<Reloj> builder)
    {
        builder.ToTable("Relojes");
        builder.HasKey(x => x.IdReloj);
        builder.Property(x => x.IdReloj).HasMaxLength(128).ValueGeneratedNever();

        builder.Property(x => x.Puerto).IsRequired();
        builder.Property(x => x.ResidentialId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DeviceSn);
        builder.Property(x => x.LastPushEvent);
        builder.Property(x => x.LastPollEvent);

        builder.HasIndex(x => x.ResidentialId);
        builder.HasIndex(x => x.DeviceSn).IsUnique();
    }
}
