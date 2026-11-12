# Bonos de desempeño — Prueba técnica Full Stack

Módulo de Recursos Humanos para registrar bonos de desempeño: se busca un empleado activo, se le registra un bono (monto fijo o porcentaje del sueldo) para un periodo, y el sistema calcula bruto, retención y neto. Desde el listado se puede pagar (con reparto por proyecto), anular y eliminar.

| Capa | Tecnología |
|---|---|
| Base de datos | SQL Server 2019+ (2022 en Docker) |
| Backend | .NET 10, ASP.NET Core Web API, EF Core 10 |
| Frontend | Angular 22 (standalone) + Angular Material 22 |
| Pruebas | xUnit |

## Cómo levantarlo

### Opción 1: Docker (un comando)

Desde la raíz del repo:

```bash
cp .env.example .env    # opcional: cambia la contraseña del SA (por defecto BonusTest#2026)
docker compose up --build
```

El SQL Server de Docker no publica ningún puerto; antes de levantarlo, detén solo la API o el front locales que usen 5106 o 4200.

En Mac con Apple Silicon, SQL Server corre emulado (activa Rosetta en Docker Desktop).

| Servicio | Qué hace |
|---|---|
| `sql` | SQL Server 2022. No se publica al host. |
| `db-init` | Ejecuta `01` solo si la base `BonusTest` no existe, luego `02` y `03`. Termina con código 0. |
| `api` | http://localhost:5106 — Swagger en http://localhost:5106/swagger (`ASPNETCORE_ENVIRONMENT=Development`). |
| `web` | Front con nginx — http://localhost:4200/bonuses |

Para apagar todo: `docker compose down`. La base de Docker se recrea limpia en el siguiente `up`.

Archivos: `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile`, `frontend/nginx.conf`, `docker/db-init.sh`.

### Opción 2: local

#### 1. Base de datos

Ejecuta los scripts de `database/` en orden:

1. `01_schema_seed.sql` — tablas base y datos de prueba (sin cambios).
2. `02_bonus.sql` — tabla `Bonus` e índices.
3. `03_bonus_payment_detail.sql` — detalle del pago por proyecto.

Los scripts 02 y 03 son idempotentes. Si usas `sqlcmd`, agrega `-I` (los índices filtrados requieren `QUOTED_IDENTIFIER ON`; los scripts ya lo activan).

**Sin SQL Server instalado (Docker):**
```bash
docker run -d --name bonustest-sql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=BonusTest#2026" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
docker cp database/. bonustest-sql:/tmp/db
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "BonusTest#2026" -C -I -i /tmp/db/01_schema_seed.sql
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "BonusTest#2026" -C -I -i /tmp/db/02_bonus.sql
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "BonusTest#2026" -C -I -i /tmp/db/03_bonus_payment_detail.sql
```
(En Git Bash, antepone `MSYS_NO_PATHCONV=1` a los comandos `docker exec`/`docker cp`.)

#### 2. API

`appsettings.Development.json` apunta a `Server=localhost` con autenticación de Windows. Para otra instancia (por ejemplo, Docker), sobrescribe la cadena con una variable de entorno:

```bash
export ConnectionStrings__BonusTest="Server=localhost,1433;Database=BonusTest;User Id=sa;Password=BonusTest#2026;TrustServerCertificate=True;"
cd backend/src/BonusTest.Api
dotnet run --launch-profile http
```
Swagger: http://localhost:5106/swagger

Pruebas: `cd backend && dotnet test`

#### 3. Front

```bash
cd frontend
npm ci
npx ng serve
```
Pantalla: http://localhost:4200/bonuses

## Endpoints

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/api/v1/Bonus?pageIndex&pageSize&departmentCode&employeeNumber&period&bonusStateCode` | 200 listado paginado |
| GET | `/api/v1/Bonus/summary` | 200 `{ pendingCount, pendingNet, paidCount, paidNet, cancelledCount, committedGross, committedTax, committedNet }`. Mismos filtros que el listado. |
| GET | `/api/v1/Bonus/export` | 200 CSV (`text/csv`, `bonos-AAAAMMDD.csv`). Mismos filtros. 409 `EXPORT_TOO_LARGE` si hay más de 10 000 filas. |
| GET | `/api/v1/Bonus/employee/{departmentCode}/{employeeNumber}` | 200 datos del empleado activo |
| GET | `/api/v1/Bonus/{bonusId}/payment-detail` | 200 `{ bonusId, grossAmount, lines: [{ projectName, hours, amount }] }`. 409 `BONUS_NOT_PAID` o `BONUS_NOT_FOUND`. |
| POST | `/api/v1/Bonus` | 201 `{ bonusId, grossAmount, taxAmount, netAmount }` |
| PUT | `/api/v1/Bonus/{bonusId}/cancel` | 204 |
| DELETE | `/api/v1/Bonus/{bonusId}` | 204 (eliminado lógico) |
| POST | `/api/v1/Bonus/{bonusId}/pay` | 204 (extra 1) |

El usuario que opera llega en el header `UserCode`. Los errores de negocio responden 409 con `{ code, description }`.

## Funciones para RR. HH. y Finanzas

- **Resumen por estado:** tarjetas *Por pagar*, *Pagado*, *Anulados* y *Total comprometido*. Siguen los filtros aplicados y se actualizan después de cada acción (pagar, anular, registrar).
- **Exportar CSV para planilla:** botón "Exportar CSV" con los filtros actuales. Si no hay resultados, avisa en lugar de descargar un archivo vacío. Formato: UTF-8 con BOM, separador coma, decimales con punto, campos entre comillas cuando hace falta. Los valores que empiezan con `=`, `+`, `-` o `@` llevan un `'` al inicio (protección contra inyección de fórmulas).
- **Detalle del pago:** acción "Ver detalle del pago" en los bonos Pagados, con tabla Proyecto / Horas / Monto y total.

## Decisiones técnicas

- **Reglas en Domain y Application, no en el controlador.** `BonusCalculator` y `BonusDistributor` son funciones puras; la entidad `Bonus` solo cambia de estado con `Cancel`, `Delete` y `Pay`, que exigen el estado Registrado. `BonusService` aplica las validaciones que requieren datos (empleado activo, motivo activo, duplicados, periodo, límites del valor).
- **Dinero con `decimal` y redondeo `MidpointRounding.AwayFromZero`** (0.125 → 0.13). El redondeo bancario por defecto de .NET daría 0.12.
- **Montos guardados al registrar** (`GrossAmount`, `TaxAmount`, `NetAmount`) junto con `TaxRateApplied`: el bono conserva lo calculado aunque luego cambien el sueldo o la tasa del empleado, y el listado no recalcula.
- **Un bono activo por periodo, validado dos veces:** en el servicio (mensaje claro) y con el índice único filtrado `UX_Bonus_ActivePerPeriod` (`WHERE IsDeleted = 0 AND BonusStateCode <> '0203'`), que cubre registros concurrentes; esa violación también se traduce a 409.
- **Tres índices en `Bonus`:** `UX_Bonus_ActivePerPeriod` (único, solo bonos activos), `IX_Bonus_Employee` (filtro por empleado en el listado, que incluye anulados y pagados) e `IX_Bonus_Period_State` (filtros por periodo y estado). Además, checks que limitan el estado a los de bono (`02xx`) y la retención a 0–100.
- **Paginación en la base de datos:** EF Core traduce `Skip/Take` a `OFFSET/FETCH`; primero se filtra y cuenta, luego se une la página con los catálogos para devolver las descripciones.
- **Listado, resumen y exportación comparten la misma consulta filtrada.** El resumen es una sola consulta `GROUP BY` en la base de datos. El CSV y el cálculo del resumen son funciones puras en Application, sin paquetes NuGet nuevos.
- **Tipos `char`/`varchar` mapeados en EF** para que los parámetros no se envíen como `nvarchar` y se usen los índices.
- **Fecha actual vía `TimeProvider`** para probar la regla "periodo no posterior al mes actual" sin depender del reloj. El "mes actual" se evalúa con el reloj del servidor (hora local del servidor).
- **Pago en una transacción explícita:** detalle por proyecto + paso a Pagado. Cada línea se redondea y el último proyecto recibe la diferencia para cuadrar exacto; solo se reparten proyectos con horas > 0.
- **Concurrencia optimista:** `BonusStateCode` e `IsDeleted` son tokens de concurrencia en EF Core; si dos usuarios pagan, anulan o eliminan el mismo bono a la vez, el segundo recibe 409 en lugar de duplicar el pago.
- **Docker:** `db-init` crea el esquema solo si la base no existe; los scripts 02 y 03 son idempotentes, así que un `up` repetido no rompe nada.
- **Front:** Angular Material (tabla, paginador, diálogos, formularios reactivos), estado con signals, `switchMap` para descartar respuestas de páginas antiguas. La vista previa replica la fórmula, pero el monto oficial lo calcula la API.
- **Avisos sobre el modal:** Angular Material 22 abre los diálogos en la capa superior del navegador (Popover API); el aviso global también se muestra como popover para quedar encima del modal. Los avisos de éxito se cierran solos a los 4 segundos.

## Pruebas

`backend/tests/BonusTest.UnitTests` (xUnit): 71 pruebas unitarias sobre cálculo y redondeo, transiciones de estado, reparto por proyecto y validaciones de `BonusService` con un repositorio en memoria.

## Pendientes / fuera de alcance

- Autenticación real (el usuario llega en el header `UserCode`).
- Edición de bonos y reversión de pagos.
- Pruebas de integración de la API contra SQL Server y pruebas del front.
- Prueba automatizada de concurrencia del pago (se verificó manualmente con peticiones simultáneas).
- Vulnerabilidades reportadas por `npm audit` en dependencias de desarrollo del proyecto base, sin revisar.
