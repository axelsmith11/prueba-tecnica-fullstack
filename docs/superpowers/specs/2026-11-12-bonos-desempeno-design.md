# Módulo de Bonos de desempeño — Diseño

Fecha: 2026-11-12 · Alcance: parte obligatoria + extra 1 (pago y reparto por proyecto) + extra 2 (pruebas unitarias).

## 1. Objetivo

El usuario de RR. HH. busca un empleado activo, le registra un bono para un periodo (monto fijo o porcentaje del sueldo) con un motivo, y el sistema calcula bruto, retención y neto. Desde un listado paginado en servidor puede anular, eliminar (lógico) y pagar bonos en estado Registrado.

Restricciones del enunciado: no cambiar el stack (.NET 10 + EF Core, Angular 22 standalone, SQL Server) ni la estructura de capas (Domain, Application, Infrastructure, Api). Ninguna regla de negocio en el controlador. El script `01_schema_seed.sql` no se modifica.

## 2. Decisiones principales

| Decisión | Elección | Motivo |
|---|---|---|
| Ubicación de reglas | Domain (cálculo, transiciones) + Application (validaciones que consultan datos) | Reglas puras testeables sin BD; sigue las capas del proyecto base |
| Acceso a datos | EF Core con LINQ, proyección directa a DTO | Mismo patrón que `CatalogRepository`; `Skip/Take` se traduce a `OFFSET/FETCH` |
| Montos | Se calculan al registrar y se guardan | Histórico fiel aunque cambie el sueldo o la tasa; el listado no recalcula |
| Redondeo | `Math.Round(x, 2, MidpointRounding.AwayFromZero)` con `decimal` | “Medio hacia arriba” (0.125 → 0.13) sin errores de punto flotante |
| Bono activo único | Validación en servicio + índice único filtrado | Mensaje claro (409) y garantía ante concurrencia |
| Fecha actual | `TimeProvider` inyectado | Probar la regla del periodo sin depender del reloj |
| UI | Angular Material 22.2.7 | Tabla, paginador, diálogos y formularios listos y accesibles |

## 3. Base de datos

### `database/02_bonus.sql`

```sql
CREATE TABLE dbo.Bonus (
    BonusId         INT IDENTITY(1,1) CONSTRAINT PK_Bonus PRIMARY KEY,
    DepartmentCode  CHAR(5)        NOT NULL,
    EmployeeNumber  INT            NOT NULL,
    Period          CHAR(7)        NOT NULL,   -- YYYY-MM
    BonusTypeId     INT            NOT NULL,
    BonusValue      DECIMAL(16,2)  NOT NULL,   -- monto o porcentaje ingresado
    ReasonCode      CHAR(6)        NOT NULL,
    TaxRateApplied  DECIMAL(5,2)   NOT NULL,   -- tasa del empleado al registrar
    GrossAmount     DECIMAL(16,2)  NOT NULL,
    TaxAmount       DECIMAL(16,2)  NOT NULL,
    NetAmount       DECIMAL(16,2)  NOT NULL,
    BonusStateCode  CHAR(4)        NOT NULL CONSTRAINT DF_Bonus_State DEFAULT ('0201'),
    IsDeleted       BIT            NOT NULL CONSTRAINT DF_Bonus_IsDeleted DEFAULT (0),
    CreatedBy       VARCHAR(50)    NOT NULL,
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Bonus_CreatedAt DEFAULT (SYSDATETIME()),
    UpdatedBy       VARCHAR(50)    NULL,
    UpdatedAt       DATETIME2(0)   NULL,
    CONSTRAINT FK_Bonus_Employee    FOREIGN KEY (DepartmentCode, EmployeeNumber) REFERENCES dbo.Employee (DepartmentCode, EmployeeNumber),
    CONSTRAINT FK_Bonus_BonusType   FOREIGN KEY (BonusTypeId)    REFERENCES dbo.BonusType (BonusTypeId),
    CONSTRAINT FK_Bonus_BonusReason FOREIGN KEY (ReasonCode)     REFERENCES dbo.BonusReason (ReasonCode),
    CONSTRAINT FK_Bonus_State       FOREIGN KEY (BonusStateCode) REFERENCES dbo.State (StateCode),
    CONSTRAINT CK_Bonus_Period      CHECK (Period LIKE '[12][0-9][0-9][0-9]-[01][0-9]' AND SUBSTRING(Period, 6, 2) BETWEEN '01' AND '12'),
    CONSTRAINT CK_Bonus_BonusValue  CHECK (BonusValue > 0),
    CONSTRAINT CK_Bonus_Amounts     CHECK (GrossAmount > 0 AND TaxAmount >= 0 AND NetAmount = GrossAmount - TaxAmount)
);

-- Un solo bono activo (no anulado y no eliminado) por empleado y periodo.
CREATE UNIQUE INDEX UX_Bonus_ActivePerPeriod
    ON dbo.Bonus (DepartmentCode, EmployeeNumber, Period)
    WHERE IsDeleted = 0 AND BonusStateCode <> '0203';

-- Filtros del listado por periodo y estado sobre registros visibles.
CREATE INDEX IX_Bonus_Period_State
    ON dbo.Bonus (Period, BonusStateCode)
    WHERE IsDeleted = 0;
```

El filtro por empleado usa el índice único anterior (su prefijo es `DepartmentCode, EmployeeNumber`).

### `database/03_bonus_payment_detail.sql` (extra 1)

```sql
CREATE TABLE dbo.BonusPaymentDetail (
    BonusPaymentDetailId INT IDENTITY(1,1) CONSTRAINT PK_BonusPaymentDetail PRIMARY KEY,
    BonusId      INT            NOT NULL CONSTRAINT FK_BonusPaymentDetail_Bonus REFERENCES dbo.Bonus (BonusId),
    ProjectName  VARCHAR(100)   NOT NULL,
    Hours        DECIMAL(8,2)   NOT NULL,
    Amount       DECIMAL(16,2)  NOT NULL,
    CreatedBy    VARCHAR(50)    NOT NULL,
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_BonusPaymentDetail_CreatedAt DEFAULT (SYSDATETIME())
);
CREATE INDEX IX_BonusPaymentDetail_BonusId ON dbo.BonusPaymentDetail (BonusId);
```

Los scripts son idempotentes (`IF OBJECT_ID(...) IS NULL`).

## 4. Domain

- `Entities/Bonus.cs`: propiedades de la tabla.
  - `static Bonus Create(Employee employee, string period, int bonusTypeId, decimal bonusValue, string reasonCode, string userCode, DateTime now)` → calcula montos, estado `0201`, auditoría de creación.
  - `Cancel(userCode, now)`: requiere `0201`, pasa a `0203`.
  - `Delete(userCode, now)`: requiere `0201`, `IsDeleted = true`.
  - `MarkAsPaid(userCode, now)`: requiere `0201`, pasa a `0202`.
  - Transición inválida → `BusinessException("BONUS_NOT_REGISTERED", ...)`.
- `Entities/BonusPaymentDetail.cs`.
- `Services/BonusCalculator.cs` (estático):
  - `Bruto = tipo 1 ? valor : Round(sueldo × valor / 100)`
  - `Retención = Round(Bruto × TaxRate / 100)`
  - `Neto = Bruto − Retención`
  - Devuelve `record BonusAmounts(decimal Gross, decimal Tax, decimal Net)`.
- `Services/BonusDistributor.cs` (estático): `Distribute(decimal gross, IReadOnlyList<(string Project, decimal Hours)>)`. Cada línea `Round(gross × horas / totalHoras)`; la última recibe `gross − suma anteriores`; un solo proyecto recibe todo. Sin proyectos o total de horas 0 → `BusinessException("EMPLOYEE_WITHOUT_PROJECTS", ...)`.

## 5. Application (`Bonuses/`)

- DTOs:
  - `BonusFilter : PaginationRequest` — `DepartmentCode?`, `EmployeeNumber?`, `Period?`, `BonusStateCode?`.
  - `BonusListItemDto` — `BonusId, BonusStateCode, StateDescription, Period, DepartmentCode, DepartmentName, EmployeeNumber, FullName, BonusTypeId, BonusTypeDescription, BonusValue, ReasonDescription, GrossAmount, TaxAmount, NetAmount`.
  - `EmployeeLookupDto` — `DepartmentCode, DepartmentName, EmployeeNumber, FullName, HireDate, MonthlySalary, TaxRate`.
  - `RegisterBonusRequest` — `DepartmentCode, EmployeeNumber, Period, BonusTypeId, BonusValue, ReasonCode`.
- `IBonusRepository`: `GetPagedAsync`, `GetEmployeeAsync`, `IsReasonActiveAsync`, `BonusTypeExistsAsync`, `HasActiveBonusAsync`, `GetByIdAsync`, `GetProjectHoursAsync`, `Add`, `SaveChangesAsync`, `SavePaymentAsync` (transacción).
- `BonusService`:
  - `SearchAsync(filter)` → `PagedResponse<BonusListItemDto>`.
  - `GetActiveEmployeeAsync(departmentCode, employeeNumber)`.
  - `RegisterAsync(request, userCode)` — orden de validación: usuario, periodo (formato y no futuro), empleado existe y activo, tipo existe, motivo activo, rango del valor, sin bono activo en el periodo.
  - `CancelAsync`, `DeleteAsync`, `PayAsync(bonusId, userCode)`.

### Códigos de error (409)

| Código | Cuándo |
|---|---|
| `USER_REQUIRED` | Falta el header `UserCode` |
| `EMPLOYEE_NOT_FOUND` | No existe el empleado |
| `EMPLOYEE_INACTIVE` | Empleado Cesado |
| `INVALID_PERIOD` | Formato distinto de `YYYY-MM` |
| `FUTURE_PERIOD` | Periodo posterior al mes actual |
| `INVALID_BONUS_TYPE` | Tipo inexistente |
| `INACTIVE_REASON` | Motivo inexistente o inactivo |
| `INVALID_AMOUNT` | Monto fijo ≤ 0 o > sueldo |
| `INVALID_PERCENTAGE` | Porcentaje ≤ 0 o > 100 |
| `DUPLICATE_BONUS` | Ya hay un bono activo en el periodo |
| `BONUS_NOT_FOUND` | Bono inexistente o eliminado |
| `BONUS_NOT_REGISTERED` | Acción sobre un bono que no está en Registrado |
| `EMPLOYEE_WITHOUT_PROJECTS` | Pago sin horas por proyecto |

## 6. Infrastructure

- `BonusConfiguration`, `BonusPaymentDetailConfiguration` (precisiones decimales, claves, FK compuesta).
- `DbSet<Bonus>`, `DbSet<BonusPaymentDetail>` en `AppDbContext`.
- `BonusRepository.GetPagedAsync`: `IQueryable` con `!IsDeleted` + filtros opcionales → `CountAsync` → `OrderByDescending(BonusId)` → `Skip((PageIndex-1)×PageSize).Take(PageSize)` → proyección con joins.
- `SavePaymentAsync`: `BeginTransactionAsync` → agrega detalle → `SaveChangesAsync` → `CommitAsync`.
- Registro de `TimeProvider.System` como singleton.

## 7. Api — `BonusController`

| Método | Ruta | Respuesta |
|---|---|---|
| GET | `/api/v1/Bonus?pageIndex&pageSize&departmentCode&employeeNumber&period&bonusStateCode` | 200 `PagedResponse<BonusListItemDto>` |
| GET | `/api/v1/Bonus/employee/{departmentCode}/{employeeNumber}` | 200 `EmployeeLookupDto` |
| POST | `/api/v1/Bonus` | 201 `BonusListItemDto` |
| PUT | `/api/v1/Bonus/{bonusId}/cancel` | 204 |
| DELETE | `/api/v1/Bonus/{bonusId}` | 204 |
| POST | `/api/v1/Bonus/{bonusId}/pay` | 204 |

`UserCode` se lee con `[FromHeader(Name = "UserCode")]` y se pasa al servicio. Cada acción es una línea.

## 8. Frontend

- Dependencias: `@angular/material` y `@angular/cdk` 22.2.7, tema azul en `styles.css`, Roboto + Material Symbols.
- `core/models/bonus.ts`, `core/services/bonus-api.ts`, `core/utils/bonus-calculator.ts` (solo vista previa).
- `shared/confirm-dialog/` — diálogo de confirmación reutilizable.
- `features/bonuses/`:
  - Filtros: área, número de empleado, periodo (`input type="month"`), estado; Buscar (vuelve a página 1) y Limpiar.
  - `mat-table` con: acciones, estado (chip), periodo, área-número, nombre, tipo, monto/porcentaje (con `%`), motivo, bruto, retención, neto.
  - `mat-paginator` de 10 filas, llamada a la API por página, total de registros.
  - Acciones Anular / Eliminar / Pagar habilitadas solo en `0201`, con confirmación.
  - Estados: cargando (barra), vacío, errores vía interceptor.
- `features/bonuses/register-bonus-dialog/`:
  - Búsqueda por área + número (obligatorios, `mat-error`), tarjeta con nombre, ingreso, sueldo, % retención.
  - Formulario reactivo: periodo (≤ mes actual), tipo (radio), valor (etiqueta según tipo, > 0 y ≤ sueldo o ≤ 100), motivo activo.
  - Vista previa con `computed` de bruto, retención, neto.
  - Guardar deshabilitado si inválido o guardando; al éxito cierra con `true` y la pantalla refresca.

## 9. Pruebas y verificación

- `backend/tests/BonusTest.Domain.Tests` (xUnit): ejemplo del enunciado (500/40/460), monto fijo, redondeo medio hacia arriba, retención 0 %, transiciones válidas e inválidas, reparto con 1, 2 y 3 proyectos (suma exacta; TECH1-102 bruto 300 → 135/105/60).
- `dotnet build`, `dotnet test`, `ng build` sin errores.
- Humo con `curl` de los 6 endpoints y los 409 principales.
- Navegador (Playwright): registrar, paginar (> 10 bonos), filtrar, anular, eliminar, pagar; capturas escritorio y móvil.

## 10. Entrega

- Commits convencionales por etapa.
- README: requisitos, levantar (SQL local o Docker con `ConnectionStrings__BonusTest`), orden de scripts, decisiones, endpoints, pendientes.
- `appsettings.Development.json` sin cambios.

## Fuera de alcance

Autenticación real, edición de bonos, revertir pagos, exportar listados, i18n.
