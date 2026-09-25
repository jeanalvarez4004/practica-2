using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using CreditosApp.Data;
using CreditosApp.Hubs;
using CreditosApp.Models;
using CreditosApp.Services;

namespace CreditosApp.Controllers;

// P5: panel exclusivo del rol Analista.
[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IHubContext<SolicitudesHub> _hub;

    public AnalistaController(ApplicationDbContext context, IDistributedCache cache, IHubContext<SolicitudesHub> hub)
    {
        _context = context;
        _cache = cache;
        _hub = hub;
    }

    // GET /Analista — pendientes de todos los clientes
    public async Task<IActionResult> Index()
    {
        var list = await _context.Solicitudes
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderBy(s => s.FechaSolicitud)
            .ToListAsync();
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var s = await _context.Solicitudes.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue {s.Estado}. No se puede reprocesar.";
            return RedirectToAction(nameof(Index));
        }
        // Regla: no aprobar si supera 5x los ingresos.
        if (s.Cliente is null || s.MontoSolicitado > s.Cliente.IngresosMensuales * 5)
        {
            TempData["Error"] = $"No se puede aprobar: el monto supera 5 veces los ingresos del cliente (máx. S/ {(s.Cliente?.IngresosMensuales * 5 ?? 0):N2}).";
            return RedirectToAction(nameof(Index));
        }
        s.Estado = EstadoSolicitud.Aprobado;
        s.MotivoRechazo = null;
        await _context.SaveChangesAsync();
        await InvalidarCachePropietario(s);
        // P6: primero BD + cache, despues evento solo al propietario.
        await NotificarPropietario(s);
        TempData["Exito"] = $"Solicitud #{id} aprobada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string? motivo)
    {
        var s = await _context.Solicitudes.Include(x => x.Cliente).FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        if (s.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{id} ya fue {s.Estado}. No se puede reprocesar.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["Error"] = "El motivo de rechazo es obligatorio.";
            return RedirectToAction(nameof(Index));
        }
        s.Estado = EstadoSolicitud.Rechazado;
        s.MotivoRechazo = motivo.Trim();
        await _context.SaveChangesAsync();
        await InvalidarCachePropietario(s);
        // P6: primero BD + cache, despues evento solo al propietario.
        await NotificarPropietario(s);
        TempData["Exito"] = $"Solicitud #{id} rechazada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task InvalidarCachePropietario(SolicitudCredito s)
    {
        if (s.Cliente?.UsuarioId is string uid)
            await _cache.RemoveAsync(CacheKeys.SolicitudesDe(uid));
    }

    // P6: evento dirigido al grupo del propietario (UserId del servidor).
    private Task NotificarPropietario(SolicitudCredito s)
    {
        if (s.Cliente?.UsuarioId is not string uid) return Task.CompletedTask;
        return _hub.Clients.Group(uid).SendAsync("SolicitudEstadoActualizado", new
        {
            solicitudId = s.Id,
            estado = s.Estado.ToString(),
            motivoRechazo = s.MotivoRechazo
        });
    }
}
