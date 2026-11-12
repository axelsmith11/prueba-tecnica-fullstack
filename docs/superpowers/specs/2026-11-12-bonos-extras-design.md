# Bonos de desempeño — Extras de negocio y despliegue — Diseño

Fecha: 2026-11-12 · Rama: `feat/bonos-desempeno` · Base: módulo completo (spec `2026-11-12-bonos-desempeno-design.md`).

## Objetivo

Reducir el trabajo manual de RR. HH. y Finanzas y facilitar la evaluación:

| Id | Mejora | Dolor que resuelve |
|---|---|---|
| A | Docker Compose de un comando | El evaluador levanta todo sin instalar SQL Server, .NET ni Node |
| B | Resumen por estado (tarjetas KPI) | Saber cuánto falta pagar y cuánto se pagó sin sumar a mano |
| C | Exportar CSV del listado filtrado | Pasar los bonos a planilla sin copiar a mano |
| D | Ver detalle del pago por proyecto | Imputación contable del bono por proyecto |

Restricciones: no cambiar el stack ni la estructura de capas; reglas fuera del controlador; sin dependencias nuevas de NuGet/npm; `appsettings*.json` y `environment.ts` sin cambios.

## B. Resumen

**Endpoint:** `GET /api/v1/Bonus/summary?departmentCode&employeeNumber&period&bonusStateCode` → 200 `BonusSummaryDto`.

- Mismos filtros que el listado (reutiliza `BonusFilter` y el filtrado del repositorio), sobre bonos no eliminados.
- El repositorio ejecuta una sola consulta agregada: `GROUP BY BonusStateCode` → `BonusStateTotals(string BonusStateCode, int Count, decimal GrossAmount, decimal TaxAmount, decimal NetAmount)`.
- `BonusSummaryCalculator.Build(IReadOnlyList<BonusStateTotals>)` (Application, función pura) arma:

```
BonusSummaryDto(
  int PendingCount, decimal PendingNet,        // Registrado (0201)
  int PaidCount, decimal PaidNet,              // Pagado (0202)
  int CancelledCount,                          // Anulado (0203)
  decimal CommittedGross, decimal CommittedTax, decimal CommittedNet)  // Registrado + Pagado
```

Los anulados no suman dinero. Estados ausentes → 0.

## C. Exportar CSV

**Endpoint:** `GET /api/v1/Bonus/export?<mismos filtros>` → 200 `text/csv; charset=utf-8`, `Content-Disposition: attachment; filename="bonos-AAAAMMDD.csv"` (fecha del servidor vía `TimeProvider`).

- Todas las filas filtradas (no solo la página), ordenadas por `BonusId` descendente, con tope `MaxExportRows = 10000`; si el filtro supera el tope → 409 `EXPORT_TOO_LARGE` ("Hay más de 10 000 bonos con estos filtros; acota la búsqueda para exportar.").
- El repositorio reutiliza la proyección del listado (`ToListItems`) — sin duplicar la consulta.
- `BonusCsvWriter.Write(IReadOnlyList<BonusListItemDto>) : string` (Application, puro):
  - Encabezado: `Estado,Periodo,Área,N.º empleado,Nombre,Tipo,Monto o %,Motivo,Bruto,Retención,Neto`.
  - Decimales con `CultureInfo.InvariantCulture` y formato `0.00`; el porcentaje sin símbolo (columna "Monto o %").
  - Campos con coma, comilla, CR o LF van entre comillas dobles y las comillas internas se duplican.
  - Saltos de línea `\r\n`.
- El controlador devuelve `File(bytes, "text/csv; charset=utf-8", nombre)` con los bytes UTF-8 **con BOM** (preámbulo) para que Excel muestre acentos; separador coma (Excel es-PE). Sin línea `sep=`.

## D. Detalle del pago

**Endpoint:** `GET /api/v1/Bonus/{bonusId}/payment-detail` → 200 `BonusPaymentDetailDto(int BonusId, decimal GrossAmount, IReadOnlyList<PaymentLineDto> Lines)` con `PaymentLineDto(string ProjectName, decimal Hours, decimal Amount)` ordenadas por `BonusPaymentDetailId`.

- Bono inexistente o eliminado → 409 `BONUS_NOT_FOUND`; bono que no está Pagado → 409 `BONUS_NOT_PAID` ("El bono aún no ha sido pagado; no tiene detalle por proyecto.").
- La suma de `Lines.Amount` es igual a `GrossAmount` (garantizado por el reparto).

## Front

- **Tarjetas de resumen** (`features/bonuses/bonus-summary/`, componente standalone con `input()`): Por pagar (cantidad + neto), Pagado (cantidad + neto), Anulados (cantidad), Total comprometido (bruto, retención, neto). Se recargan con el filtro aplicado y tras cada acción; esqueleto mientras cargan; grilla responsiva (4 → 2 → 1 columnas).
- **Exportar CSV**: botón junto a Buscar/Limpiar; descarga con el filtro aplicado (Blob + enlace temporal); deshabilitado mientras descarga; si el total actual es 0, aviso "No hay bonos para exportar con estos filtros." sin llamar a la API.
- **Ver detalle** (ícono `receipt_long`, solo filas Pagado, `aria-label` con nombre y periodo): modal `PaymentDetailDialog` con tabla Proyecto / Horas / Monto y fila Total; se abre con el inyector del componente.
- `BonusApi`: `summary(filter)`, `export(filter): Observable<Blob>`, `paymentDetail(id)`.

## A. Docker Compose

`docker-compose.yml` en la raíz + `.env.example` (`MSSQL_SA_PASSWORD=BonusTest#2026`; `.env` en `.gitignore`):

| Servicio | Imagen / build | Detalle |
|---|---|---|
| `sql` | `mcr.microsoft.com/mssql/server:2022-latest` | `ACCEPT_EULA=Y`; healthcheck con `/opt/mssql-tools18/bin/sqlcmd ... -Q "SELECT 1"`; sin puerto publicado |
| `db-init` | misma imagen de SQL (trae `sqlcmd`) | `depends_on: sql (healthy)`; monta `./database` en solo lectura; corre `01`, `02`, `03` con `-C -I -b` en orden; termina con código 0 |
| `api` | `backend/Dockerfile` (sdk:10.0 → aspnet:10.0) | `ASPNETCORE_ENVIRONMENT=Development` (Swagger + CORS de Development), `ASPNETCORE_URLS=http://+:8080`, `ConnectionStrings__BonusTest=Server=sql;...`; puerto `5106:8080`; `depends_on: db-init (completed_successfully)` |
| `web` | `frontend/Dockerfile` (node LTS soportada por Angular 22 según `engines` de `@angular/core` → nginx) | `npm ci` + `ng build`; nginx sirve `dist/frontend/browser` con fallback a `index.html`; puerto `4200:80` |

- El navegador sigue usando `http://localhost:5106/api/v1` y la API acepta `http://localhost:4200`: no cambia configuración de la app.
- `.dockerignore` en `backend/` y `frontend/` (bin, obj, node_modules, dist, .angular).
- README: "Opción 1: Docker (un comando)" y "Opción 2: local"; endpoints y funciones nuevas.

## Pruebas y verificación

- xUnit (TDD): `BonusSummaryCalculator` (mezcla de estados, anulados no suman, estados ausentes), `BonusCsvWriter` (encabezado, escape de coma/comilla/salto, decimales invariantes, porcentaje), `BonusService` (detalle: no encontrado, no pagado, pagado devuelve líneas; export: tope).
- `dotnet build` 0 advertencias, `dotnet test` verde, `ng build` producción sin avisos de presupuesto.
- Navegador: tarjetas cambian con filtros y tras pagar/anular; CSV descargado abre con acentos; detalle cuadra con el bruto.
- `docker compose up --build` desde cero (con servidores de desarrollo detenidos): `db-init` termina 0, `http://localhost:4200/bonuses` y `http://localhost:5106/swagger` responden.

## Fuera de alcance

Registro masivo, exportación .xlsx, autenticación, gráficos.
