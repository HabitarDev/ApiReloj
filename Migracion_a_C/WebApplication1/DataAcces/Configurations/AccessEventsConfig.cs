using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAcces.Configurations;

public class AccessEventsConfig : IEntityTypeConfiguration<AccessEvents>
{
    public void Configure(EntityTypeBuilder<AccessEvents> builder)
    {
        builder.ToTable("AccessEvents");

        builder.HasKey(x => new { x.DeviceSn, x.SerialNumber });

        builder.Property(x => x.DeviceSn).IsRequired();

        builder.Property(x => x.SerialNumber).IsRequired();

        builder.Property(x => x.EventTimeUtc).IsRequired();

        builder.Property(x => x.TimeDevice);

        builder.Property(x => x.EmployeeNumber);

        builder.Property(x => x.Major).IsRequired();

        builder.Property(x => x.Minor).IsRequired();

        builder.Property(x => x.AttendanceStatus);

        builder.Property(x => x.Raw)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(x => x.EventTimeUtc);

        builder.HasIndex(x => x.EmployeeNumber);

        builder.HasIndex(x => new { x.DeviceSn, x.EventTimeUtc });

        builder.HasIndex(x => new { x.Major, x.Minor, x.EventTimeUtc });

        builder.HasIndex(x => x.Minor);

        builder.HasIndex(x => x.AttendanceStatus);
    }
}
