using Microsoft.AspNetCore.Identity;
using CreditosApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

// Datos iniciales P1: rol Analista + usuario analista,
// 2 clientes y 2 solicitudes (una Pendiente y una Aprobada).
public static class SeedData
{
    public const string AnalistaEmail = "analista@usmp.pe";
    public const string AnalistaPassword = "Analista123*";
    public const string ClienteEmail = "cliente1@usmp.pe";
    public const string ClientePassword = "Cliente123*";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var context = sp.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();

        if (!await roleManager.RoleExistsAsync("Analista"))
            await roleManager.CreateAsync(new IdentityRole("Analista"));

        async Task<IdentityUser> EnsureUser(string email, string password, string? role = null)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var r = await userManager.CreateAsync(user, password);
                if (!r.Succeeded)
                    throw new InvalidOperationException("Seed usuario: " + string.Join("; ", r.Errors.Select(e => e.Description)));
            }
            if (role is not null && !await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);
            return user;
        }

        var analista = await EnsureUser(AnalistaEmail, AnalistaPassword, "Analista");
        var clienteUser1 = await EnsureUser(ClienteEmail, ClientePassword);
        var clienteUser2 = await EnsureUser("cliente2@usmp.pe", ClientePassword);

        if (!await context.Clientes.AnyAsync())
        {
            var c1 = new Cliente { UsuarioId = clienteUser1.Id, IngresosMensuales = 2500m, Activo = true };
            var c2 = new Cliente { UsuarioId = clienteUser2.Id, IngresosMensuales = 4000m, Activo = true };
            context.Clientes.AddRange(c1, c2);
            await context.SaveChangesAsync();

            context.Solicitudes.AddRange(
                new SolicitudCredito
                {
                    ClienteId = c1.Id,
                    MontoSolicitado = 8000m, // 3.2x ingresos: aprobable
                    FechaSolicitud = DateTime.UtcNow.AddDays(-2),
                    Estado = EstadoSolicitud.Pendiente
                },
                new SolicitudCredito
                {
                    ClienteId = c2.Id,
                    MontoSolicitado = 10000m, // 2.5x ingresos
                    FechaSolicitud = DateTime.UtcNow.AddDays(-10),
                    Estado = EstadoSolicitud.Aprobado
                });
            await context.SaveChangesAsync();
        }
    }
}
