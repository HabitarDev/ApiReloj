using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class ResidentialConfig: IEntityTypeConfiguration<Residential>
{
    public void Configure(EntityTypeBuilder<Residential> builder)
    {
        builder.ToTable("Residentials");
        builder.HasKey(x => x.IdResidential);
        builder.Property(x => x.IdResidential).ValueGeneratedNever();

        builder.HasMany(r => r.Relojes)
               .WithOne(r => r.Residential)
               .HasForeignKey(r => r.ResidentialId)
               .OnDelete(DeleteBehavior.Restrict); // o Cascade

        builder.HasMany(r => r.Devices)
               .WithOne(d => d.Residential)
               .HasForeignKey(d => d.ResidentialId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
