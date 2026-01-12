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

        builder.Property(x => x.ResidentialId).IsRequired();
        builder.HasIndex(x => x.ResidentialId);
    }
}