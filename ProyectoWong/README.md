# ProyectoWong (SIV)

Sistema de monitoreo de la línea del mini ventilador: compras, recepción, inventario,
producción, inspección visual, pruebas con sensores (Arduino), scrap y downtime.

ASP.NET Core MVC (.NET 10) + Entity Framework Core + SQL Server.

## Puesta en marcha

1. **Cadena de conexión.** `appsettings.json` trae la de una laptop del equipo. Para usar tu propio
   SQL Server, guárdala como *user secret* (no se sube al repositorio y tiene prioridad en Development):

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=monitoreo;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   ```

2. **Base de datos.** Crea o actualiza las tablas con las migraciones:

   ```bash
   dotnet ef database update
   ```

3. **Usuario administrador.** Si la tabla `Usuarios` está vacía, al arrancar se crea un admin.
   Puedes fijar sus datos antes del primer arranque:

   ```bash
   dotnet user-secrets set "AdminInicial:Email" "admin@wong.com"
   dotnet user-secrets set "AdminInicial:Password" "tu-contraseña"
   ```

   Si no defines la contraseña, se genera una aleatoria y se imprime una sola vez en la consola.

4. **Sensores.** El Arduino (`Arduino/SensoresWong`) se lee por el puerto de `Sensores:PuertoCom`
   (COM3 por defecto). Si no está conectado, pon `Sensores:Habilitado` en `false`.

5. Ejecuta `dotnet run` y entra a la URL que aparece en la consola.

## Flujo de una orden de producción

`Pendiente` → `EnProceso` (⇄ `Pausada`) → `EnInspeccion` → `EnPruebasTecnicas` → `Completada`

- Al terminar la fabricación (botón o automáticamente al cumplirse el tiempo) se descuentan los
  componentes y se suma el producto terminado.
- La inspección visual registra `CantidadAprobada`/`CantidadRechazada`; solo las piezas aprobadas
  pasan a pruebas técnicas, que llevan sus propios contadores (`PruebasAprobadas`/`PruebasRechazadas`).
- Los números de orden empiezan con `WO-`.
