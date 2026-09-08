# Configuración segura: variables de entorno y Azure Key Vault

La solución no incluye `appsettings.json` ni secretos. ASP.NET Core traduce `__` a
secciones: `OperaCloud__ClientSecret` equivale a `OperaCloud:ClientSecret`.
`.env.example` es únicamente un catálogo; la aplicación no lee archivos `.env`.

## Desarrollo con variables del proceso

En PowerShell defina los valores únicamente en la terminal actual:

```powershell
$env:ConnectionStrings__FirmaOperaCloud = '<cadena UAT>'
$env:OperaCloud__GatewayUrl = '<gateway UAT>'
$env:OperaCloud__AppKey = '<app key>'
$env:OperaCloud__ClientId = '<client id>'
$env:OperaCloud__ClientSecret = '<client secret>'
$env:OperaCloud__EnterpriseId = '<enterprise id>'
$env:OperaCloud__Scope = '<scope>'
$env:OperaCloud__DefaultHotelId = 'VINV'
dotnet run --project src/FirmaOperaCloud.Api
```

Al cerrar la terminal desaparecen. No use `setx` en equipos compartidos y no pegue
valores reales en documentación, commits, Dockerfiles o GitHub Actions.

## Azure Key Vault

1. Cree un Key Vault con RBAC y protección contra eliminación.
2. Agregue secretos usando `--` para las secciones, por ejemplo:
   `ConnectionStrings--FirmaOperaCloud`, `OperaCloud--ClientSecret`,
   `OperaCloud--ClientId` y `OperaCloud--AppKey`.
3. Habilite identidad administrada en App Service/Container App.
4. Asigne a esa identidad el rol **Key Vault Secrets User** en el vault.
5. Configure en la aplicación solamente `KEY_VAULT_URI=https://<vault>.vault.azure.net/`.
6. En desarrollo ejecute `az login`; `DefaultAzureCredential` reutilizará esa identidad.

En producción configure `DATA_PROTECTION_KEYS_PATH` hacia un volumen persistente y
compartido por todas las instancias. Esas llaves protegen las cookies de sesión; el
volumen debe cifrarse y restringirse a la identidad de la aplicación. En Azure con
múltiples instancias conviene persistirlas en Blob Storage y protegerlas con Key Vault.

Key Vault tiene costo por operaciones; no debe suponerse gratuito. GitHub Actions es
gratuito dentro de la cuota incluida por GitHub y después puede generar consumo.

## Administrador inicial

El usuario administrador se conserva. En una base existente no se altera. Para una
base nueva, configure temporalmente `BootstrapAdmin__Password` y active la migración;
el valor se convierte a hash PBKDF2 y no se persiste como texto. Retire la variable
después del primer arranque.

## Regla de despliegue

Use identidad federada OIDC entre GitHub y Azure; no almacene un secreto permanente
de Azure en GitHub. Los secretos de OPERA y SQL permanecen en Key Vault y el flujo
de CI/CD nunca necesita leerlos.
