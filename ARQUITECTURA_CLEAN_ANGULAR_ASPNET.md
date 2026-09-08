# Guía de arquitectura para FirmaOperaCloud

## Angular + ASP.NET Core Web API + Clean Architecture

**Proyecto:** FirmaOperaCloud  


> Este documento explica la arquitectura propuesta. No contiene contraseñas, tokens, cadenas de conexión, App Keys ni otros secretos.

---

s` y `Controllers`.

El backend utiliza componentes de **ASP.NET Core MVC** para recibir solicitudes HTTP:

- Controladores derivados de `ControllerBase`.
- Atributos como `[ApiController]`, `[Route]`, `[HttpGet]` y `[HttpPost]`.
- Model binding y validación de solicitudes.
- Filtros, autenticación y autorización.
- Respuestas JSON, PDF u otros archivos.



Visto desde una perspectiva amplia de MVC:

| Concepto MVC | Implementación en FirmaOperaCloud |
|---|---|
| Model | Entidades de `Domain`, casos de uso y DTO |
| View | Aplicación Angular |
| Controller | Controladores de `FirmaOperaCloud.Api` |

Angular también tiene componentes, servicios y modelos propios, pero no utiliza estrictamente el patrón MVC clásico.

---

## 2. ¿Qué es Clean Architecture?

Clean Architecture organiza el sistema para que las reglas importantes del negocio no dependan de tecnologías externas.

Por ejemplo, la regla “un expediente debe registrar quién descargó un documento” no debería depender directamente de:

- SQL Server.
- Entity Framework Core.
- OPERA Cloud.
- Azure Blob Storage.
- Angular.
- EasyLex o DocuSign.

Estas tecnologías pueden cambiar. La regla de negocio debería permanecer estable.

La idea central es que las dependencias apunten hacia el núcleo:

```text
┌──────────────────────────────────────────────┐
│ Angular                                      │
│ Pantallas, formularios, navegación           │
└──────────────────────┬───────────────────────┘
                       │ HTTPS /api
                       ▼
┌──────────────────────────────────────────────┐
│ API                                          │
│ HTTP, autenticación, autorización, OpenAPI   │
└──────────────────────┬───────────────────────┘
                       │ ejecuta casos de uso
                       ▼
┌──────────────────────────────────────────────┐
│ Application                                  │
│ Casos de uso, contratos y coordinación       │
└──────────────────────┬───────────────────────┘
                       │ utiliza el modelo
                       ▼
┌──────────────────────────────────────────────┐
│ Domain                                       │
│ Entidades, reglas e invariantes               │
└──────────────────────────────────────────────┘

Infrastructure implementa los contratos de Application:

OPERA Cloud ─┐
SQL Server ──┤
Azure Blob ──┼──> Infrastructure ──> Application / Domain
Key Vault ───┤
OCR / PSC ───┘
```

---

## 3. Responsabilidad de cada proyecto

### 3.1 `FirmaOperaCloud.Domain`

Es el núcleo del sistema. Representa conceptos y reglas del negocio.

Puede contener:

- `Reservation`.
- `ReservationFile` o `ExpedienteReserva`.
- `Document` y `DocumentVersion`.
- `SignatureEvidence`.
- `AuditEvent`.
- `AccessPurpose`.
- Estados del expediente y del documento.
- Reglas de autorización propias del negocio.
- Excepciones de dominio.
- Objetos de valor como `ConfirmationNumber`, `HotelId` y `DocumentHash`.

Ejemplo simplificado:

```csharp
public sealed class ReservationFile
{
    public Guid Id { get; private set; }
    public string ConfirmationNumber { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public FileStatus Status { get; private set; }

    public void Seal()
    {
        if (Status != FileStatus.Ready)
            throw new DomainException("El expediente todavía no está listo.");

        Status = FileStatus.Sealed;
    }
}
```

`Domain` no debería conocer:

- `DbContext`.
- Controladores HTTP.
- `HttpClient`.
- Archivos de configuración.
- Azure SDK.
- Angular.
- Detalles de Tesseract.

Idealmente, este proyecto sólo depende de la biblioteca estándar de .NET.

### 3.2 `FirmaOperaCloud.Application`

Contiene los casos de uso: lo que el sistema permite hacer.

Ejemplos:

- Consultar una reserva.
- Preparar una tarjeta de registro.
- Capturar una firma.
- Crear una versión definitiva del PDF.
- Construir y sellar el expediente.
- Registrar un acceso al expediente.
- Subir el documento aprobado a OPERA.
- Procesar una identificación con OCR.
- Solicitar una constancia NOM-151.

También define contratos que serán implementados por Infrastructure:

```csharp
public interface IOperaReservationGateway
{
    Task<Reservation?> GetAsync(
        string hotelId,
        string confirmationNumber,
        CancellationToken cancellationToken);
}

public interface IDocumentStorage
{
    Task<StoredDocument> StoreImmutableAsync(
        DocumentContent document,
        CancellationToken cancellationToken);
}

public interface IAuditWriter
{
    Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken);
}
```

Un caso de uso coordina esos contratos:

```csharp
public sealed class OpenReservationFileUseCase
{
    private readonly IReservationFileRepository _files;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditWriter _audit;

    public async Task<ReservationFileResult> ExecuteAsync(
        OpenReservationFileCommand command,
        UserContext user,
        CancellationToken cancellationToken)
    {
        await _authorization.RequireAsync(user, "files.read", cancellationToken);

        var file = await _files.GetAsync(command.FileId, cancellationToken);

        await _audit.AppendAsync(
            AuditEvent.FileOpened(file.Id, user.Id, command.Reason),
            cancellationToken);

        return ReservationFileResult.From(file);
    }
}
```

Application determina **qué ocurre**. Infrastructure determina **cómo se conecta técnicamente**.

### 3.3 `FirmaOperaCloud.Infrastructure`

Implementa los contratos técnicos de Application.

Aquí pertenecen:

- Integración OHIP/OPERA Cloud.
- Obtención y renovación de token OCIM.
- EF Core y SQL Server.
- Repositorios.
- Azure Blob Storage.
- Azure Key Vault.
- Tesseract u otro OCR.
- Generación y manipulación de PDF.
- Correo SMTP u otro proveedor de correo.
- EasyLex, DocuSign o un PSC.
- Telemetría técnica.

Ejemplo:

```csharp
public sealed class OperaReservationGateway : IOperaReservationGateway
{
    private readonly HttpClient _httpClient;
    private readonly IOperaTokenProvider _tokens;

    public async Task<Reservation?> GetAsync(
        string hotelId,
        string confirmationNumber,
        CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAsync(cancellationToken);
        // Construcción y ejecución de la solicitud OHIP.
        // La respuesta externa se traduce al modelo interno.
    }
}
```

Infrastructure puede cambiar sin obligar a cambiar los casos de uso. Por ejemplo, `TesseractOcrProvider` podría sustituirse por otro proveedor que implemente la misma interfaz.

### 3.4 `FirmaOperaCloud.Api`

Es el punto de entrada del backend y la capa de presentación HTTP.

Contiene:

- Controladores o Minimal APIs.
- Autenticación.
- Políticas de autorización.
- Middleware.
- Manejo global de errores.
- Rate limiting.
- Validación de archivos y solicitudes.
- OpenAPI/Swagger.
- Health checks.
- Inyección de dependencias.
- Encabezados de seguridad.

El controlador debe ser pequeño:

```csharp
[ApiController]
[Route("api/reservations")]
public sealed class ReservationsController : ControllerBase
{
    [HttpGet("{confirmationNumber}")]
    [Authorize(Policy = "Reservations.Read")]
    public async Task<ActionResult<ReservationResponse>> Get(
        string confirmationNumber,
        CancellationToken cancellationToken)
    {
        var result = await _useCase.ExecuteAsync(
            new GetReservationQuery(confirmationNumber),
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
```

Un controlador no debería:

- Contener cientos de líneas de reglas de negocio.
- Acceder directamente a muchas tablas.
- Construir manualmente todo un PDF.
- Conocer el Client Secret de OPERA.
- Decidir reglas legales de retención.

### 3.5 `web-angular`

Es la aplicación visual.

Sus responsabilidades son:

- Mostrar datos.
- Capturar entradas.
- Validar la experiencia del usuario.
- Navegar entre pantallas.
- Invocar `/api`.
- Mostrar errores y estados de carga.
- Aplicar accesibilidad.

Angular nunca debería conectarse directamente a SQL Server, OPERA Cloud, Key Vault, Blob Storage o un proveedor de firma.

Una estructura recomendada es:

```text
web-angular/src/app/
├── core/
│   ├── auth/
│   ├── http/
│   ├── guards/
│   ├── interceptors/
│   └── layout/
├── shared/
│   ├── components/
│   ├── directives/
│   ├── pipes/
│   └── utilities/
├── features/
│   ├── reservations/
│   ├── registration-card/
│   ├── files/
│   ├── signatures/
│   ├── audit/
│   ├── ocr/
│   ├── templates/
│   ├── promotions/
│   └── administration/
└── app.routes.ts
```

Cada funcionalidad puede contener:

```text
features/files/
├── pages/
├── components/
├── data-access/
├── models/
└── files.routes.ts
```

---

## 4. Reglas de dependencia

Dependencias permitidas:

```text
Application    -> Domain
Infrastructure -> Application + Domain
Api            -> Application + Infrastructure
Angular        -> API mediante HTTPS
```

Dependencias que deben evitarse:

```text
Domain      -> Infrastructure
Domain      -> Api
Application -> Api
Application -> Angular
Angular     -> SQL Server
Angular     -> OPERA Cloud
```

`Api` registra las implementaciones mediante inyección de dependencias:

```csharp
services.AddScoped<IOperaReservationGateway, OperaReservationGateway>();
services.AddScoped<IDocumentStorage, AzureBlobDocumentStorage>();
services.AddScoped<IAuditWriter, SqlAuditWriter>();
```

Application solicita interfaces; en tiempo de ejecución recibe las implementaciones de Infrastructure.

---

## 5. Flujo completo de una consulta

Supongamos que el usuario busca la reserva `123456`.

1. Angular envía:

```http
GET /api/reservations/123456
```

2. ASP.NET Core valida la sesión del usuario.
3. La política `Reservations.Read` verifica sus permisos.
4. `ReservationsController` valida el formato básico.
5. El controlador llama a `GetReservationUseCase`.
6. Application solicita la reserva mediante `IOperaReservationGateway`.
7. Infrastructure obtiene un token OCIM desde el backend.
8. Infrastructure consulta OPERA Cloud UAT.
9. La respuesta de OHIP se traduce al modelo interno.
10. Application aplica las reglas y devuelve un resultado.
11. Se registra un evento de auditoría con usuario, fecha, resultado y correlación.
12. API devuelve un DTO JSON.
13. Angular presenta la información.

Las credenciales OHIP nunca se entregan al navegador.

---

## 6. DTO, entidades y modelos de Angular

No todos los modelos son iguales.

### Entidad de dominio

Representa el negocio y protege invariantes. No debe exponerse directamente por HTTP.

### DTO de API

Define exactamente lo que entra o sale del endpoint:

```csharp
public sealed record ReservationResponse(
    string ConfirmationNumber,
    string GuestName,
    DateOnly Arrival,
    DateOnly Departure);
```

### Modelo TypeScript

Representa ese contrato en Angular:

```typescript
export interface ReservationResponse {
  confirmationNumber: string;
  guestName: string;
  arrival: string;
  departure: string;
}
```

Para evitar diferencias entre C# y TypeScript, se recomienda publicar OpenAPI y generar el cliente Angular automáticamente.

---

## 7. Estructura final recomendada

```text
FirmaOperaCloud-Angular/
├── src/
│   ├── FirmaOperaCloud.Domain/
│   ├── FirmaOperaCloud.Application/
│   ├── FirmaOperaCloud.Infrastructure/
│   └── FirmaOperaCloud.Api/
├── web-angular/
├── tests/
│   ├── FirmaOperaCloud.Domain.Tests/
│   ├── FirmaOperaCloud.Application.Tests/
│   ├── FirmaOperaCloud.Infrastructure.Tests/
│   ├── FirmaOperaCloud.Api.Tests/
│   └── e2e/
├── deploy/
│   ├── docker/
│   └── scripts/
├── docs/
├── .github/
│   └── workflows/
├── Directory.Build.props
├── global.json
└── README.md
```

Se eliminarían:

- Frontend Blazor `FirmaOperaCloud.Web`.
- Aplicación `FirmaOperaCloud.Tablet`.
- Controladores de dispositivos.
- Sesiones de estación/tablet.
- Hubs y clientes SignalR.
- Entidades exclusivas de emparejamiento.
- Dependencias que ya no tengan consumidores.

La eliminación de tablas debe manejarse mediante migraciones y respaldos; no se deben borrar directamente de UAT o producción.

---

## 8. Seguridad por capas

### Navegador y Angular

- Preferir sesión con cookie `HttpOnly`, `Secure` y `SameSite`.
- Evitar tokens de larga duración en `localStorage`.
- Implementar Content Security Policy.
- No utilizar HTML dinámico sin sanitización.
- No incluir secretos en archivos `environment.ts`.
- Usar HTTPS incluso en UAT.
- Validar rutas para experiencia del usuario, sin confiar en ellas como autorización real.

### API

- Autorización por política en cada endpoint.
- MFA mediante Microsoft Entra ID cuando sea viable.
- Rate limiting en login, OCR, búsqueda y descargas.
- Límites de tamaño y tiempo.
- Validación real del tipo de archivo por contenido.
- Encabezados de seguridad.
- Errores estandarizados con `ProblemDetails`.
- Correlation ID por solicitud.
- No registrar tokens, contraseñas ni documentos.

### Infraestructura

- Secretos en Azure Key Vault.
- Identidad administrada para leer Key Vault.
- Mínimo privilegio para SQL y Blob Storage.
- Redes privadas cuando la infraestructura lo permita.
- Cifrado en tránsito y reposo.
- Backups con restauración probada.
- Monitoreo y alertas.

### Angular y autorización

Un guard sólo evita que la interfaz navegue a una página. No protege el endpoint.

La API debe volver a verificar el permiso:

```csharp
[Authorize(Policy = "Files.Download")]
[HttpGet("{id:guid}/content")]
public Task<IActionResult> Download(Guid id) { ... }
```

---

## 9. Configuración y secretos

`appsettings.json` no es inseguro por sí mismo. Lo inseguro es colocar secretos dentro y versionarlos.

Clasificación recomendada:

| Dato | Ubicación recomendada |
|---|---|
| Logging y límites no sensibles | Configuración normal |
| URL pública de API | Configuración pública Angular |
| Hotel habilitado | Azure App Configuration o variable de entorno |
| Client Secret OHIP | Azure Key Vault |
| App Key OHIP | Azure Key Vault |
| Clave JWT o certificado | Azure Key Vault |
| Contraseña SQL | Key Vault, o identidad administrada si aplica |
| Contraseña SMTP | Azure Key Vault |

En desarrollo se utilizan `.NET User Secrets`. En UAT y producción se utiliza Key Vault. Angular no debe poseer ningún secreto.

---

## 10. Expediente de reserva y evidencia

El sistema debería evolucionar de “guardar un PDF firmado” a administrar un expediente.

Un expediente puede incluir:

- `ExpedienteId` con GUID versión 7.
- `CreatedAtUtc` independiente.
- Hotel y confirmación.
- Identificador interno de reserva.
- Versiones de cada documento.
- SHA-256 de cada archivo.
- Manifiesto canónico del expediente.
- SHA-256 del manifiesto.
- Firmantes y método de identificación.
- Texto y versión del consentimiento.
- Resultado de OCR revisado.
- Attachment ID devuelto por OPERA.
- Eventos de correo.
- Constancia de conservación NOM-151.
- Estado de retención o bloqueo legal.

El GUID identifica de manera única. La fecha indica cuándo se creó el registro. El hash ayuda a detectar alteraciones. Ninguno de ellos sustituye por sí solo una constancia emitida por un Prestador de Servicios de Certificación.

### Auditoría mínima

Cada evento debería contener:

- Identificador único.
- Fecha UTC del servidor.
- Usuario y roles.
- Acción.
- Recurso consultado.
- Reserva y hotel.
- Motivo declarado.
- Dirección IP y datos técnicos permitidos.
- Correlation ID.
- Resultado exitoso o denegado.
- Hash o versión del documento.

Ejemplos de acciones:

```text
ReservationSearched
FileOpened
DocumentViewed
DocumentDownloaded
DocumentExported
SignatureCaptured
DocumentUploadedToOpera
FileSealed
RetentionApplied
LegalHoldApplied
AccessDenied
PermissionChanged
```

Los eventos de auditoría deben ser append-only: una corrección produce un evento nuevo, no modifica el anterior.

---

## 11. Roles sugeridos

| Rol | Responsabilidad |
|---|---|
| Operador | Consultar reservas y preparar documentos |
| Supervisor | Aprobar excepciones y operaciones sensibles |
| Archivo | Consultar expedientes autorizados |
| Auditor | Leer evidencia y trazabilidad, sin modificar |
| Legal/Compliance | Retención, exportación y legal hold |
| Administrador técnico | Configuración y usuarios, sin acceso automático a documentos |
| Seguridad | Eventos, accesos denegados y alertas |

Una cuenta administrativa técnica no debe recibir automáticamente permiso para leer identificaciones y firmas.

---

## 12. OCR

Tesseract puede ser útil como POC, pero el resultado debe ser revisado por una persona.

Flujo recomendado:

1. Validar tamaño y tipo real del archivo.
2. Normalizar orientación y resolución.
3. Eliminar metadatos EXIF cuando corresponda.
4. Procesar frente y reverso.
5. Detectar tipo de identificación.
6. Extraer campos.
7. Validar CURP, vigencia y dígitos MRZ.
8. Mostrar confianza por campo.
9. Permitir corrección humana.
10. Guardar valores confirmados.
11. Aplicar retención a imágenes y texto OCR.

No se debería escribir automáticamente información en OPERA basándose únicamente en OCR.

Si se utilizan contenedores Linux, debe revisarse el uso actual de `System.Drawing.Common`, porque está orientado a Windows. Para portabilidad se puede usar SkiaSharp o ImageSharp.

---

## 13. Firma electrónica y proveedores

Conviene distinguir:

- **Firma dibujada:** imagen del trazo.
- **Firma electrónica:** datos electrónicos asociados al consentimiento y firmante.
- **Firma electrónica avanzada:** requiere características adicionales de atribución, control e integridad.
- **Constancia NOM-151:** evidencia de conservación e integridad del mensaje de datos.

Una imagen PNG, un GUID y un hash no garantizan por sí solos validez jurídica para todos los actos.

Para evitar dependencia de un proveedor:

```csharp
public interface IElectronicSignatureProvider { }
public interface IIdentityVerificationProvider { }
public interface IConservationStampProvider { }
```

Después pueden existir implementaciones:

```text
EasyLexSignatureProvider
DocuSignSignatureProvider
Nom151ConservationProvider
```

La selección debe revisar API, webhooks firmados, evidencias exportables, PSC acreditado, residencia de datos, retención, SLA y responsabilidades contractuales.

---

## 14. Pruebas recomendadas

### Unitarias

- Reglas del dominio.
- Validaciones.
- Hashes y manifiestos.
- Mapeo OCR.
- Permisos.

### Integración

- EF Core con una base controlada.
- Blob Storage mediante emulador o cuenta de pruebas.
- Key Vault mediante adaptadores.
- Cliente OHIP contra un servidor simulado.

### Contrato

- OpenAPI válido.
- DTO C# y cliente Angular sincronizados.
- Respuestas OHIP conocidas.

### E2E

- Login.
- Consulta de reserva.
- Captura de datos.
- Firma.
- Vista previa.
- Generación de PDF.
- Attachment UAT controlado.
- Consulta y descarga del expediente.
- Denegación a usuarios sin permisos.

### Seguridad

- Archivos maliciosos.
- Autorización horizontal y vertical.
- Rate limiting.
- Sesiones expiradas.
- Intentos de acceso a otra propiedad.
- Secret scanning.
- Análisis de dependencias.

---

## 15. CI/CD, GitHub Actions, Docker y Key Vault

### CI/CD

CI, integración continua, valida cada cambio:

```text
Commit / Pull Request
   -> restaurar dependencias
   -> compilar
   -> lint
   -> pruebas
   -> análisis de seguridad
   -> generar artefacto
```

CD, entrega o despliegue continuo, promueve el artefacto:

```text
Artefacto aprobado
   -> DEV
   -> pruebas automáticas
   -> UAT
   -> aprobación humana
   -> Producción
```

### GitHub Actions

Ejecuta el pipeline definido en `.github/workflows/*.yml`.

El pipeline debería:

- Compilar .NET y Angular.
- Ejecutar pruebas.
- Detectar secretos.
- Revisar paquetes vulnerables.
- Generar SBOM.
- Construir imagen Docker si se utiliza.
- Publicar un artefacto versionado.
- Desplegar con identidad OIDC y permisos mínimos.

### Docker

Docker empaqueta la aplicación y sus dependencias. No sustituye a la seguridad ni a la base de datos.

Una compilación multietapa puede usar:

1. Una imagen Node para compilar Angular.
2. Una imagen SDK de .NET para compilar la API.
3. Una imagen pequeña de runtime para ejecutar el resultado.

SQL Server, documentos y secretos permanecen fuera del contenedor.

### Azure Key Vault

Almacena secretos, claves y certificados. La aplicación puede acceder mediante identidad administrada, evitando una contraseña adicional.

Key Vault no debe utilizarse como almacén de contraseñas de usuarios finales. Para usuarios se utiliza Entra ID o ASP.NET Core Identity.

---

## 16. Mantenibilidad y convenciones

- Clases y métodos con una responsabilidad clara.
- Controladores pequeños.
- Casos de uso explícitos.
- Interfaces sólo en límites tecnológicos reales.
- DTO separados de entidades.
- Operaciones asíncronas con `CancellationToken`.
- Fechas en UTC mediante `DateTimeOffset`.
- Errores consistentes con `ProblemDetails`.
- Logging estructurado.
- Correlation ID.
- Nullable habilitado.
- Validación de opciones al iniciar.
- Analizadores y advertencias tratadas de forma estricta.
- Dependencias actualizadas de manera controlada.
- Documentación de decisiones mediante ADR.

No es necesario crear una interfaz para cada clase. Las interfaces son especialmente útiles al cruzar límites como OPERA, almacenamiento, OCR, correo, tiempo, identidad y firma electrónica.

---

## 17. Plan de migración resumido

1. Rotar todas las credenciales expuestas.
2. Construir una copia limpia sin `.git`, logs, binarios ni datos reales.
3. Crear inventario y matriz de paridad funcional.
4. Eliminar tablet, estación y SignalR.
5. Eliminar Blazor cuando Angular tenga paridad aprobada.
6. Organizar backend según Domain, Application, Infrastructure y API.
7. Implementar contratos OpenAPI y cliente Angular generado.
8. Corregir autenticación y autorización.
9. Conectar y validar OPERA Cloud UAT.
10. Implementar auditoría completa.
11. Crear el expediente documental.
12. Incorporar almacenamiento inmutable.
13. Fortalecer OCR con revisión humana.
14. Integrar un proveedor legal mediante adaptadores.
15. Agregar pruebas automatizadas.
16. Crear CI/CD.
17. Documentar despliegue, respaldo, recuperación y rotación.

---

## 18. Referencias oficiales

- [ASP.NET Core Web API](https://learn.microsoft.com/aspnet/core/web-api/)
- [Autorización basada en roles con Microsoft Entra ID](https://learn.microsoft.com/en-us/entra/identity-platform/howto-implement-rbac-for-apps)
- [Seguridad de Angular](https://angular.dev/best-practices/security)
- [Autenticación de Oracle Hospitality APIs](https://docs.oracle.com/en/industries/hospitality/integration-platform/ohipu/c_authenticating_to_oracle_hospitality_property_apis_ocim.htm)
- [Azure Key Vault para ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/key-vault-configuration)
- [Azure Immutable Blob Storage](https://learn.microsoft.com/azure/storage/blobs/immutable-storage-overview)
- [GitHub Actions](https://docs.github.com/actions)
- [Docker multi-stage builds](https://docs.docker.com/build/building/multi-stage/)
- [NOM-151-SCFI-2016](https://sidof.segob.gob.mx/notas/docFuente/5478024)
- [Directorio de Prestadores de Servicios de Certificación](https://psc.economia.gob.mx/directorio.html)
- [Ley Federal de Protección de Datos Personales en Posesión de los Particulares](https://www.diputados.gob.mx/LeyesBiblio/pdf/LFPDPPP.pdf)

---

## Conclusión

La solución no debe verse únicamente como “MVC” ni como “una API con carpetas”. La organización propuesta separa responsabilidades:

- Angular presenta y captura información.
- API protege y publica operaciones HTTP.
- Application ejecuta casos de uso.
- Domain conserva las reglas del negocio.
- Infrastructure conecta tecnologías externas.

Esta separación permite migrar la interfaz, cambiar OCR, incorporar NOM-151, sustituir proveedores de firma y modificar almacenamiento sin reescribir todo el sistema.

