namespace CreditosApp.Services;

// P7: opciones de Cloud MQ (RabbitMQ en CloudAMQP) por variables de entorno.
public class RabbitMqOptions
{
    public const string Section = "RabbitMq";
    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "solicitudes.notificaciones";
    public bool ConsumerEnabled { get; set; } = true;
}
