# CI/CD, GitHub Actions y Docker en esta solución

## Flujo implementado

`ci.yml` se ejecuta en cada pull request y push a `main`: restaura .NET, compila,
ejecuta pruebas, instala Angular con el lockfile, compila el SPA y publica un
artefacto. CI significa integrar y verificar cada cambio automáticamente.

`deploy-uat.yml` es manual y usa el ambiente protegido `uat`. Autentica GitHub contra
Azure mediante OIDC, sin contraseña permanente. CD toma el artefacto aprobado y lo
despliega. Deben existir las variables de repositorio `AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` y `AZURE_WEBAPP_NAME`; estas son
identificadores, no las credenciales de OPERA.

La aplicación en Azure recibe `KEY_VAULT_URI`; su identidad administrada consulta SQL,
OPERA y SMTP en Key Vault. GitHub no necesita conocer esos secretos.

## Docker

`Dockerfile` compila Angular en una etapa Node, publica ASP.NET en una etapa SDK y
ejecuta el resultado como usuario sin privilegios en la imagen runtime. `compose.yaml`
sirve para una prueba local:

```powershell
docker compose build
docker compose up
```

Compose puede interpolar variables desde el entorno o un `.env` local que está
ignorado por Git. Ese archivo nunca debe copiarse al repositorio. El volumen
`data-protection-keys` mantiene válidas las cookies entre reinicios.

Importante: el preprocesamiento OCR actual usa `System.Drawing` y fue heredado para
Windows. La compilación del contenedor Linux está preparada, pero OCR debe probarse y
migrarse a una biblioteca de imágenes multiplataforma antes de declarar ese
contenedor apto para producción.

## Key Vault y costos

Key Vault cobra por operaciones y almacenamiento según región/modalidad. GitHub
Actions incluye minutos/almacenamiento según el plan y puede cobrar excedentes. Por
eso ambos deben presupuestarse; no se consideran incondicionalmente gratuitos.
