using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CreditosApp.Hubs;

// P6: Hub protegido con Identity. Cada usuario autenticado entra
// automaticamente a un grupo con su propio UserId (del servidor,
// nunca enviado por el navegador). Conexion anonima => 401.
[Authorize]
public class SolicitudesHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var uid = Context.UserIdentifier!;
        await Groups.AddToGroupAsync(Context.ConnectionId, uid);
        await base.OnConnectedAsync();
    }
}
