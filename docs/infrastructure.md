# GRD infrastructure

## Service composition

Each HTTP service is composed in the same order:

1. `AddServiceDefaults()` configures structured logs, problem details, and health checks.
2. `AddApplication()` discovers MediatR handlers and FluentValidation validators.
3. `AddInfrastructure(configuration)` registers MySQL persistence and RabbitMQ publishing.
4. Controllers, authorization, OpenAPI, and service endpoints are mapped.

The API projects are the composition roots. Domain and Application projects do not
depend on MySQL, RabbitMQ, ASP.NET Core, or other infrastructure implementations.

## Required configuration

Use environment variables in deployed environments. Do not store credentials in
`appsettings.json`.

| Setting | Environment variable | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Database` | `ConnectionStrings__Database` | Service-owned MySQL connection string |
| `RabbitMq:HostName` | `RabbitMq__HostName` | RabbitMQ host |
| `RabbitMq:Port` | `RabbitMq__Port` | AMQP port; defaults to `5672` |
| `RabbitMq:UserName` | `RabbitMq__UserName` | RabbitMQ user |
| `RabbitMq:Password` | `RabbitMq__Password` | RabbitMQ password |
| `RabbitMq:VirtualHost` | `RabbitMq__VirtualHost` | Virtual host; defaults to `/` |
| `RabbitMq:ExchangeName` | `RabbitMq__ExchangeName` | Topic exchange; defaults to `grd.integration` |

`IDbConnectionFactory` opens connections lazily. An API can therefore start without
a database during early development, but its repository operation will fail with a
clear configuration error until `ConnectionStrings__Database` is supplied.

## Local dependencies

Copy `deploy/docker/.env.example` to `deploy/docker/.env`, change the local passwords,
then start MySQL and RabbitMQ from the `deploy/docker` directory:

```text
docker compose --env-file .env -f compose.infrastructure.yml up -d
```

RabbitMQ management is available at `http://localhost:15672` by default. The compose
file is intended only for local development; production secrets must come from the
deployment platform's secret manager.

## Gateway routes

In Development, the gateway listens on `http://localhost:7000` and forwards paths
under `/api/<service>/...` to the ports in each service's launch profile. Production
destinations should be supplied through configuration or environment variables.

## Health checks

- `/health/live` proves that the process is alive.
- `/health/ready` runs every registered readiness check.

Database and broker readiness checks should be added when deployment credentials and
the expected failure policy are defined; liveness must remain independent of external
dependencies.
