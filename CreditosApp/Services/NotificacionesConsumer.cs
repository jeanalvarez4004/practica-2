using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using CreditosApp.Data;
using CreditosApp.Models;

namespace CreditosApp.Services;

// P7: consumidor en BackgroundService. ACK manual solo despues de guardar.
// Duplicados (mismo MessageId) se confirman sin insertar. Mensaje invalido
// o error de proceso => reject SIN reencolar (evita reintentos infinitos);
// el reenvio es manual con el mismo MessageId (ver README).
public class NotificacionesConsumer : BackgroundService
{
    private readonly RabbitMqOptions _opt;
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificacionesConsumer> _log;

    public NotificacionesConsumer(IOptions<RabbitMqOptions> opt, IServiceProvider services, ILogger<NotificacionesConsumer> log)
    {
        _opt = opt.Value;
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.ConsumerEnabled)
        {
            _log.LogWarning("Consumidor RabbitMQ desactivado (RabbitMq__ConsumerEnabled=false). Los mensajes quedaran en cola.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_opt.ConnectionString))
        {
            _log.LogWarning("Consumidor RabbitMQ sin ConnectionString. No se inicia.");
            return;
        }

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_opt.ConnectionString),
            AutomaticRecoveryEnabled = true,
        };
        var conn = await factory.CreateConnectionAsync(stoppingToken);
        var ch = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await ch.QueueDeclareAsync(_opt.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await ch.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var evt = JsonSerializer.Deserialize<SolicitudRegistradaEvent>(ea.Body.Span);
                if (evt is null || string.IsNullOrWhiteSpace(evt.MessageId) || evt.SolicitudId <= 0 || string.IsNullOrWhiteSpace(evt.UsuarioId))
                {
                    _log.LogWarning("Mensaje invalido, se rechaza sin reencolar.");
                    await ch.BasicRejectAsync(ea.DeliveryTag, requeue: false, stoppingToken);
                    return;
                }

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (await db.Notificaciones.AnyAsync(n => n.MessageId == evt.MessageId, stoppingToken))
                {
                    _log.LogInformation("MessageId {MessageId} ya procesado: se confirma sin duplicar.", evt.MessageId);
                    await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                    return;
                }

                db.Notificaciones.Add(new Notificacion
                {
                    MessageId = evt.MessageId,
                    SolicitudId = evt.SolicitudId,
                    UsuarioId = evt.UsuarioId,
                    Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                    FechaProcesamientoUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(stoppingToken);
                await ch.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken); // ACK solo tras guardar
                _log.LogInformation("Notificacion guardada para solicitud {SolicitudId}.", evt.SolicitudId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error procesando mensaje. Se rechaza sin reencolar (reenvio manual, ver README).");
                await ch.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
        };

        await ch.BasicConsumeAsync(_opt.QueueName, autoAck: false, consumer, stoppingToken);
        _log.LogInformation("Consumidor escuchando cola {Queue}.", _opt.QueueName);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
