using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using CreditosApp.Data;
using CreditosApp.Models;
using CreditosApp.Services;

namespace CreditosApp.Controllers;

// P2: catalogo "Mis solicitudes" del usuario autenticado + filtros server-side.
[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _users;
    private readonly IDistributedCache _cache;

    public SolicitudesController(ApplicationDbContext context, UserManager<IdentityUser> users, IDistributedCache cache)
    {
        _context = context;
        _users = users;
        _cache = cache;
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

        // P4: sin filtros => lista cacheada 60s por usuario (Redis o memoria).
        var sinFiltros = !estado.HasValue && !montoMin.HasValue && !montoMax.HasValue && !desde.HasValue && !hasta.HasValue;
        if (sinFiltros && ModelState.IsValid)
        {
            var key = CacheKeys.SolicitudesDe(UsuarioId);
            var hit = await _cache.GetStringAsync(key);
            if (hit is not null)
            {
                ViewBag.Cache = true;
                return View(JsonSerializer.Deserialize<List<SolicitudDto>>(hit)!
                    .Select(d => new SolicitudCredito
                    {
                        Id = d.Id,
                        MontoSolicitado = d.Monto,
                        FechaSolicitud = d.Fecha,
                        Estado = d.Estado
                    }).ToList());
            }
            var data = await q.OrderByDescending(s => s.FechaSolicitud).ToListAsync();
            var dto = data.Select(s => new SolicitudDto(s.Id, s.MontoSolicitado, s.FechaSolicitud, s.Estado)).ToList();
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) });
            return View(data);
        }

        return View(await q.OrderByDescending(s => s.FechaSolicitud).ToListAsync());
    }

    // GET /Solicitudes/Details/5 (solo el propietario)
    public async Task<IActionResult> Details(int id)
    {
        var s = await _context.Solicitudes
            .Include(x => x.Cliente)
            .FirstOrDefaultAsync(x => x.Id == id && x.Cliente!.UsuarioId == UsuarioId);
        if (s is null) return NotFound();
        // P4: sesion => ultima solicitud visitada (se muestra en el layout).
        HttpContext.Session.SetInt32("UltimaSolicitudId", s.Id);
        HttpContext.Session.SetString("UltimaSolicitudMonto", s.MontoSolicitado.ToString("N2"));
        return View(s);
    }

    // GET /Solicitudes/Create
    public async Task<IActionResult> Create()
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == UsuarioId && c.Activo);
        if (cliente is null)
        {
            TempData["Error"] = "No tienes un cliente activo asociado. Contacta al administrador.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Ingresos = cliente.IngresosMensuales;
        ViewBag.Maximo = cliente.IngresosMensuales * 10;
        return View(new SolicitudCredito());
    }

    // POST /Solicitudes/Create — P3: validaciones server-side
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SolicitudCredito model)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == UsuarioId && c.Activo);
        if (cliente is null)
        {
            ModelState.AddModelError(string.Empty, "No tienes un cliente activo asociado.");
            return View(model);
        }
        if (model.MontoSolicitado <= 0)
            ModelState.AddModelError(nameof(model.MontoSolicitado), "El monto debe ser mayor a 0.");
        if (model.MontoSolicitado > cliente.IngresosMensuales * 10)
            ModelState.AddModelError(nameof(model.MontoSolicitado),
                $"El monto no puede superar 10 veces tus ingresos (máx. S/ {(cliente.IngresosMensuales * 10):N2}).");
        if (await _context.Solicitudes.AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente))
            ModelState.AddModelError(string.Empty, "Ya tienes una solicitud pendiente. Espera su evaluación.");

        if (!ModelState.IsValid)
        {
            ViewBag.Ingresos = cliente.IngresosMensuales;
            ViewBag.Maximo = cliente.IngresosMensuales * 10;
            return View(model);
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };
        try
        {
            _context.Solicitudes.Add(solicitud);
            await _context.SaveChangesAsync();
            // P4: invalida el listado cacheado del usuario.
            await _cache.RemoveAsync(CacheKeys.SolicitudesDe(UsuarioId));
        }
        catch (DbUpdateException)
        {
            // Carrera contra el indice unico: ya existe una pendiente.
            ModelState.AddModelError(string.Empty, "Ya tienes una solicitud pendiente. Espera su evaluación.");
            ViewBag.Ingresos = cliente.IngresosMensuales;
            ViewBag.Maximo = cliente.IngresosMensuales * 10;
            return View(model);
        }

        TempData["Exito"] = $"Solicitud #{solicitud.Id} registrada con éxito. Está pendiente de evaluación.";
        return RedirectToAction(nameof(Details), new { id = solicitud.Id });
    }
}
