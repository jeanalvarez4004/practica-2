using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Data;

namespace CreditosApp.Controllers;

// P7: "Mis notificaciones" del usuario autenticado.
[Authorize]
public class NotificacionesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _users;

    public NotificacionesController(ApplicationDbContext context, UserManager<IdentityUser> users)
    {
        _context = context;
        _users = users;
    }

    public async Task<IActionResult> Index()
    {
        var uid = _users.GetUserId(User)!;
        var list = await _context.Notificaciones
            .Where(n => n.UsuarioId == uid)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ToListAsync();
        return View(list);
    }
}
