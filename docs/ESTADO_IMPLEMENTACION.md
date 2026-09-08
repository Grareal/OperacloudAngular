# Estado de la primera implementación

## Completado

- Copia limpia preparada sin historial Git, `appsettings.json`, `bin`, `obj`,
  `node_modules` ni valores secretos.
- Solución en .NET 10 y Angular 21; Blazor, tablet, estación y SignalR retirados del
  código ejecutable.
- Angular servido por ASP.NET al publicar, proxy local y cliente OpenAPI generado.
- Sesión BFF con cookie HttpOnly, CSRF, encabezados de seguridad y sin JWT en
  `localStorage`.
- Variables de entorno y Azure Key Vault con identidad administrada.
- Políticas por permiso para expedientes, firmas, OCR, plantillas, comunicaciones,
  correo, promociones y tarjetas.
- Expediente versionado con GUID v7, manifiesto SHA-256, estado abierto/sellado y
  migración SQL no destructiva.
- Auditoría append-only con cadena de hashes, usuario, IP, motivo, correlación,
  resultado y pantalla Angular de consulta/verificación.
- OCR JPEG/PNG: límite, firma mágica, Tesseract, INE/pasaporte, CURP, MRZ, confianza
  por campo, revisión humana obligatoria y fecha de retención.
- CI, despliegue UAT manual con OIDC, Dockerfile, Compose y documentación.
- Compilación .NET y Angular correctas; tres pruebas unitarias correctas.

## Requiere ambiente o decisión posterior


- No se ejecutó la migración contra UAT ni se realizaron llamadas reales a OPERA.
  Primero debe revisarse el script, hacer respaldo y configurar credenciales.
- Las tablas históricas de tablet/estación se conservan físicamente en la base para
  evitar pérdida de evidencia; el código ya no las utiliza. Su eliminación requiere
  autorización de retención y una migración destructiva independiente.
- El preprocesamiento OCR usa `System.Drawing` y requiere Windows. Antes de operar el
  contenedor Linux debe migrarse a una biblioteca multiplataforma y probar Tesseract.
- DocuSign/EasyLex, sellado de tiempo certificado, e.firma y cumplimiento NOM no se
  implementaron: requieren selección de proveedor, dictamen jurídico, consentimiento,
  políticas de conservación y contrato. El GUID/UTC/hash/manifiesto implementado da
  trazabilidad técnica, pero por sí solo no vuelve legal una firma.
- La inmutabilidad actual es lógica y detectable por hash. Para nivel regulatorio se
  recomienda almacenamiento WORM/immutable Blob, llaves HSM y sellado de tiempo de un
  tercero confiable.
