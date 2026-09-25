# Práctica 2 — Plataforma de Créditos (ASP.NET Core MVC + Identity + EF Core SQLite)

Sistema interno para gestionar solicitudes de crédito: usuarios autenticados registran solicitudes,
analistas de riesgo las aprueban/rechazan con reglas de negocio, notificaciones en tiempo real (SignalR)
y mensajería asíncrona (RabbitMQ/CloudAMQP). Desplegado en Render como Web Service (Docker).

- **URL Render:** `https://practica-2-97ng.onrender.com`
- **Stack:** .NET 10, ASP.NET Core MVC, Identity, EF Core + SQLite, Session/Cache Redis,
  SignalR, RabbitMQ.Client 7.x, Docker.

## Usuarios de prueba (seed automático)

| Rol | Email | Clave |
|-----|-------|-------|
| Analista | `analista@usmp.pe` | `Analista123*` |
| Cliente | `cliente1@usmp.pe` | `Cliente123*` |
| Cliente | `cliente2@usmp.pe` | `Cliente123*` |

Seed: 2 clientes (S/ 2500 y S/ 4000), 2 solicitudes (una Pendiente S/ 8000, una Aprobada S/ 10000).

## Correr en local

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations list --project CreditosApp   # ver migraciones
dotnet run --project CreditosApp                  # migra + seed automáticos
# http://localhost:5000 (o el puerto que muestre)
```

Migraciones:
```bash
dotnet ef migrations add <Nombre> --project CreditosApp
dotnet ef database update --project CreditosApp
```

## Variables de entorno

| Variable | Local | Render |
|----------|-------|--------|
| `ASPNETCORE_ENVIRONMENT` | Development | `Production` |
| `ASPNETCORE_URLS` | (default) | La fija el `CMD` del Dockerfile con `$PORT` |
| `ConnectionStrings__DefaultConnection` | `DataSource=app.db;Cache=Shared` | igual (SQLite efímero, ver nota) |
| `Redis__ConnectionString` | vacío = cache en memoria | URL de Redis Cloud (`rediss://...`) |
| `RabbitMq__ConnectionString` | vacío = sin cola (avisa y guarda igual) | URI `amqps://...` de CloudAMQP |
| `RabbitMq__QueueName` | `solicitudes.notificaciones` | igual |
| `RabbitMq__ConsumerEnabled` | `true` | `true` (`false` = acumula en cola, ver P7) |

Nunca subir credenciales al repo: solo van como Environment Variables en Render.

## Despliegue en Render

1. Dashboard → **New → Web Service** → repo `practica-2`, rama `main` (o **New → Blueprint** con `render.yaml`).
2. Runtime Docker (Render no tiene runtime nativo .NET). Health check `/`.
3. Cargar las variables de la tabla. **Una sola instancia** (el consumidor corre dentro del Web Service).
4. Deploy. Cada push a `main` redespliega.

> **SQLite en Render (plan free):** el disco es efímero, la base **se recrea en cada deploy/reinicio**
> y el seed idempotente la deja lista (migraciones + usuarios de prueba). Para datos durables:
> plan pago con Disco en `/opt/render/project/src/data` y `ConnectionStrings__DefaultConnection`
> apuntando ahí (ver bloque comentado en `render.yaml`), o migrar a Postgres.

## Evidencias P6 (WebSocket)

1. Abrir sesión de `cliente1@usmp.pe` en una ventana y de `analista@usmp.pe` en otra (o incógnito).
2. En cliente: abrir **Mis solicitudes** (badge `● en línea`, verificar transporte `websocket` en DevTools → Red → WS).
3. En analista: aprobar/rechazar la pendiente → el cliente ve el estado y el aviso **sin recargar**.
4. Con `cliente2@usmp.pe` comprobar que **no** recibe el evento.
5. Sin login: `POST /hubs/solicitudes/negotiate` → `401`.

## Evidencias P7 (Cloud MQ) y reenvío

1. Con `RabbitMq__ConsumerEnabled=false`, registrar una solicitud → ver mensaje pendiente en CloudAMQP.
2. Reactivar (`true`, redeploy) → la cola se vacía y aparece **una sola** notificación en **Mis notificaciones**.
3. Reenviar el mismo `MessageId` (botón **Reenviar notificación** en el detalle) → no se duplica
   (índice único en `MessageId`, se confirma sin insertar).
4. Si el broker falla al publicar: la solicitud **se conserva**, queda aviso amarillo y se reenvía
   manualmente con el mismo `MessageId` desde el detalle (sin patrón outbox en esta práctica).

## Ramas y PRs

| Pregunta | Rama | PR |
|----------|------|----|
| P1 bootstrap + dominio | `feature/bootstrap-dominio` | #1 |
| P2 catálogo y filtros | `feature/catalogo-solicitudes` | #2 |
| P3 registro y validaciones | `feature/solicitudes` | #3 |
| P4 sesión y Redis | `feature/sesion-redis` | #4 |
| P5 panel analista | `feature/panel-analista` | #5 |
| P6 websocket | `feature/websocket-notificaciones` | #6 |
| P7 cloud MQ | `feature/cloudmq-notificaciones` | #7 |
| P8 deploy | `deploy/render` | #8 |
