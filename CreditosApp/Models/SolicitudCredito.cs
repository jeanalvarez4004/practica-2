using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class SolicitudCredito
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    [Display(Name = "Monto solicitado (S/)")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Fecha de solicitud")]
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;

    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;

    [Display(Name = "Motivo de rechazo")]
    public string? MotivoRechazo { get; set; }
}
