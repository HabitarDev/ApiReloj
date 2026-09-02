using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class JornadaProjectionStateConfig : IEntityTypeConfiguration<JornadaProjectionState>
{
    public void Configure(EntityTypeBuilder<JornadaProjectionState> builder)
    {
        builder.ToTable("JornadaProjectionStates");
        builder.HasKey(x => new { x.EmployeeNumber, x.ResidentialId });

        builder.Property(x => x.EmployeeNumber).IsRequired();
        builder.Property(x => x.ResidentialId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.RequestedRevision).IsRequired();
        builder.Property(x => x.AppliedRevision).IsRequired();
        builder.Property(x => x.Attempts).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.UpdatedAtUtc });
        builder.HasIndex(x => x.DirtyFromUtc);
    }
}
