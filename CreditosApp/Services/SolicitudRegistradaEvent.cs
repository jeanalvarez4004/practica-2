namespace CreditosApp.Services;

// P7: evento publicado al registrar una solicitud Pendiente.
public record SolicitudRegistradaEvent(
    string MessageId,
    int SolicitudId,
    string UsuarioId,
    DateTime FechaEventoUtc);
