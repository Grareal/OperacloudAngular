# Validación controlada de OPERA Cloud UAT

La aplicación no incluye credenciales. Después de configurar las variables o Key
Vault, un administrador puede validar sin escribir en OPERA:

1. Iniciar sesión en Angular.
2. Ejecutar `GET /api/opera-uat-validation/authentication` para comprobar OAuth.
3. Ejecutar `GET /api/opera-uat-validation/reservation/{confirmacion}` con una reserva
   de prueba autorizada.
4. Revisar la pantalla Auditoría y el `correlationId` correspondiente.

Ambos endpoints se bloquean si el host configurado no contiene `oc-test.com` y nunca
devuelven el access token. La carga de PDFs y el alta de acompañantes sí modifican
OPERA; requieren una prueba posterior con reserva UAT, respaldo y autorización
operativa. En esta migración no se ejecutó ninguna escritura externa.
