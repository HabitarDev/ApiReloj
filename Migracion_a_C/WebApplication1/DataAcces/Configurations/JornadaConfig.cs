using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class JornadaConfig : IEntityTypeConfiguration<Jornada>
{
    public void Configure(EntityTypeBuilder<Jornada> builder)
    {
        builder.ToTable("Jornadas");

        builder.HasKey(x => x.JornadaId);

        builder.Property(x => x.JornadaId)
            .HasMaxLength(26)
            .IsRequired();

        builder.Property(x => x.EmployeeNumber).IsRequired();
        builder.Property(x => x.ResidentialId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ClockSn).IsRequired();

        builder.Property(x => x.StartAt);
        builder.Property(x => x.BreakInAt);
        builder.Property(x => x.BreakOutAt);
        builder.Property(x => x.EndAt);

        builder.Property(x => x.StatusCheck)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.StatusBreak)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.WarningsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ErrorsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ProjectionStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Revision).IsRequired();
        builder.Property(x => x.IsDeleted).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.EmployeeNumber, x.ResidentialId, x.StatusCheck });
        builder.HasIndex(x => new
            {
                x.EmployeeNumber,
                x.ResidentialId,
                x.IdentityDeviceSn,
                x.IdentitySerialNumber
            })
            .IsUnique();
        builder.HasIndex(x => x.UpdatedAt);
        builder.HasIndex(x => x.StartAt);
        builder.HasIndex(x => x.ClockSn);
    }
}
