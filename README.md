# Firma OPERA Cloud — Angular + ASP.NET Core

Solución migrada a Angular 21 y ASP.NET Core 10 con arquitectura por capas. Blazor,
tablet, estación y SignalR ya no forman parte del código de ejecución.

## Arranque local

1. Configure las variables descritas en `docs/CONFIGURACION_SEGURA.md`.
2. API: `dotnet run --project src/FirmaOperaCloud.Api`.
3. Angular: en `web-angular`, ejecute `npm ci` y después `npm start`.
4. Abra `http://localhost:4200`. El proxy envía `/api` a ASP.NET.

Para un paquete de un solo origen, `dotnet publish src/FirmaOperaCloud.Api -c Release`
compila Angular y lo incorpora a `wwwroot` del artefacto publicado.

Las migraciones no se aplican automáticamente salvo que
`Database__ApplyMigrations=true`. En UAT y producción deben ejecutarse como una
etapa controlada y respaldada del despliegue.

## Cliente Angular generado desde OpenAPI

Con la API de desarrollo ejecutándose en el puerto 5016, use
`npm run api:generate` dentro de `web-angular`. El generador lee el contrato Swagger y
crea tipos y servicios en `src/app/core/generated`; así, un cambio incompatible del
backend se detecta al compilar Angular. El cliente manual actual se conserva durante
la transición y debe sustituirse gradualmente por los servicios generados.
