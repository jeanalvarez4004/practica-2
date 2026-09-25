using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CreditosApp.Services;

// P7: productor. Publica mensajes persistentes con confirmacion del broker.
// Si no hay broker configurado o falla, devuelve ok=false SIN lanzar.
public class NotificacionesPublisher
{
    private readonly RabbitMqOptions _opt;
    private readonly ILogger<NotificacionesPublisher> _log;

    public NotificacionesPublisher(IOptions<RabbitMqOptions> opt, ILogger<NotificacionesPublisher> log)
    {
        _opt = opt.Value;
        _log = log;
    }

    public async Task<(bool Ok, string? Error)> PublishAsync(SolicitudRegistradaEvent evt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opt.ConnectionString))
            return (false, "Cola no configurada (RabbitMq__ConnectionString vacio).");

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_opt.ConnectionString),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
                SocketReadTimeout = TimeSpan.FromSeconds(5),
                SocketWriteTimeout = TimeSpan.FromSeconds(5),
                AutomaticRecoveryEnabled = true,
            };
            await using var conn = await factory.CreateConnectionAsync(ct);
            await using var ch = await conn.CreateChannelAsync(cancellationToken: ct);
            await ch.QueueDeclareAsync(_opt.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

            // Confirmacion del publicador (API v7: eventos BasicAcks/BasicNacks).
            var seq = await ch.GetNextPublishSequenceNumberAsync(ct);
            var ack = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            AsyncEventHandler<BasicAckEventArgs> onAck = (_, ea) =>
            {
                if (ea.DeliveryTag >= (ulong)seq) ack.TrySetResult(true);
                return Task.CompletedTask;
            };
            AsyncEventHandler<BasicNackEventArgs> onNack = (_, ea) =>
            {
                if (ea.DeliveryTag >= (ulong)seq) ack.TrySetResult(false);
                return Task.CompletedTask;
            };
            ch.BasicAcksAsync += onAck;
            ch.BasicNacksAsync += onNack;
            try
            {
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));
                var props = new BasicProperties { Persistent = true, MessageId = evt.MessageId, ContentType = "application/json" };
                await ch.BasicPublishAsync(exchange: string.Empty, routingKey: _opt.QueueName,
                    mandatory: true, basicProperties: props, body: body, cancellationToken: ct);
                var confirmado = await ack.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
                if (!confirmado)
                    return (false, "El broker rechazó el mensaje (nack).");
            }
            finally
            {
                ch.BasicAcksAsync -= onAck;
                ch.BasicNacksAsync -= onNack;
            }
            _log.LogInformation("Evento {MessageId} aceptado por el broker.", evt.MessageId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo encolar el evento {MessageId}.", evt.MessageId);
            return (false, ex.Message);
        }
    }
}
