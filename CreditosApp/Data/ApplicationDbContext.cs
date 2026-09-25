using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Models;

namespace CreditosApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<SolicitudCredito> Solicitudes => Set<SolicitudCredito>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(e =>
        {
            e.Property(c => c.IngresosMensuales).HasPrecision(18, 2);
            e.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SolicitudCredito>(e =>
        {
            e.Property(s => s.MontoSolicitado).HasPrecision(18, 2);
            // Regla: un cliente solo puede tener UNA solicitud Pendiente.
            e.HasIndex(s => s.ClienteId)
                .IsUnique()
                .HasFilter("\"Estado\" = 0");
            e.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
