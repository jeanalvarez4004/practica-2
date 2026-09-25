using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Data;
using CreditosApp.Models;

namespace CreditosApp.Controllers;

// P2: catalogo "Mis solicitudes" del usuario autenticado + filtros server-side.
[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _users;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> users)
    {
        _context = context;
        _users = users;
    }

    private string UsuarioId => _users.GetUserId(User)!;

    // GET /Solicitudes?estado=&montoMin=&montoMax=&desde=&hasta=
    public async Task<IActionResult> Index(
        EstadoSolicitud? estado,
        decimal? montoMin,
        decimal? montoMax,
        DateTime? desde,
        DateTime? hasta)
    {
        // Validaciones server-side de filtros
        if (montoMin is < 0 || montoMax is < 0)
            ModelState.AddModelError(string.Empty, "Los montos no pueden ser negativos.");
        if (montoMin.HasValue && montoMax.HasValue && montoMin > montoMax)
            ModelState.AddModelError(string.Empty, "El monto minimo no puede ser mayor al monto maximo.");
        if (desde.HasValue && hasta.HasValue && desde > hasta)
            ModelState.AddModelError(string.Empty, "La fecha de inicio no puede ser mayor a la fecha fin.");

        var clienteIds = await _context.Clientes
            .Where(c => c.UsuarioId == UsuarioId)
            .Select(c => c.Id)
            .ToListAsync();

        var q = _context.Solicitudes
            .Include(s => s.Cliente)
            .Where(s => clienteIds.Contains(s.ClienteId));

        if (ModelState.IsValid)
        {
            if (estado.HasValue) q = q.Where(s => s.Estado == estado.Value);
            if (montoMin.HasValue) q = q.Where(s => s.MontoSolicitado >= montoMin.Value);
            if (montoMax.HasValue) q = q.Where(s => s.MontoSolicitado <= montoMax.Value);
            if (desde.HasValue) q = q.Where(s => s.FechaSolicitud >= desde.Value);
            if (hasta.HasValue) q = q.Where(s => s.FechaSolicitud <= hasta.Value.AddDays(1).AddTicks(-1));
        }

        ViewBag.Estado = estado;
        ViewBag.MontoMin = montoMin;
        ViewBag.MontoMax = montoMax;
        ViewBag.Desde = desde?.ToString("yyyy-MM-dd");
        ViewBag.Hasta = hasta?.ToString("yyyy-MM-dd");

        return View(await q.OrderByDescending(s => s.FechaSolicitud).ToListAsync());
    }

    // GET /Solicitudes/Details/5 (solo el propietario)
    public async Task<IActionResult> Details(int id)
    {
        var s = await _context.Solicitudes
            .Include(x => x.Cliente)
            .FirstOrDefaultAsync(x => x.Id == id && x.Cliente!.UsuarioId == UsuarioId);
        if (s is null) return NotFound();
        return View(s);
    }
}
