# Copilot Instructions — un-big-brother (UnTaskAlert)

## Contexto (qué hace el proyecto)
- Bot de Telegram + Azure Functions (isolated worker) para monitorear actividad en Azure DevOps (work items / pull requests) y generar reportes de horas.
- Persiste configuración y estado del usuario (subscriber) en Cosmos DB.

## Principios/decisiones de este repo
- Mantener el Target Framework actual del proyecto principal (ver `src/UnTaskAlert/UnTaskAlert.csproj`).
- Tests unitarios: MSTest.
- Observabilidad:
  - Producción: Application Insights.
  - Local: logging a consola (sin necesidad de AppInsights).

## Mapa de arquitectura (rutas importantes)
- Host/DI: `src/UnTaskAlert/Program.cs`
- Triggers Azure Functions:
  - Timer monitoring: `src/UnTaskAlert/Functions/UnTaskAlertFunction.cs`
  - HTTP webhook Telegram: `src/UnTaskAlert/Functions/TelegramBotFunction.cs`
  - Background polling: `src/UnTaskAlert/Functions/TelegramBotBackgroundService.cs`
- Routing de comandos: `src/UnTaskAlert/CommandProcessor.cs`
- Workflows/comandos: `src/UnTaskAlert/Commands/Workflow/*`
- Data access:
  - Cosmos: `src/UnTaskAlert/DbAccessor.cs`
  - Azure DevOps work items: `src/UnTaskAlert/BacklogAccessor.cs`, `src/UnTaskAlert/QueryBuilder.cs`
  - Azure DevOps pull requests: `src/UnTaskAlert/PrAccessor.cs`
- Notificaciones:
  - Telegram: `src/UnTaskAlert/TelegramNotifier.cs`
  - Email: `src/UnTaskAlert/MailSender.cs`

## Ejecución local
- Settings: copiar `src/UnTaskAlert/local.settings.json.Template` a `src/UnTaskAlert/local.settings.json` y completar valores.
- Run:
  - Preferido: `func start` desde `src/UnTaskAlert`
  - Alternativo: `dotnet run` desde `src/UnTaskAlert`

## Observabilidad (AppInsights + consola)
- Local: logs a consola; evitar volcar payloads completos de Telegram.
- Prod:
  - configurar `APPLICATIONINSIGHTS_CONNECTION_STRING` en App Settings
  - mantener sampling/config en `src/UnTaskAlert/host.json`
- Reglas de logging:
  - No loguear secretos: tokens, PINs, payloads completos, emails completos (usar masking).

## Cómo agregar un workflow/comando
1) Crear un workflow en `src/UnTaskAlert/Commands/Workflow/` heredando de `CommandWorkflow`.
2) Implementar `DoesAccept(string)` y `PerformStep(...)`.
3) Registrar el workflow en el array de `workflows` en `src/UnTaskAlert/CommandProcessor.cs`.
4) Si es multi-step:
   - persistir solo estado mínimo en `Subscriber` (nombre del workflow, step, expiración, data)
   - NO serializar objetos del workflow.

## Tests (MSTest)
- Proyecto: `src/UnTaskAlert.Tests/UnTaskAlert.Tests.csproj`
- Prioridad para unit tests:
  - Lógica pura: `QueryBuilder`, `DateUtils`, parsing de argumentos/fechas en workflows.
  - Evitar dependencia de red (Azure DevOps/Telegram) en tests unitarios.

## Checklist para PRs
- Compila: `dotnet build src/UnTaskAlert/UnTaskAlert.csproj`
- Tests: `dotnet test src/UnTaskAlert.Tests/UnTaskAlert.Tests.csproj`
- No filtrar PII/secretos en logs (especialmente si AppInsights está habilitado).
- Cambios de configuración: actualizar `local.settings.json.Template`.

## Plan de trabajo para nuevas features
- Agregar comando/workflow + tests unitarios de parsing/branching.
- Ajustar integración (DevOps/Telegram) con timeouts y manejo de errores.
- Asegurar telemetría mínima (logs y excepciones) sin PII.
