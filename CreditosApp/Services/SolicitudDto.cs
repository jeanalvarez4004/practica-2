using CreditosApp.Models;

namespace CreditosApp.Services;

// DTO liviano para cachear el listado en Redis.
public record SolicitudDto(int Id, decimal Monto, DateTime Fecha, EstadoSolicitud Estado);
