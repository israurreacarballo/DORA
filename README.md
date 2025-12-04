# DevOps Metrics (fork personalizado)

Este proyecto recoge e informa las principales métricas DORA (Deployment Frequency, Lead Time for Changes, Mean Time To Restore y Change Failure Rate) para proyectos alojados en Azure DevOps y GitHub Actions.

Resumen de la funcionalidad
- Recolecta y consolida datos de builds, pull requests y alertas para calcular las cuatro métricas DORA.
- Expone un servicio web (API) que procesa datos y guarda resultados en Azure Table Storage.
- Ofrece una interfaz web (Razor MVC) que muestra las métricas por proyecto y por métrica, con imágenes/badges y vistas de detalle por proyecto.
- Permite actualizar/forzar recálculos de métricas por proyecto y mantener registros (logs) de procesamiento.

Cambios relevantes en esta copia
- Cliente HTTP renombrado a `IsraServiceClient` e introducida la interfaz `IServiceApiClient` para facilitar pruebas y desacoplar la llamada al servicio.
- Uso de `IHttpClientFactory` (AddHttpClient) para gestionar `HttpClient` correctamente y evitar problemas de sockets.
- Refactor en `HomeController` para reducir duplicación (helpers para listas y construcción de view models).

Requisitos previos
- Cuenta de Azure con permisos para crear recursos.
- .NET 8 SDK instalado para ejecutar y compilar los proyectos web y de servicio.
- Valores de configuración: URL del servicio, cadenas de conexión a Storage/KeyVault y credenciales de GitHub (si se usan integraciones).

Claves de configuración (ejemplos de AppSettings)
- `AppSettings:WebServiceURL` — URL base del servicio (p. ej. `https://mi-servicio.azurewebsites.net`).
- `AppSettings:KeyVaultURL` — URL del Key Vault usado para secretos.
- `APPINSIGHTS_CONNECTIONSTRING` — Connection string para Application Insights (opcional).
- `AppSettings:GitHubClientId` y `AppSettings:GitHubClientSecret` — credenciales si se consultan APIs de GitHub desde el cliente web.
- `AppSettings:AzureStorageAccountConfigurationString` — cadena de conexión a Azure Storage usada por la API/DA.

Despliegue en Azure (pasos resumidos)
1. **Preparar la infraestructura**
   - Puedes ejecutar el script de infraestructura incluido (cuando exista) para crear el resource group, Storage Account, App Service, Function App y Key Vault. En el repositorio canónico este script se menciona en `src/DevOpsMetrics.Infrastructure/DeployInfrastructureToAzure2.ps1`.
   - Si no usas el script, crea manualmente:
     - Un Resource Group.
     - Azure Storage Account y la tabla necesaria (o dejar que la API la cree).
     - Un App Service (Web App) para `DevOpsMetrics.Service` (API).
     - Otro App Service (Web App) para `DevOpsMetrics.Web` (front-end) o desplegar front-end estático según prefieras.
     - (Opcional) Azure Function para recolectar alertas si se usa MTTR desde Azure Monitor.
     - Key Vault para almacenar PATs/secretos y las credenciales de GitHub.

2. **Configurar secretos y app settings**
   - Guardar los secretos (PAT de Azure DevOps, GitHub client id/secret) en Key Vault o en la configuración de la App (recomendado: Key Vault para producción).
   - Establecer las App Settings en las Web Apps/Function App:
     - `AppSettings:WebServiceURL` (por ejemplo, la URL pública del servicio si el front apunta al servicio desplegado).
     - `AppSettings:KeyVaultURL` (URI del Key Vault).
     - `APPINSIGHTS_CONNECTIONSTRING` (si se activa Application Insights).
     - `AppSettings:GitHubClientId`, `AppSettings:GitHubClientSecret` (si procede).
     - Azure Storage connection string usado por el servicio.

3. **Publicar la API (`DevOpsMetrics.Service`)**
   - Desde Visual Studio / CLI: publicar el proyecto `DevOpsMetrics.Service` al App Service creado.
     - dotnet publish y usar `az webapp deploy` o el comando de VS.
   - Asegúrate de que el App Setting `AppSettings:KeyVaultURL` esté presente y que la identidad del App Service tenga acceso al Key Vault (Managed Identity recomendado).

4. **Publicar el front-end (`DevOpsMetrics.Web`)**
   - Publicar `DevOpsMetrics.Web` al App Service destinado al sitio web.
   - Configura `AppSettings:WebServiceURL` para que apunte a la API publicada.

5. **Alternativa: CI/CD con GitHub Actions**
   - El repositorio original incluye pipelines / workflows para build y despliegue. Puedes conectar los workflows a tus App Services y usar secrets en GitHub para las credenciales.
   - Asegúrate de exponer las variables necesarias (connection strings y URIs) como GitHub secrets utilizadas por los workflows.

Ejecución local (desarrollo)
1. Configura user-secrets o `appsettings.Development.json` con las claves indicadas arriba.
2. Abre la solución en Visual Studio o ejecuta:
   - dotnet build
   - dotnet run --project src/DevOpsMetrics.Service
   - dotnet run --project src/DevOpsMetrics.Web
3. Abre el front-end en el navegador y configura proyectos desde la página de Settings.

Notas operativas
- La solución usa Azure Table Storage para datos operativos y resultados agregados. Asegúrate de que la cadena de conexión sea correcta y que la cuenta de almacenamiento esté en la misma región, si te interesa optimización.
- `IServiceApiClient` permite sustituir la implementación real por mocks en pruebas unitarias.

Soporte y siguientes pasos
- Si quieres puedo:
  - Añadir scripts `az cli` concretos para automatizar recursos mínimos.
  - Añadir políticas de retry/timeouts con Polly para `IsraServiceClient`.
  - Añadir plantillas ARM/Bicep para el despliegue en Azure.

License
- Ver la licencia original del proyecto (si aplica) en el repositorio original.

---
Esta README describe el funcionamiento y una guía de despliegue básica. Si quieres que genere comandos `az` concretos o un archivo Bicep/ARM para provisión, indícalo y lo preparo.
