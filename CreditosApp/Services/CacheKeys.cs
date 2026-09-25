namespace CreditosApp.Services;

// Claves de cache P4. Se invalida al registrar o cambiar estado (P5/P6 reusan).
public static class CacheKeys
{
    public static string SolicitudesDe(string usuarioId) => $"solicitudes:{usuarioId}";
}
