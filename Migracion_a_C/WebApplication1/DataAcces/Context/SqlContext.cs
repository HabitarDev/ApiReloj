using System.Reflection;
using Dominio;
using Microsoft.EntityFrameworkCore;
namespace DataAcces.Context;

public sealed class SqlContext : DbContext
{
    public DbSet<Reloj> Relojes => Set<Reloj>();
    public DbSet<Residential> Residentials => Set<Residential>();

    public SqlContext(DbContextOptions<SqlContext> options)
        : base(options)
    {
        if(!Database.IsInMemory())
        {
            Database.Migrate();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas las configuraciones de esta assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}