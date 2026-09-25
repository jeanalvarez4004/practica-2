using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

// P7: notificacion generada por el consumidor de la cola.
public class Notificacion
{
    public int Id { get; set; }

    // UUID del evento; unico para evitar duplicados por redelivery.
    [Required]
    public string MessageId { get; set; } = string.Empty;

    public int SolicitudId { get; set; }

    [Required]
    public string UsuarioId { get; set; } = string.Empty;

    [Required]
    public string Texto { get; set; } = string.Empty;

    public DateTime FechaProcesamientoUtc { get; set; } = DateTime.UtcNow;
}
