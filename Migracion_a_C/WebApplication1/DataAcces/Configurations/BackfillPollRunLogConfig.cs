using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class BackfillPollRunLogConfig : IEntityTypeConfiguration<BackfillPollRunLog>
{
    public void Configure(EntityTypeBuilder<BackfillPollRunLog> builder)
    {
        builder.ToTable("BackfillPollRuns");

        builder.HasKey(x => x.RunId);

        builder.Property(x => x.RunId)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Trigger)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.FinishedAtUtc);

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(x => x.Error);

        builder.Property(x => x.TotalClocks).IsRequired();
        builder.Property(x => x.TotalWindows).IsRequired();
        builder.Property(x => x.TotalPages).IsRequired();
        builder.Property(x => x.Inserted).IsRequired();
        builder.Property(x => x.Duplicates).IsRequired();
        builder.Property(x => x.Ignored).IsRequired();

        builder.Property(x => x.ClocksJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.Status);
    }
}
