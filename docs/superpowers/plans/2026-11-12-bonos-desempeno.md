# Módulo de Bonos de desempeño — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Completar el módulo de bonos (BD, API .NET 10 por capas y pantalla Angular 22 con Material) con registro, listado paginado en servidor, anular, eliminar lógico y pagar con reparto por proyecto.

**Architecture:** Reglas puras en Domain (`BonusCalculator`, `BonusDistributor`, entidad `Bonus` con transiciones), casos de uso en Application (`BonusService` + `IBonusRepository`), EF Core en Infrastructure (paginación `Skip/Take` → `OFFSET/FETCH`), controlador delgado en Api. El front consume la API con `BonusApi`, muestra `mat-table` + `mat-paginator` y un `MatDialog` de registro.

**Tech Stack:** SQL Server 2022 (Docker), .NET 10 / ASP.NET Core / EF Core 10, xUnit, Angular 22.2 standalone + Angular Material 22.2.7, RxJS 7.8.

**Spec:** `docs/superpowers/specs/2026-11-12-bonos-desempeno-design.md`

## Global Constraints

- No modificar `database/01_schema_seed.sql` ni `backend/src/BonusTest.Api/appsettings.Development.json`.
- Respetar las capas Domain → Application → Infrastructure → Api; **ninguna regla de negocio en el controlador**.
- Dinero siempre en `decimal`; redondeo `Math.Round(x, 2, MidpointRounding.AwayFromZero)`.
- Errores de negocio con `BusinessException(code, description)` (el middleware responde 409). Mensajes en español.
- El front no maneja errores HTTP en componentes: el interceptor ya los muestra. En `subscribe` usar `error: noop` solo para evitar errores no controlados en consola.
- Usuario operador: header `UserCode` (el interceptor del front ya lo envía como `candidato`).
- Commits convencionales en español, terminando con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Todas las rutas son relativas a la raíz del repositorio.

## Entorno local (leer antes de empezar)

- `dotnet` está en `C:\Program Files\dotnet`. En Git Bash: `export PATH="/c/Program Files/dotnet:$PATH"`.
- SQL Server corre en Docker, contenedor `bonustest-sql`, usuario `sa`, clave `BonusTest#2026`. Ejecutar scripts:
  ```bash
  export MSYS_NO_PATHCONV=1
  docker cp database/02_bonus.sql bonustest-sql:/tmp/02.sql
  docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -I -b -i /tmp/02.sql
  ```
- La API se levanta con la cadena de conexión por variable de entorno (no se toca `appsettings`):
  ```bash
  export ConnectionStrings__BonusTest='Server=localhost,1433;Database=BonusTest;User Id=sa;Password=BonusTest#2026;TrustServerCertificate=True;'
  cd backend/src/BonusTest.Api && dotnet run --launch-profile http   # http://localhost:5106
  ```
  **Detener la API antes de `dotnet build`/`dotnet test`** (bloquea los DLL en Windows).
- Front: `cd frontend && npx ng serve` → http://localhost:4200/bonuses.

## Mapa de archivos

```
database/
  02_bonus.sql                                   (T1) tabla Bonus + índices
  03_bonus_payment_detail.sql                    (T10) detalle de pago
backend/src/BonusTest.Domain/
  Exceptions/BonusErrors.cs                      (T2) catálogo de BusinessException del módulo
  Services/Money.cs                              (T2) redondeo monetario
  Services/BonusCalculator.cs                    (T2) bruto/retención/neto
  Services/BonusDistributor.cs                   (T10) reparto por horas
  Entities/Bonus.cs                              (T3, T10) entidad + transiciones
  Entities/BonusPaymentDetail.cs                 (T10)
backend/src/BonusTest.Application/
  Bonuses/BonusDtos.cs                           (T4)
  Bonuses/IBonusRepository.cs                    (T4, T10)
  Bonuses/BonusService.cs                        (T4, T10)
  DependencyInjection.cs                         (T4) modificar
backend/src/BonusTest.Infrastructure/
  Persistence/AppDbContext.cs                    (T5, T10) modificar
  Persistence/Configurations/BonusConfigurations.cs (T5, T10)
  Repositories/BonusRepository.cs                (T5, T10)
  DependencyInjection.cs                         (T5) modificar
backend/src/BonusTest.Api/Controllers/BonusController.cs (T6, T10)
backend/tests/BonusTest.UnitTests/
  Domain/BonusCalculatorTests.cs                 (T2)
  Domain/BonusTests.cs                           (T3, T10)
  Domain/BonusDistributorTests.cs                (T10)
  Application/BonusServiceTests.cs               (T4, T10)
  Fakes/FakeBonusRepository.cs                   (T4, T10)
  Fakes/FixedTimeProvider.cs                     (T4)
frontend/src/
  index.html, styles.css, app/app.css, app/app.config.ts, angular.json  (T7) modificar
  app/core/models/bonus.ts                       (T7)
  app/core/services/bonus-api.ts                 (T7, T11)
  app/core/utils/bonus-calculator.ts             (T7)
  app/core/utils/period.ts                       (T7)
  app/core/i18n/spanish-paginator-intl.ts        (T7)
  app/shared/confirm-dialog/confirm-dialog.ts    (T7)
  app/features/bonuses/bonuses.{ts,html,css}     (T8, T9, T11) modificar
  app/features/bonuses/register-bonus-dialog/register-bonus-dialog.{ts,html,css} (T9)
README.md                                        (T12)
```

---

### Task 1: Tabla `Bonus` en la base de datos

**Files:**
- Create: `database/02_bonus.sql`

**Interfaces:**
- Produces: tabla `dbo.Bonus` con columnas `BonusId, DepartmentCode, EmployeeNumber, Period, BonusTypeId, BonusValue, ReasonCode, TaxRateApplied, GrossAmount, TaxAmount, NetAmount, BonusStateCode, IsDeleted, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt`; índices `UX_Bonus_ActivePerPeriod`, `IX_Bonus_Period_State`.

- [ ] **Step 1: Escribir el script**

```sql
/* =====================================================================
   Prueba técnica - Bonos de desempeño
   Script 2: tabla Bonus. Idempotente (se puede ejecutar más de una vez).
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- requerido por los índices filtrados
GO
USE BonusTest;
GO

IF OBJECT_ID('dbo.Bonus', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bonus (
        BonusId         INT IDENTITY(1,1) CONSTRAINT PK_Bonus PRIMARY KEY,
        DepartmentCode  CHAR(5)        NOT NULL,
        EmployeeNumber  INT            NOT NULL,
        Period          CHAR(7)        NOT NULL,   -- YYYY-MM
        BonusTypeId     INT            NOT NULL,
        BonusValue      DECIMAL(16,2)  NOT NULL,   -- monto fijo o porcentaje ingresado
        ReasonCode      CHAR(6)        NOT NULL,
        TaxRateApplied  DECIMAL(5,2)   NOT NULL,   -- % de retención del empleado al registrar
        GrossAmount     DECIMAL(16,2)  NOT NULL,
        TaxAmount       DECIMAL(16,2)  NOT NULL,
        NetAmount       DECIMAL(16,2)  NOT NULL,
        BonusStateCode  CHAR(4)        NOT NULL CONSTRAINT DF_Bonus_State DEFAULT ('0201'),
        IsDeleted       BIT            NOT NULL CONSTRAINT DF_Bonus_IsDeleted DEFAULT (0),
        CreatedBy       VARCHAR(50)    NOT NULL,
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Bonus_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       VARCHAR(50)    NULL,
        UpdatedAt       DATETIME2(0)   NULL,
        CONSTRAINT FK_Bonus_Employee    FOREIGN KEY (DepartmentCode, EmployeeNumber)
            REFERENCES dbo.Employee (DepartmentCode, EmployeeNumber),
        CONSTRAINT FK_Bonus_BonusType   FOREIGN KEY (BonusTypeId)    REFERENCES dbo.BonusType (BonusTypeId),
        CONSTRAINT FK_Bonus_BonusReason FOREIGN KEY (ReasonCode)     REFERENCES dbo.BonusReason (ReasonCode),
        CONSTRAINT FK_Bonus_State       FOREIGN KEY (BonusStateCode) REFERENCES dbo.State (StateCode),
        CONSTRAINT CK_Bonus_Period      CHECK (Period LIKE '[12][0-9][0-9][0-9]-[01][0-9]'
                                               AND SUBSTRING(Period, 6, 2) BETWEEN '01' AND '12'),
        CONSTRAINT CK_Bonus_BonusValue  CHECK (BonusValue > 0),
        CONSTRAINT CK_Bonus_Amounts     CHECK (GrossAmount > 0 AND TaxAmount >= 0
                                               AND NetAmount = GrossAmount - TaxAmount)
    );
END
GO

-- Regla: un solo bono activo (no anulado y no eliminado) por empleado y periodo.
-- Respalda la validación del servicio ante registros concurrentes.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Bonus_ActivePerPeriod' AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE UNIQUE INDEX UX_Bonus_ActivePerPeriod
        ON dbo.Bonus (DepartmentCode, EmployeeNumber, Period)
        WHERE IsDeleted = 0 AND BonusStateCode <> '0203';
GO

-- Filtros del listado por periodo y estado sobre los registros visibles.
-- El filtro por área + número usa el prefijo del índice anterior.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Bonus_Period_State' AND object_id = OBJECT_ID('dbo.Bonus'))
    CREATE INDEX IX_Bonus_Period_State
        ON dbo.Bonus (Period, BonusStateCode)
        WHERE IsDeleted = 0;
GO
```

- [ ] **Step 2: Ejecutar el script dos veces (verifica idempotencia)**

```bash
export MSYS_NO_PATHCONV=1
docker cp database/02_bonus.sql bonustest-sql:/tmp/02.sql
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -I -b -i /tmp/02.sql
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -I -b -i /tmp/02.sql
```
Expected: ambas ejecuciones terminan sin errores (salida `Changed database context to 'BonusTest'.`).

- [ ] **Step 3: Verificar el índice único filtrado (y revertir)**

```bash
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -I -d BonusTest -Q "
BEGIN TRAN;
INSERT INTO dbo.Bonus (DepartmentCode,EmployeeNumber,Period,BonusTypeId,BonusValue,ReasonCode,TaxRateApplied,GrossAmount,TaxAmount,NetAmount,CreatedBy)
VALUES ('TECH1',101,'2026-10',2,10,'000001',8,500,40,460,'test');
UPDATE dbo.Bonus SET BonusStateCode='0203' WHERE CreatedBy='test';
INSERT INTO dbo.Bonus (DepartmentCode,EmployeeNumber,Period,BonusTypeId,BonusValue,ReasonCode,TaxRateApplied,GrossAmount,TaxAmount,NetAmount,CreatedBy)
VALUES ('TECH1',101,'2026-10',2,10,'000001',8,500,40,460,'test');
PRINT 'anulado + nuevo: OK';
INSERT INTO dbo.Bonus (DepartmentCode,EmployeeNumber,Period,BonusTypeId,BonusValue,ReasonCode,TaxRateApplied,GrossAmount,TaxAmount,NetAmount,CreatedBy)
VALUES ('TECH1',101,'2026-10',2,10,'000001',8,500,40,460,'test');
ROLLBACK;"
```
Expected: imprime `anulado + nuevo: OK` y luego `Msg 2601 ... Cannot insert duplicate key row ... UX_Bonus_ActivePerPeriod`.

- [ ] **Step 4: Commit**

```bash
git add database/02_bonus.sql
git commit -m "feat(db): tabla Bonus con claves foráneas, checks e índices

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Proyecto de pruebas + cálculo del bono (Domain)

**Files:**
- Create: `backend/tests/BonusTest.UnitTests/` (plantilla xUnit)
- Create: `backend/src/BonusTest.Domain/Exceptions/BonusErrors.cs`
- Create: `backend/src/BonusTest.Domain/Services/Money.cs`
- Create: `backend/src/BonusTest.Domain/Services/BonusCalculator.cs`
- Test: `backend/tests/BonusTest.UnitTests/Domain/BonusCalculatorTests.cs`

**Interfaces:**
- Produces:
  - `BonusErrors` (static, namespace `BonusTest.Domain.Exceptions`) con métodos `BusinessException UserRequired()`, `EmployeeNotFound()`, `EmployeeInactive()`, `InvalidPeriod()`, `FuturePeriod()`, `InvalidBonusType()`, `InactiveReason()`, `InvalidAmount()`, `InvalidPercentage()`, `DuplicateBonus()`, `BonusNotFound()`, `BonusNotRegistered()`, `EmployeeWithoutProjects()`.
  - `Money.Round(decimal) : decimal` (namespace `BonusTest.Domain.Services`).
  - `record BonusAmounts(decimal Gross, decimal Tax, decimal Net)`; `BonusCalculator.Calculate(int bonusTypeId, decimal bonusValue, decimal monthlySalary, decimal taxRate) : BonusAmounts`.

- [ ] **Step 1: Crear el proyecto de pruebas y agregarlo a la solución**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd backend
dotnet new xunit -n BonusTest.UnitTests -o tests/BonusTest.UnitTests -f net10.0
rm tests/BonusTest.UnitTests/UnitTest1.cs
dotnet add tests/BonusTest.UnitTests/BonusTest.UnitTests.csproj reference src/BonusTest.Application/BonusTest.Application.csproj
dotnet sln BonusTest.slnx add tests/BonusTest.UnitTests/BonusTest.UnitTests.csproj
```
Expected: `BonusTest.slnx` ahora lista `tests/BonusTest.UnitTests/BonusTest.UnitTests.csproj`.

- [ ] **Step 2: Escribir las pruebas que fallan**

`backend/tests/BonusTest.UnitTests/Domain/BonusCalculatorTests.cs`
```csharp
using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.UnitTests.Domain;

public class BonusCalculatorTests
{
    [Fact]
    public void Percentage_bonus_matches_statement_example()
    {
        // TECH1-101: sueldo 5,000.00, retención 8 %, bono 10 % → 500.00 / 40.00 / 460.00
        var amounts = BonusCalculator.Calculate(BonusTypes.SalaryPercentage, 10m, 5000m, 8m);

        Assert.Equal(new BonusAmounts(500.00m, 40.00m, 460.00m), amounts);
    }

    [Fact]
    public void Fixed_amount_bonus_uses_the_amount_as_gross()
    {
        var amounts = BonusCalculator.Calculate(BonusTypes.FixedAmount, 1200m, 3000m, 8m);

        Assert.Equal(new BonusAmounts(1200m, 96m, 1104m), amounts);
    }

    [Fact]
    public void Zero_tax_rate_keeps_net_equal_to_gross()
    {
        var amounts = BonusCalculator.Calculate(BonusTypes.FixedAmount, 800m, 2500m, 0m);

        Assert.Equal(new BonusAmounts(800m, 0m, 800m), amounts);
    }

    [Fact]
    public void Gross_rounds_half_up()
    {
        // 1,000.25 × 50 % = 500.125 → 500.13
        var amounts = BonusCalculator.Calculate(BonusTypes.SalaryPercentage, 50m, 1000.25m, 0m);

        Assert.Equal(500.13m, amounts.Gross);
    }

    [Fact]
    public void Tax_rounds_half_up()
    {
        // 100.25 × 10 % = 10.025 → 10.03 (el redondeo bancario daría 10.02)
        var amounts = BonusCalculator.Calculate(BonusTypes.FixedAmount, 100.25m, 3000m, 10m);

        Assert.Equal(new BonusAmounts(100.25m, 10.03m, 90.22m), amounts);
    }

    [Fact]
    public void Unknown_bonus_type_is_rejected()
    {
        var error = Assert.Throws<BusinessException>(() => BonusCalculator.Calculate(3, 10m, 5000m, 8m));

        Assert.Equal("INVALID_BONUS_TYPE", error.Code);
    }

    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(0.124, 0.12)]
    [InlineData(2.675, 2.68)]
    [InlineData(-0.125, -0.13)]
    public void Money_round_uses_half_away_from_zero(double value, double expected)
    {
        Assert.Equal((decimal)expected, Money.Round((decimal)value));
    }
}
```

- [ ] **Step 3: Ejecutar y confirmar que falla**

Run: `cd backend && dotnet test`
Expected: FAIL de compilación — `The name 'BonusCalculator' does not exist in the current context`.

- [ ] **Step 4: Implementar**

`backend/src/BonusTest.Domain/Exceptions/BonusErrors.cs`
```csharp
namespace BonusTest.Domain.Exceptions;

/// <summary>
/// Errores de negocio del módulo de bonos. Centraliza códigos y mensajes para no repetirlos entre capas.
/// </summary>
public static class BonusErrors
{
    public static BusinessException UserRequired() =>
        new("USER_REQUIRED", "No se recibió el usuario que realiza la operación (header UserCode).");

    public static BusinessException EmployeeNotFound() =>
        new("EMPLOYEE_NOT_FOUND", "No existe un empleado con el área y número indicados.");

    public static BusinessException EmployeeInactive() =>
        new("EMPLOYEE_INACTIVE", "El empleado está cesado; solo se registran bonos a empleados activos.");

    public static BusinessException InvalidPeriod() =>
        new("INVALID_PERIOD", "El periodo debe tener el formato YYYY-MM.");

    public static BusinessException FuturePeriod() =>
        new("FUTURE_PERIOD", "El periodo no puede ser posterior al mes actual.");

    public static BusinessException InvalidBonusType() =>
        new("INVALID_BONUS_TYPE", "El tipo de bono no es válido.");

    public static BusinessException InactiveReason() =>
        new("INACTIVE_REASON", "El motivo no existe o no está activo.");

    public static BusinessException InvalidAmount() =>
        new("INVALID_AMOUNT", "El monto debe ser mayor a 0, con hasta 2 decimales, y no superar el sueldo mensual.");

    public static BusinessException InvalidPercentage() =>
        new("INVALID_PERCENTAGE", "El porcentaje debe ser mayor a 0, con hasta 2 decimales, y no superar 100.");

    public static BusinessException DuplicateBonus() =>
        new("DUPLICATE_BONUS", "El empleado ya tiene un bono activo en ese periodo.");

    public static BusinessException BonusNotFound() =>
        new("BONUS_NOT_FOUND", "El bono no existe o fue eliminado.");

    public static BusinessException BonusNotRegistered() =>
        new("BONUS_NOT_REGISTERED", "Solo se puede anular, eliminar o pagar un bono en estado Registrado.");

    public static BusinessException EmployeeWithoutProjects() =>
        new("EMPLOYEE_WITHOUT_PROJECTS", "El empleado no tiene horas por proyecto para distribuir el bono.");
}
```

`backend/src/BonusTest.Domain/Services/Money.cs`
```csharp
namespace BonusTest.Domain.Services;

public static class Money
{
    /// <summary>Redondea a 2 decimales con el medio hacia arriba (0.125 → 0.13).</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
```

`backend/src/BonusTest.Domain/Services/BonusCalculator.cs`
```csharp
using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;

namespace BonusTest.Domain.Services;

public record BonusAmounts(decimal Gross, decimal Tax, decimal Net);

/// <summary>Calcula bruto, retención y neto de un bono. Cada monto se redondea a 2 decimales.</summary>
public static class BonusCalculator
{
    public static BonusAmounts Calculate(int bonusTypeId, decimal bonusValue, decimal monthlySalary, decimal taxRate)
    {
        var gross = bonusTypeId switch
        {
            BonusTypes.FixedAmount => Money.Round(bonusValue),
            BonusTypes.SalaryPercentage => Money.Round(monthlySalary * bonusValue / 100m),
            _ => throw BonusErrors.InvalidBonusType()
        };
        var tax = Money.Round(gross * taxRate / 100m);

        return new BonusAmounts(gross, tax, gross - tax);
    }
}
```

- [ ] **Step 5: Ejecutar y confirmar que pasa**

Run: `cd backend && dotnet test`
Expected: `Correctas! - Con error: 0, Superado: 10` (6 Facts + 4 casos del Theory).

- [ ] **Step 6: Commit**

```bash
git add backend/BonusTest.slnx backend/tests backend/src/BonusTest.Domain
git commit -m "feat(domain): cálculo del bono con redondeo medio hacia arriba

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Entidad `Bonus` con transiciones de estado

**Files:**
- Create: `backend/src/BonusTest.Domain/Entities/Bonus.cs`
- Test: `backend/tests/BonusTest.UnitTests/Domain/BonusTests.cs`

**Interfaces:**
- Consumes: `BonusCalculator.Calculate`, `BonusErrors.BonusNotRegistered()` (Task 2); `Employee`, `BonusStates`, `BonusTypes` (proyecto base).
- Produces: clase `Bonus` (namespace `BonusTest.Domain.Entities`) con propiedades de solo lectura pública `BonusId, DepartmentCode, EmployeeNumber, Period, BonusTypeId, BonusValue, ReasonCode, TaxRateApplied, GrossAmount, TaxAmount, NetAmount, BonusStateCode, IsDeleted, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt`; `static Bonus Create(Employee employee, string period, int bonusTypeId, decimal bonusValue, string reasonCode, string userCode, DateTime now)`; `void Cancel(string userCode, DateTime now)`; `void Delete(string userCode, DateTime now)`.

- [ ] **Step 1: Escribir las pruebas que fallan**

`backend/tests/BonusTest.UnitTests/Domain/BonusTests.cs`
```csharp
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;

namespace BonusTest.UnitTests.Domain;

public class BonusTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0);
    private static readonly DateTime Later = Now.AddHours(2);

    private static Employee Andrea() => new()
    {
        DepartmentCode = "TECH1",
        EmployeeNumber = 101,
        FullName = "Andrea Torres Vega",
        HireDate = new DateTime(2022, 3, 1),
        MonthlySalary = 5000m,
        TaxRate = 8m,
        EmployeeStateCode = EmployeeStates.Active
    };

    private static Bonus NewBonus() =>
        Bonus.Create(Andrea(), "2026-10", BonusTypes.SalaryPercentage, 10m, "000001", "candidato", Now);

    [Fact]
    public void Create_sets_amounts_registered_state_and_creation_audit()
    {
        var bonus = NewBonus();

        Assert.Equal("TECH1", bonus.DepartmentCode);
        Assert.Equal(101, bonus.EmployeeNumber);
        Assert.Equal("2026-10", bonus.Period);
        Assert.Equal(BonusTypes.SalaryPercentage, bonus.BonusTypeId);
        Assert.Equal(10m, bonus.BonusValue);
        Assert.Equal("000001", bonus.ReasonCode);
        Assert.Equal(8m, bonus.TaxRateApplied);
        Assert.Equal(500m, bonus.GrossAmount);
        Assert.Equal(40m, bonus.TaxAmount);
        Assert.Equal(460m, bonus.NetAmount);
        Assert.Equal(BonusStates.Registered, bonus.BonusStateCode);
        Assert.False(bonus.IsDeleted);
        Assert.Equal("candidato", bonus.CreatedBy);
        Assert.Equal(Now, bonus.CreatedAt);
        Assert.Null(bonus.UpdatedBy);
        Assert.Null(bonus.UpdatedAt);
    }

    [Fact]
    public void Cancel_moves_registered_bonus_to_cancelled_and_audits()
    {
        var bonus = NewBonus();

        bonus.Cancel("rrhh", Later);

        Assert.Equal(BonusStates.Cancelled, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(Later, bonus.UpdatedAt);
    }

    [Fact]
    public void Delete_marks_registered_bonus_as_deleted_and_audits()
    {
        var bonus = NewBonus();

        bonus.Delete("rrhh", Later);

        Assert.True(bonus.IsDeleted);
        Assert.Equal(BonusStates.Registered, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(Later, bonus.UpdatedAt);
    }

    [Fact]
    public void Cancel_fails_when_bonus_is_already_cancelled()
    {
        var bonus = NewBonus();
        bonus.Cancel("rrhh", Later);

        var error = Assert.Throws<BusinessException>(() => bonus.Cancel("rrhh", Later));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
    }

    [Fact]
    public void Delete_fails_when_bonus_is_cancelled()
    {
        var bonus = NewBonus();
        bonus.Cancel("rrhh", Later);

        var error = Assert.Throws<BusinessException>(() => bonus.Delete("rrhh", Later));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
        Assert.False(bonus.IsDeleted);
    }
}
```

- [ ] **Step 2: Ejecutar y confirmar que falla**

Run: `cd backend && dotnet test`
Expected: FAIL de compilación — `The type or namespace name 'Bonus' could not be found`.

- [ ] **Step 3: Implementar**

`backend/src/BonusTest.Domain/Entities/Bonus.cs`
```csharp
using BonusTest.Domain.Constants;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Domain.Entities;

/// <summary>
/// Bono de desempeño. Los montos se calculan al crearlo y solo cambia de estado
/// mediante sus métodos, que exigen que el bono esté en Registrado.
/// </summary>
public class Bonus
{
    public int BonusId { get; private set; }
    public string DepartmentCode { get; private set; } = null!;
    public int EmployeeNumber { get; private set; }
    public string Period { get; private set; } = null!;
    public int BonusTypeId { get; private set; }
    public decimal BonusValue { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public decimal TaxRateApplied { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public string BonusStateCode { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Bonus() { }

    public static Bonus Create(Employee employee, string period, int bonusTypeId, decimal bonusValue,
        string reasonCode, string userCode, DateTime now)
    {
        var amounts = BonusCalculator.Calculate(bonusTypeId, bonusValue, employee.MonthlySalary, employee.TaxRate);

        return new Bonus
        {
            DepartmentCode = employee.DepartmentCode,
            EmployeeNumber = employee.EmployeeNumber,
            Period = period,
            BonusTypeId = bonusTypeId,
            BonusValue = bonusValue,
            ReasonCode = reasonCode,
            TaxRateApplied = employee.TaxRate,
            GrossAmount = amounts.Gross,
            TaxAmount = amounts.Tax,
            NetAmount = amounts.Net,
            BonusStateCode = BonusStates.Registered,
            CreatedBy = userCode,
            CreatedAt = now
        };
    }

    public void Cancel(string userCode, DateTime now)
    {
        EnsureRegistered();
        BonusStateCode = BonusStates.Cancelled;
        RegisterUpdate(userCode, now);
    }

    public void Delete(string userCode, DateTime now)
    {
        EnsureRegistered();
        IsDeleted = true;
        RegisterUpdate(userCode, now);
    }

    private void EnsureRegistered()
    {
        if (BonusStateCode != BonusStates.Registered)
            throw BonusErrors.BonusNotRegistered();
    }

    private void RegisterUpdate(string userCode, DateTime now)
    {
        UpdatedBy = userCode;
        UpdatedAt = now;
    }
}
```

- [ ] **Step 4: Ejecutar y confirmar que pasa**

Run: `cd backend && dotnet test`
Expected: `Superado: 15`, `Con error: 0`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/BonusTest.Domain/Entities/Bonus.cs backend/tests/BonusTest.UnitTests/Domain/BonusTests.cs
git commit -m "feat(domain): entidad Bonus con anulación y eliminación lógica

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Casos de uso en Application (`BonusService`)

**Files:**
- Create: `backend/src/BonusTest.Application/Bonuses/BonusDtos.cs`
- Create: `backend/src/BonusTest.Application/Bonuses/IBonusRepository.cs`
- Create: `backend/src/BonusTest.Application/Bonuses/BonusService.cs`
- Modify: `backend/src/BonusTest.Application/DependencyInjection.cs`
- Create: `backend/tests/BonusTest.UnitTests/Fakes/FixedTimeProvider.cs`
- Create: `backend/tests/BonusTest.UnitTests/Fakes/FakeBonusRepository.cs`
- Test: `backend/tests/BonusTest.UnitTests/Application/BonusServiceTests.cs`

**Interfaces:**
- Consumes: `Bonus`, `BonusErrors`, `Money` (Tasks 2-3); `PagedResponse<T>`, `PaginationRequest` (base).
- Produces (namespace `BonusTest.Application.Bonuses`):
  - `class BonusFilter : PaginationRequest { string? DepartmentCode; int? EmployeeNumber; string? Period; string? BonusStateCode; }`
  - `record BonusListItemDto(int BonusId, string BonusStateCode, string StateDescription, string Period, string DepartmentCode, string DepartmentName, int EmployeeNumber, string FullName, int BonusTypeId, string BonusTypeDescription, decimal BonusValue, string ReasonDescription, decimal GrossAmount, decimal TaxAmount, decimal NetAmount)`
  - `record EmployeeLookupDto(string DepartmentCode, int EmployeeNumber, string FullName, DateTime HireDate, decimal MonthlySalary, decimal TaxRate)`
  - `record RegisterBonusRequest(string DepartmentCode, int EmployeeNumber, string Period, int BonusTypeId, decimal BonusValue, string ReasonCode)`
  - `record RegisteredBonusDto(int BonusId, decimal GrossAmount, decimal TaxAmount, decimal NetAmount)`
  - `interface IBonusRepository` (métodos abajo).
  - `BonusService.SearchAsync(BonusFilter, CancellationToken) : Task<PagedResponse<BonusListItemDto>>`, `GetActiveEmployeeAsync(string, int, CancellationToken) : Task<EmployeeLookupDto>`, `RegisterAsync(RegisterBonusRequest, string? userCode, CancellationToken) : Task<RegisteredBonusDto>`, `CancelAsync(int bonusId, string? userCode, CancellationToken) : Task`, `DeleteAsync(int bonusId, string? userCode, CancellationToken) : Task`.

- [ ] **Step 1: Crear DTOs e interfaz del repositorio (contratos que usan las pruebas)**

`backend/src/BonusTest.Application/Bonuses/BonusDtos.cs`
```csharp
using BonusTest.Application.Common;

namespace BonusTest.Application.Bonuses;

/// <summary>Filtros opcionales del listado de bonos.</summary>
public class BonusFilter : PaginationRequest
{
    public string? DepartmentCode { get; set; }
    public int? EmployeeNumber { get; set; }
    public string? Period { get; set; }
    public string? BonusStateCode { get; set; }
}

public record BonusListItemDto(
    int BonusId,
    string BonusStateCode,
    string StateDescription,
    string Period,
    string DepartmentCode,
    string DepartmentName,
    int EmployeeNumber,
    string FullName,
    int BonusTypeId,
    string BonusTypeDescription,
    decimal BonusValue,
    string ReasonDescription,
    decimal GrossAmount,
    decimal TaxAmount,
    decimal NetAmount);

public record EmployeeLookupDto(
    string DepartmentCode,
    int EmployeeNumber,
    string FullName,
    DateTime HireDate,
    decimal MonthlySalary,
    decimal TaxRate);

public record RegisterBonusRequest(
    string DepartmentCode,
    int EmployeeNumber,
    string Period,
    int BonusTypeId,
    decimal BonusValue,
    string ReasonCode);

public record RegisteredBonusDto(int BonusId, decimal GrossAmount, decimal TaxAmount, decimal NetAmount);
```

`backend/src/BonusTest.Application/Bonuses/IBonusRepository.cs`
```csharp
using BonusTest.Application.Common;
using BonusTest.Domain.Entities;

namespace BonusTest.Application.Bonuses;

public interface IBonusRepository
{
    Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken);
    Task<Employee?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken);
    Task<bool> BonusTypeExistsAsync(int bonusTypeId, CancellationToken cancellationToken);
    Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken cancellationToken);
    /// <summary>Activo = no eliminado y en un estado distinto de Anulado.</summary>
    Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken);
    /// <summary>Devuelve el bono no eliminado, con seguimiento de cambios.</summary>
    Task<Bonus?> GetByIdAsync(int bonusId, CancellationToken cancellationToken);
    void Add(Bonus bonus);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Crear los dobles de prueba**

`backend/tests/BonusTest.UnitTests/Fakes/FixedTimeProvider.cs`
```csharp
namespace BonusTest.UnitTests.Fakes;

/// <summary>Reloj fijo en UTC para probar reglas que dependen de la fecha actual.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
```

`backend/tests/BonusTest.UnitTests/Fakes/FakeBonusRepository.cs`
```csharp
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;

namespace BonusTest.UnitTests.Fakes;

/// <summary>Repositorio en memoria. Sin BD, todos los bonos tienen BonusId = 0.</summary>
internal sealed class FakeBonusRepository : IBonusRepository
{
    public List<Employee> Employees { get; } = [];
    public List<Bonus> Bonuses { get; } = [];
    public HashSet<int> BonusTypeIds { get; } = [BonusTypes.FixedAmount, BonusTypes.SalaryPercentage];
    public HashSet<string> ActiveReasonCodes { get; } = ["000001", "000002", "000003"];
    public int SaveChangesCalls { get; private set; }

    public Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        throw new NotSupportedException("El listado se prueba contra la base de datos.");

    public Task<Employee?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber));

    public Task<bool> BonusTypeExistsAsync(int bonusTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(BonusTypeIds.Contains(bonusTypeId));

    public Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken cancellationToken) =>
        Task.FromResult(ActiveReasonCodes.Contains(reasonCode));

    public Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken) =>
        Task.FromResult(Bonuses.Any(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber
            && x.Period == period && !x.IsDeleted && x.BonusStateCode != BonusStates.Cancelled));

    public Task<Bonus?> GetByIdAsync(int bonusId, CancellationToken cancellationToken) =>
        Task.FromResult(Bonuses.LastOrDefault(x => x.BonusId == bonusId && !x.IsDeleted));

    public void Add(Bonus bonus) => Bonuses.Add(bonus);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Escribir las pruebas que fallan**

`backend/tests/BonusTest.UnitTests/Application/BonusServiceTests.cs`
```csharp
using BonusTest.Application.Bonuses;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.UnitTests.Fakes;

namespace BonusTest.UnitTests.Application;

public class BonusServiceTests
{
    private static readonly DateTimeOffset Today = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);
    private const string User = "candidato";

    private readonly FakeBonusRepository _repository = new();
    private readonly BonusService _service;

    public BonusServiceTests()
    {
        _repository.Employees.Add(new Employee
        {
            DepartmentCode = "TECH1", EmployeeNumber = 101, FullName = "Andrea Torres Vega",
            HireDate = new DateTime(2022, 3, 1), MonthlySalary = 5000m, TaxRate = 8m,
            EmployeeStateCode = EmployeeStates.Active
        });
        _repository.Employees.Add(new Employee
        {
            DepartmentCode = "VENTA", EmployeeNumber = 202, FullName = "Rosa Medina Quispe",
            HireDate = new DateTime(2020, 5, 20), MonthlySalary = 2000m, TaxRate = 8m,
            EmployeeStateCode = EmployeeStates.Terminated
        });
        _service = new BonusService(_repository, new FixedTimeProvider(Today));
    }

    private static RegisterBonusRequest Request(string period = "2026-11", int bonusTypeId = BonusTypes.SalaryPercentage,
        decimal bonusValue = 10m, string reasonCode = "000001", string departmentCode = "TECH1", int employeeNumber = 101) =>
        new(departmentCode, employeeNumber, period, bonusTypeId, bonusValue, reasonCode);

    private async Task<string> RegisterErrorCode(RegisterBonusRequest request, string? user = User)
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.RegisterAsync(request, user, CancellationToken.None));
        return error.Code;
    }

    [Fact]
    public async Task Register_saves_registered_bonus_with_calculated_amounts()
    {
        var result = await _service.RegisterAsync(Request(), User, CancellationToken.None);

        var bonus = Assert.Single(_repository.Bonuses);
        Assert.Equal(BonusStates.Registered, bonus.BonusStateCode);
        Assert.Equal(User, bonus.CreatedBy);
        Assert.Equal(Today.DateTime, bonus.CreatedAt);
        Assert.Equal(new RegisteredBonusDto(0, 500m, 40m, 460m), result);
        Assert.Equal(1, _repository.SaveChangesCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_requires_user(string? user) =>
        Assert.Equal("USER_REQUIRED", await RegisterErrorCode(Request(), user));

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026/10")]
    [InlineData("26-10")]
    [InlineData("2026-1")]
    [InlineData("")]
    public async Task Register_rejects_malformed_period(string period) =>
        Assert.Equal("INVALID_PERIOD", await RegisterErrorCode(Request(period: period)));

    [Fact]
    public async Task Register_rejects_period_after_current_month() =>
        Assert.Equal("FUTURE_PERIOD", await RegisterErrorCode(Request(period: "2026-12")));

    [Fact]
    public async Task Register_accepts_past_period()
    {
        await _service.RegisterAsync(Request(period: "2025-01"), User, CancellationToken.None);

        Assert.Single(_repository.Bonuses);
    }

    [Fact]
    public async Task Register_rejects_unknown_employee() =>
        Assert.Equal("EMPLOYEE_NOT_FOUND", await RegisterErrorCode(Request(employeeNumber: 999)));

    [Fact]
    public async Task Register_rejects_terminated_employee() =>
        Assert.Equal("EMPLOYEE_INACTIVE", await RegisterErrorCode(Request(departmentCode: "VENTA", employeeNumber: 202)));

    [Fact]
    public async Task Register_rejects_unknown_bonus_type() =>
        Assert.Equal("INVALID_BONUS_TYPE", await RegisterErrorCode(Request(bonusTypeId: 3)));

    [Fact]
    public async Task Register_rejects_inactive_reason() =>
        Assert.Equal("INACTIVE_REASON", await RegisterErrorCode(Request(reasonCode: "000004")));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5000.01)]
    [InlineData(10.555)]
    public async Task Register_rejects_invalid_fixed_amount(double amount) =>
        Assert.Equal("INVALID_AMOUNT",
            await RegisterErrorCode(Request(bonusTypeId: BonusTypes.FixedAmount, bonusValue: (decimal)amount)));

    [Fact]
    public async Task Register_accepts_fixed_amount_equal_to_salary()
    {
        var result = await _service.RegisterAsync(
            Request(bonusTypeId: BonusTypes.FixedAmount, bonusValue: 5000m), User, CancellationToken.None);

        Assert.Equal(5000m, result.GrossAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.01)]
    [InlineData(10.555)]
    public async Task Register_rejects_invalid_percentage(double percentage) =>
        Assert.Equal("INVALID_PERCENTAGE",
            await RegisterErrorCode(Request(bonusTypeId: BonusTypes.SalaryPercentage, bonusValue: (decimal)percentage)));

    [Fact]
    public async Task Register_rejects_second_active_bonus_in_same_period()
    {
        await _service.RegisterAsync(Request(period: "2026-10"), User, CancellationToken.None);

        Assert.Equal("DUPLICATE_BONUS", await RegisterErrorCode(Request(period: "2026-10")));
    }

    [Fact]
    public async Task Register_allows_new_bonus_after_cancelling_the_previous_one()
    {
        await _service.RegisterAsync(Request(period: "2026-10"), User, CancellationToken.None);
        await _service.CancelAsync(0, User, CancellationToken.None);

        await _service.RegisterAsync(Request(period: "2026-10"), User, CancellationToken.None);

        Assert.Equal(2, _repository.Bonuses.Count);
    }

    [Fact]
    public async Task Cancel_updates_state_and_audit()
    {
        await _service.RegisterAsync(Request(), User, CancellationToken.None);

        await _service.CancelAsync(0, "rrhh", CancellationToken.None);

        var bonus = Assert.Single(_repository.Bonuses);
        Assert.Equal(BonusStates.Cancelled, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(2, _repository.SaveChangesCalls);
    }

    [Fact]
    public async Task Cancel_fails_for_unknown_bonus()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.CancelAsync(99, User, CancellationToken.None));

        Assert.Equal("BONUS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Deleted_bonus_can_no_longer_be_found()
    {
        await _service.RegisterAsync(Request(), User, CancellationToken.None);
        await _service.DeleteAsync(0, User, CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.CancelAsync(0, User, CancellationToken.None));

        Assert.True(Assert.Single(_repository.Bonuses).IsDeleted);
        Assert.Equal("BONUS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Delete_requires_user()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.DeleteAsync(0, null, CancellationToken.None));

        Assert.Equal("USER_REQUIRED", error.Code);
    }

    [Fact]
    public async Task GetActiveEmployee_returns_lookup_data()
    {
        var employee = await _service.GetActiveEmployeeAsync("TECH1", 101, CancellationToken.None);

        Assert.Equal(new EmployeeLookupDto("TECH1", 101, "Andrea Torres Vega", new DateTime(2022, 3, 1), 5000m, 8m), employee);
    }

    [Fact]
    public async Task GetActiveEmployee_rejects_terminated_employee()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(
            () => _service.GetActiveEmployeeAsync("VENTA", 202, CancellationToken.None));

        Assert.Equal("EMPLOYEE_INACTIVE", error.Code);
    }
}
```

- [ ] **Step 4: Ejecutar y confirmar que falla**

Run: `cd backend && dotnet test`
Expected: FAIL de compilación — `The type or namespace name 'BonusService' could not be found`.

- [ ] **Step 5: Implementar el servicio y registrarlo**

`backend/src/BonusTest.Application/Bonuses/BonusService.cs`
```csharp
using System.Globalization;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.Application.Bonuses;

/// <summary>
/// Casos de uso del módulo de bonos. Aplica las reglas que necesitan consultar datos;
/// el cálculo y las transiciones de estado viven en el dominio.
/// </summary>
public class BonusService(IBonusRepository repository, TimeProvider timeProvider)
{
    private const string PeriodFormat = "yyyy-MM";
    private const decimal MaxPercentage = 100m;

    private DateTime Now => timeProvider.GetLocalNow().DateTime;

    public Task<PagedResponse<BonusListItemDto>> SearchAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        repository.GetPagedAsync(filter, cancellationToken);

    public async Task<EmployeeLookupDto> GetActiveEmployeeAsync(string departmentCode, int employeeNumber,
        CancellationToken cancellationToken)
    {
        var employee = await FindActiveEmployeeAsync(departmentCode, employeeNumber, cancellationToken);

        return new EmployeeLookupDto(employee.DepartmentCode, employee.EmployeeNumber, employee.FullName,
            employee.HireDate, employee.MonthlySalary, employee.TaxRate);
    }

    public async Task<RegisteredBonusDto> RegisterAsync(RegisterBonusRequest request, string? userCode,
        CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        EnsurePeriodIsValid(request.Period);
        var employee = await FindActiveEmployeeAsync(request.DepartmentCode, request.EmployeeNumber, cancellationToken);

        if (!await repository.BonusTypeExistsAsync(request.BonusTypeId, cancellationToken))
            throw BonusErrors.InvalidBonusType();
        if (!await repository.IsReasonActiveAsync(request.ReasonCode, cancellationToken))
            throw BonusErrors.InactiveReason();
        EnsureBonusValueIsValid(request.BonusTypeId, request.BonusValue, employee.MonthlySalary);
        if (await repository.HasActiveBonusAsync(employee.DepartmentCode, employee.EmployeeNumber, request.Period, cancellationToken))
            throw BonusErrors.DuplicateBonus();

        var bonus = Bonus.Create(employee, request.Period, request.BonusTypeId, request.BonusValue,
            request.ReasonCode, user, Now);
        repository.Add(bonus);
        await repository.SaveChangesAsync(cancellationToken);

        return new RegisteredBonusDto(bonus.BonusId, bonus.GrossAmount, bonus.TaxAmount, bonus.NetAmount);
    }

    public async Task CancelAsync(int bonusId, string? userCode, CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        var bonus = await FindBonusAsync(bonusId, cancellationToken);

        bonus.Cancel(user, Now);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int bonusId, string? userCode, CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        var bonus = await FindBonusAsync(bonusId, cancellationToken);

        bonus.Delete(user, Now);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string RequireUser(string? userCode) =>
        string.IsNullOrWhiteSpace(userCode) ? throw BonusErrors.UserRequired() : userCode.Trim();

    private void EnsurePeriodIsValid(string period)
    {
        var isWellFormed = period?.Length == PeriodFormat.Length
            && DateTime.TryParseExact(period, PeriodFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        if (!isWellFormed)
            throw BonusErrors.InvalidPeriod();

        var currentPeriod = Now.ToString(PeriodFormat, CultureInfo.InvariantCulture);
        if (string.CompareOrdinal(period, currentPeriod) > 0)
            throw BonusErrors.FuturePeriod();
    }

    private static void EnsureBonusValueIsValid(int bonusTypeId, decimal bonusValue, decimal monthlySalary)
    {
        var hasAtMostTwoDecimals = Money.Round(bonusValue) == bonusValue;

        if (bonusTypeId == BonusTypes.FixedAmount
            && (bonusValue <= 0 || bonusValue > monthlySalary || !hasAtMostTwoDecimals))
            throw BonusErrors.InvalidAmount();

        if (bonusTypeId == BonusTypes.SalaryPercentage
            && (bonusValue <= 0 || bonusValue > MaxPercentage || !hasAtMostTwoDecimals))
            throw BonusErrors.InvalidPercentage();
    }

    private async Task<Employee> FindActiveEmployeeAsync(string departmentCode, int employeeNumber,
        CancellationToken cancellationToken)
    {
        var employee = await repository.GetEmployeeAsync(departmentCode, employeeNumber, cancellationToken)
            ?? throw BonusErrors.EmployeeNotFound();

        return employee.EmployeeStateCode == EmployeeStates.Active ? employee : throw BonusErrors.EmployeeInactive();
    }

    private async Task<Bonus> FindBonusAsync(int bonusId, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(bonusId, cancellationToken) ?? throw BonusErrors.BonusNotFound();
}
```

`backend/src/BonusTest.Application/DependencyInjection.cs` — reemplazar el contenido por:
```csharp
using Microsoft.Extensions.DependencyInjection;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Catalogs;

namespace BonusTest.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CatalogService>();
        services.AddScoped<BonusService>();

        return services;
    }
}
```

- [ ] **Step 6: Ejecutar y confirmar que pasa**

Run: `cd backend && dotnet test`
Expected: `Con error: 0`, `Superado: 46` (15 previos + 31 del servicio).

- [ ] **Step 7: Commit**

```bash
git add backend/src/BonusTest.Application backend/tests/BonusTest.UnitTests
git commit -m "feat(application): servicio de bonos con validaciones de negocio

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Persistencia con EF Core (Infrastructure)

**Files:**
- Create: `backend/src/BonusTest.Infrastructure/Persistence/Configurations/BonusConfigurations.cs`
- Create: `backend/src/BonusTest.Infrastructure/Repositories/BonusRepository.cs`
- Modify: `backend/src/BonusTest.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/BonusTest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IBonusRepository`, `BonusFilter`, `BonusListItemDto` (Task 4); `Bonus`, `BonusErrors` (Tasks 2-3).
- Produces: `AppDbContext.Bonuses : DbSet<Bonus>`; `BonusRepository : IBonusRepository` registrado como scoped.

- [ ] **Step 1: Configuración EF de la tabla**

`backend/src/BonusTest.Infrastructure/Persistence/Configurations/BonusConfigurations.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BonusTest.Domain.Entities;

namespace BonusTest.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapea dbo.Bonus (creada por database/02_bonus.sql). Los tipos char/varchar evitan
/// conversiones implícitas a nvarchar que impedirían usar los índices.
/// </summary>
public class BonusConfiguration : IEntityTypeConfiguration<Bonus>
{
    public void Configure(EntityTypeBuilder<Bonus> builder)
    {
        builder.ToTable("Bonus").HasKey(x => x.BonusId);

        builder.Property(x => x.DepartmentCode).HasColumnType("char(5)");
        builder.Property(x => x.Period).HasColumnType("char(7)");
        builder.Property(x => x.ReasonCode).HasColumnType("char(6)");
        builder.Property(x => x.BonusStateCode).HasColumnType("char(4)");
        builder.Property(x => x.CreatedBy).HasColumnType("varchar(50)");
        builder.Property(x => x.UpdatedBy).HasColumnType("varchar(50)");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.UpdatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.BonusValue).HasPrecision(16, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(16, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(16, 2);
        builder.Property(x => x.NetAmount).HasPrecision(16, 2);
        builder.Property(x => x.TaxRateApplied).HasPrecision(5, 2);

        builder.HasOne<Employee>().WithMany().HasForeignKey(x => new { x.DepartmentCode, x.EmployeeNumber });
        builder.HasOne<BonusType>().WithMany().HasForeignKey(x => x.BonusTypeId);
        builder.HasOne<BonusReason>().WithMany().HasForeignKey(x => x.ReasonCode);
        builder.HasOne<State>().WithMany().HasForeignKey(x => x.BonusStateCode);
    }
}
```

- [ ] **Step 2: Agregar el `DbSet`**

En `backend/src/BonusTest.Infrastructure/Persistence/AppDbContext.cs`, reemplazar la línea
`    // agrega aquí el DbSet de Bonus y su configuración.` por:
```csharp
    public DbSet<Bonus> Bonuses => Set<Bonus>();
```

- [ ] **Step 3: Repositorio**

`backend/src/BonusTest.Infrastructure/Repositories/BonusRepository.cs`
```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;
using BonusTest.Domain.Constants;
using BonusTest.Domain.Entities;
using BonusTest.Domain.Exceptions;
using BonusTest.Infrastructure.Persistence;

namespace BonusTest.Infrastructure.Repositories;

public class BonusRepository(AppDbContext context) : IBonusRepository
{
    /// <summary>Violación de índice único (UX_Bonus_ActivePerPeriod) por registros concurrentes.</summary>
    private const int UniqueIndexViolation = 2601;

    public async Task<PagedResponse<BonusListItemDto>> GetPagedAsync(BonusFilter filter, CancellationToken cancellationToken)
    {
        var bonuses = ApplyFilter(context.Bonuses.AsNoTracking().Where(x => !x.IsDeleted), filter);

        var totalRecords = await bonuses.CountAsync(cancellationToken);
        var page = bonuses
            .OrderByDescending(x => x.BonusId)
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize);
        var data = await ToListItems(page).ToListAsync(cancellationToken);

        return PagedResponse<BonusListItemDto>.Create(data, filter.PageIndex, filter.PageSize, totalRecords);
    }

    public Task<Employee?> GetEmployeeAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber, cancellationToken);

    public Task<bool> BonusTypeExistsAsync(int bonusTypeId, CancellationToken cancellationToken) =>
        context.BonusTypes.AnyAsync(x => x.BonusTypeId == bonusTypeId, cancellationToken);

    public Task<bool> IsReasonActiveAsync(string reasonCode, CancellationToken cancellationToken) =>
        context.BonusReasons.AnyAsync(x => x.ReasonCode == reasonCode && x.State, cancellationToken);

    public Task<bool> HasActiveBonusAsync(string departmentCode, int employeeNumber, string period, CancellationToken cancellationToken) =>
        context.Bonuses.AnyAsync(x => x.DepartmentCode == departmentCode
            && x.EmployeeNumber == employeeNumber
            && x.Period == period
            && !x.IsDeleted
            && x.BonusStateCode != BonusStates.Cancelled, cancellationToken);

    public Task<Bonus?> GetByIdAsync(int bonusId, CancellationToken cancellationToken) =>
        context.Bonuses.FirstOrDefaultAsync(x => x.BonusId == bonusId && !x.IsDeleted, cancellationToken);

    public void Add(Bonus bonus) => context.Bonuses.Add(bonus);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation })
        {
            throw BonusErrors.DuplicateBonus();
        }
    }

    private static IQueryable<Bonus> ApplyFilter(IQueryable<Bonus> bonuses, BonusFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.DepartmentCode))
            bonuses = bonuses.Where(x => x.DepartmentCode == filter.DepartmentCode);
        if (filter.EmployeeNumber is not null)
            bonuses = bonuses.Where(x => x.EmployeeNumber == filter.EmployeeNumber);
        if (!string.IsNullOrWhiteSpace(filter.Period))
            bonuses = bonuses.Where(x => x.Period == filter.Period);
        if (!string.IsNullOrWhiteSpace(filter.BonusStateCode))
            bonuses = bonuses.Where(x => x.BonusStateCode == filter.BonusStateCode);

        return bonuses;
    }

    /// <summary>Une la página con los catálogos para devolver descripciones listas para mostrar.</summary>
    private IQueryable<BonusListItemDto> ToListItems(IQueryable<Bonus> bonuses) =>
        from bonus in bonuses
        join state in context.States on bonus.BonusStateCode equals state.StateCode
        join bonusType in context.BonusTypes on bonus.BonusTypeId equals bonusType.BonusTypeId
        join reason in context.BonusReasons on bonus.ReasonCode equals reason.ReasonCode
        join employee in context.Employees
            on new { bonus.DepartmentCode, bonus.EmployeeNumber } equals new { employee.DepartmentCode, employee.EmployeeNumber }
        join department in context.Departments on bonus.DepartmentCode equals department.DepartmentCode
        orderby bonus.BonusId descending
        select new BonusListItemDto(
            bonus.BonusId,
            bonus.BonusStateCode,
            state.StateDescription,
            bonus.Period,
            bonus.DepartmentCode,
            department.DepartmentName,
            bonus.EmployeeNumber,
            employee.FullName,
            bonus.BonusTypeId,
            bonusType.BonusTypeDescription,
            bonus.BonusValue,
            reason.ReasonDescription,
            bonus.GrossAmount,
            bonus.TaxAmount,
            bonus.NetAmount);
}
```

- [ ] **Step 4: Registrar el repositorio**

En `backend/src/BonusTest.Infrastructure/DependencyInjection.cs`:
- agregar `using BonusTest.Application.Bonuses;`
- reemplazar `        // registra aquí los repositorios del módulo de bonos.` por:
```csharp
        services.AddScoped<IBonusRepository, BonusRepository>();
```

- [ ] **Step 5: Compilar y ejecutar pruebas**

Run: `cd backend && dotnet build BonusTest.slnx && dotnet test`
Expected: `Compilación correcta. 0 Advertencia(s) 0 Errores` y `Con error: 0, Superado: 46`.

- [ ] **Step 6: Commit**

```bash
git add backend/src/BonusTest.Infrastructure
git commit -m "feat(infrastructure): repositorio de bonos con paginación en BD

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Endpoints REST (`BonusController`)

**Files:**
- Create: `backend/src/BonusTest.Api/Controllers/BonusController.cs`

**Interfaces:**
- Consumes: `BonusService` y DTOs (Task 4).
- Produces: `GET /api/v1/Bonus`, `GET /api/v1/Bonus/employee/{departmentCode}/{employeeNumber}`, `POST /api/v1/Bonus` (201 `RegisteredBonusDto`), `PUT /api/v1/Bonus/{bonusId}/cancel` (204), `DELETE /api/v1/Bonus/{bonusId}` (204). JSON en camelCase.

- [ ] **Step 1: Implementar el controlador**

`backend/src/BonusTest.Api/Controllers/BonusController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using BonusTest.Application.Bonuses;

namespace BonusTest.Api.Controllers;

/// <summary>Módulo de bonos de desempeño. Sin reglas de negocio: delega todo en BonusService.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class BonusController(BonusService bonusService) : ControllerBase
{
    private const string UserCodeHeader = "UserCode";

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] BonusFilter filter, CancellationToken cancellationToken) =>
        Ok(await bonusService.SearchAsync(filter, cancellationToken));

    [HttpGet("employee/{departmentCode}/{employeeNumber:int}")]
    public async Task<IActionResult> GetEmployee(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        Ok(await bonusService.GetActiveEmployeeAsync(departmentCode, employeeNumber, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Register(RegisterBonusRequest request,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await bonusService.RegisterAsync(request, userCode, cancellationToken));

    [HttpPut("{bonusId:int}/cancel")]
    public async Task<IActionResult> Cancel(int bonusId,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken)
    {
        await bonusService.CancelAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{bonusId:int}")]
    public async Task<IActionResult> Delete(int bonusId,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken)
    {
        await bonusService.DeleteAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }
}
```

- [ ] **Step 2: Compilar y levantar la API**

Detener la API si está corriendo, luego:
```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd backend && dotnet build BonusTest.slnx
export ConnectionStrings__BonusTest='Server=localhost,1433;Database=BonusTest;User Id=sa;Password=BonusTest#2026;TrustServerCertificate=True;'
cd src/BonusTest.Api && dotnet run --no-build --launch-profile http   # en segundo plano
```
Expected: `Now listening on: http://localhost:5106`.

- [ ] **Step 3: Pruebas de humo con curl**

```bash
API=http://localhost:5106/api/v1/Bonus
H='-H Content-Type:application/json -H UserCode:candidato'

curl -s $API/employee/TECH1/101; echo
# Expected: {"departmentCode":"TECH1","employeeNumber":101,"fullName":"Andrea Torres Vega","hireDate":"2022-03-01T00:00:00","monthlySalary":5000.00,"taxRate":8.00}

curl -s -w ' %{http_code}' $API/employee/VENTA/202; echo
# Expected: {"code":"EMPLOYEE_INACTIVE",...} 409

curl -s -w ' %{http_code}' -X POST $API $H -d '{"departmentCode":"TECH1","employeeNumber":101,"period":"2026-10","bonusTypeId":2,"bonusValue":10,"reasonCode":"000001"}'; echo
# Expected: {"bonusId":<n>,"grossAmount":500.00,"taxAmount":40.00,"netAmount":460.00} 201

curl -s -w ' %{http_code}' -X POST $API $H -d '{"departmentCode":"TECH1","employeeNumber":101,"period":"2026-10","bonusTypeId":2,"bonusValue":10,"reasonCode":"000001"}'; echo
# Expected: {"code":"DUPLICATE_BONUS",...} 409

curl -s -X POST $API $H -d '{"departmentCode":"TECH1","employeeNumber":102,"period":"2026-12","bonusTypeId":1,"bonusValue":100,"reasonCode":"000001"}'; echo
# Expected: FUTURE_PERIOD

curl -s -X POST $API $H -d '{"departmentCode":"TECH1","employeeNumber":102,"period":"2026-10","bonusTypeId":1,"bonusValue":3000.01,"reasonCode":"000001"}'; echo
# Expected: INVALID_AMOUNT

curl -s -X POST $API -H Content-Type:application/json -d '{"departmentCode":"TECH1","employeeNumber":102,"period":"2026-10","bonusTypeId":1,"bonusValue":100,"reasonCode":"000001"}'; echo
# Expected: USER_REQUIRED

curl -s "$API?pageIndex=1&pageSize=10&departmentCode=TECH1"; echo
# Expected: {"pageNumber":1,"pageSize":10,"totalPages":1,"totalRecords":1,"data":[{..."stateDescription":"Registrado"...,"departmentName":"Tecnología","fullName":"Andrea Torres Vega","bonusTypeDescription":"Porcentaje del sueldo","reasonDescription":"Cumplimiento de metas","grossAmount":500.00,...}]}

ID=$(curl -s "$API?departmentCode=TECH1&employeeNumber=101&period=2026-10" | grep -o '"bonusId":[0-9]*' | head -1 | cut -d: -f2)
curl -s -w '%{http_code}' -X PUT $API/$ID/cancel $H; echo        # Expected: 204
curl -s -w ' %{http_code}' -X PUT $API/$ID/cancel $H; echo       # Expected: BONUS_NOT_REGISTERED 409
curl -s -w ' %{http_code}' -X DELETE $API/$ID $H; echo           # Expected: BONUS_NOT_REGISTERED 409

curl -s -X POST $API $H -d '{"departmentCode":"TECH1","employeeNumber":103,"period":"2026-10","bonusTypeId":1,"bonusValue":700,"reasonCode":"000002"}'; echo
ID2=$(curl -s "$API?employeeNumber=103&period=2026-10" | grep -o '"bonusId":[0-9]*' | head -1 | cut -d: -f2)
curl -s -w '%{http_code}' -X DELETE $API/$ID2 $H; echo           # Expected: 204
curl -s "$API?employeeNumber=103&period=2026-10"; echo           # Expected: "totalRecords":0
```

- [ ] **Step 4: Verificar en la BD el eliminado lógico y la auditoría**

```bash
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -d BonusTest \
  -Q "SELECT BonusId, EmployeeNumber, BonusStateCode, IsDeleted, CreatedBy, UpdatedBy FROM dbo.Bonus"
```
Expected: el bono de 103 con `IsDeleted = 1` y `UpdatedBy = candidato`; el de 101 con `0203`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/BonusTest.Api/Controllers/BonusController.cs
git commit -m "feat(api): endpoints de bonos (listado, empleado, registro, anular, eliminar)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Base del frontend (Material, modelos, servicio, utilidades)

**Files:**
- Modify: `frontend/package.json` (vía npm), `frontend/angular.json`, `frontend/src/index.html`, `frontend/src/styles.css`, `frontend/src/app/app.css`, `frontend/src/app/app.config.ts`
- Create: `frontend/src/app/core/models/bonus.ts`
- Create: `frontend/src/app/core/services/bonus-api.ts`
- Create: `frontend/src/app/core/utils/bonus-calculator.ts`
- Create: `frontend/src/app/core/utils/period.ts`
- Create: `frontend/src/app/core/i18n/spanish-paginator-intl.ts`
- Create: `frontend/src/app/shared/confirm-dialog/confirm-dialog.ts`

**Interfaces:**
- Consumes: endpoints de Task 6; `PagedResponse<T>`, `BonusTypes`, `environment` (base).
- Produces:
  - Tipos `BonusFilter`, `BonusListItem`, `EmployeeLookup`, `RegisterBonusRequest`, `RegisteredBonus`, `BonusAmounts`, constante `EMPTY_BONUS_FILTER`.
  - `BonusApi.search(filter: BonusFilter, pageIndex: number, pageSize: number): Observable<PagedResponse<BonusListItem>>`, `getEmployee(departmentCode: string, employeeNumber: number): Observable<EmployeeLookup>`, `register(request: RegisterBonusRequest): Observable<RegisteredBonus>`, `cancel(bonusId: number): Observable<void>`, `delete(bonusId: number): Observable<void>`.
  - `calculateBonus(bonusTypeId, bonusValue, monthlySalary, taxRate): BonusAmounts`, `roundMoney(value): number`.
  - `currentPeriod(today?: Date): string`, `PERIOD_PATTERN: RegExp`, `maxPeriodValidator(maxPeriod: string): ValidatorFn`.
  - `ConfirmDialog`, `ConfirmDialogData`, `confirmAction(dialog: MatDialog, data: ConfirmDialogData): Observable<boolean>`.

- [ ] **Step 1: Instalar Angular Material**

```bash
cd frontend && npm install @angular/material@22.2.7 @angular/cdk@22.2.7
```
Expected: `added 2 packages` (o similar) sin errores de peer dependencies.

- [ ] **Step 2: Tema, fuentes y estilos globales**

`frontend/angular.json` — en `projects.frontend.architect.build.options.styles` dejar:
```json
            "styles": [
              "@angular/material/prebuilt-themes/azure-blue.css",
              "src/styles.css"
            ]
```

`frontend/src/index.html` — reemplazar el contenido por:
```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <title>Bonos - Prueba técnica</title>
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <link rel="icon" type="image/x-icon" href="favicon.ico">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap" rel="stylesheet">
  <link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet">
</head>
<body class="mat-typography">
  <app-root></app-root>
</body>
</html>
```

`frontend/src/styles.css` — reemplazar el contenido por (se quitan los estilos genéricos de `select`, `input` y `.btn`, que romperían los campos de Material):
```css
/* Estilos globales. Los componentes usan Angular Material (tema azure-blue). */
:root {
  --primary: #1f5fa8;
  --text: #1f2933;
  --text-muted: #616e7c;
  --border: #d9e2ec;
  --surface: #ffffff;
  --background: #f5f7fa;
  --info-bg: #e3f0fb;
  --info-text: #1f5fa8;
  --success-bg: #e8f5e9;
  --success-text: #1b5e20;
  --neutral-bg: #eef2f6;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  font-family: Roboto, 'Segoe UI', sans-serif;
  color: var(--text);
  background: var(--background);
}

.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 20px;
}

.amount {
  text-align: right;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
```

`frontend/src/app/app.css` — reemplazar el bloque `.notification { ... }` por (el aviso flota sobre el modal para que los errores 409 se vean aunque el diálogo esté abierto):
```css
.notification {
  position: fixed;
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1100;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  width: min(720px, calc(100% - 32px));
  padding: 12px 16px;
  border-radius: 6px;
  box-shadow: 0 4px 16px rgb(0 0 0 / 15%);
}
```

- [ ] **Step 3: Proveedores globales (paginador en español y campos outline)**

`frontend/src/app/core/i18n/spanish-paginator-intl.ts`
```ts
import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

@Injectable()
export class SpanishPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'Filas por página';
  override nextPageLabel = 'Página siguiente';
  override previousPageLabel = 'Página anterior';
  override firstPageLabel = 'Primera página';
  override lastPageLabel = 'Última página';

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0) {
      return '0 de 0';
    }
    const start = page * pageSize + 1;
    const end = Math.min(start + pageSize - 1, length);
    return `${start} – ${end} de ${length}`;
  };
}
```

`frontend/src/app/app.config.ts` — reemplazar el contenido por:
```ts
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { SpanishPaginatorIntl } from './core/i18n/spanish-paginator-intl';
import { apiInterceptor } from './core/interceptors/api-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([apiInterceptor])),
    { provide: MatPaginatorIntl, useClass: SpanishPaginatorIntl },
    {
      provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
      useValue: { appearance: 'outline', subscriptSizing: 'dynamic' },
    },
  ],
};
```

- [ ] **Step 4: Modelos, servicio y utilidades**

`frontend/src/app/core/models/bonus.ts`
```ts
export interface BonusFilter {
  departmentCode: string;
  employeeNumber: number | null;
  period: string;
  bonusStateCode: string;
}

export const EMPTY_BONUS_FILTER: BonusFilter = {
  departmentCode: '',
  employeeNumber: null,
  period: '',
  bonusStateCode: '',
};

export interface BonusListItem {
  bonusId: number;
  bonusStateCode: string;
  stateDescription: string;
  period: string;
  departmentCode: string;
  departmentName: string;
  employeeNumber: number;
  fullName: string;
  bonusTypeId: number;
  bonusTypeDescription: string;
  bonusValue: number;
  reasonDescription: string;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

export interface EmployeeLookup {
  departmentCode: string;
  employeeNumber: number;
  fullName: string;
  hireDate: string;
  monthlySalary: number;
  taxRate: number;
}

export interface RegisterBonusRequest {
  departmentCode: string;
  employeeNumber: number;
  period: string;
  bonusTypeId: number;
  bonusValue: number;
  reasonCode: string;
}

export interface RegisteredBonus {
  bonusId: number;
  grossAmount: number;
  taxAmount: number;
  netAmount: number;
}

export interface BonusAmounts {
  gross: number;
  tax: number;
  net: number;
}
```

`frontend/src/app/core/services/bonus-api.ts`
```ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  BonusFilter,
  BonusListItem,
  EmployeeLookup,
  RegisterBonusRequest,
  RegisteredBonus,
} from '../models/bonus';
import { PagedResponse } from '../models/paged-response';

@Injectable({ providedIn: 'root' })
export class BonusApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/Bonus`;

  search(filter: BonusFilter, pageIndex: number, pageSize: number): Observable<PagedResponse<BonusListItem>> {
    let params = new HttpParams().set('pageIndex', pageIndex).set('pageSize', pageSize);
    if (filter.departmentCode) params = params.set('departmentCode', filter.departmentCode);
    if (filter.employeeNumber !== null) params = params.set('employeeNumber', filter.employeeNumber);
    if (filter.period) params = params.set('period', filter.period);
    if (filter.bonusStateCode) params = params.set('bonusStateCode', filter.bonusStateCode);
    return this.http.get<PagedResponse<BonusListItem>>(this.baseUrl, { params });
  }

  getEmployee(departmentCode: string, employeeNumber: number): Observable<EmployeeLookup> {
    return this.http.get<EmployeeLookup>(
      `${this.baseUrl}/employee/${encodeURIComponent(departmentCode)}/${employeeNumber}`,
    );
  }

  register(request: RegisterBonusRequest): Observable<RegisteredBonus> {
    return this.http.post<RegisteredBonus>(this.baseUrl, request);
  }

  cancel(bonusId: number): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${bonusId}/cancel`, null);
  }

  delete(bonusId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${bonusId}`);
  }
}
```

`frontend/src/app/core/utils/bonus-calculator.ts`
```ts
import { BonusAmounts } from '../models/bonus';
import { BonusTypes } from '../models/catalog';

/** Redondea a 2 decimales con el medio hacia arriba. */
export function roundMoney(value: number): number {
  return (Math.sign(value) * Math.round((Math.abs(value) + Number.EPSILON) * 100)) / 100;
}

/**
 * Replica la fórmula de la API solo para la vista previa del modal.
 * El monto oficial siempre lo calcula y guarda el backend.
 */
export function calculateBonus(
  bonusTypeId: number,
  bonusValue: number,
  monthlySalary: number,
  taxRate: number,
): BonusAmounts {
  const gross =
    bonusTypeId === BonusTypes.FixedAmount
      ? roundMoney(bonusValue)
      : roundMoney((monthlySalary * bonusValue) / 100);
  const tax = roundMoney((gross * taxRate) / 100);
  return { gross, tax, net: roundMoney(gross - tax) };
}
```

`frontend/src/app/core/utils/period.ts`
```ts
import { ValidatorFn } from '@angular/forms';

export const PERIOD_PATTERN = /^\d{4}-(0[1-9]|1[0-2])$/;

/** Mes actual en formato YYYY-MM. */
export function currentPeriod(today: Date = new Date()): string {
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}`;
}

/** Rechaza periodos posteriores a maxPeriod (la comparación de texto funciona con YYYY-MM). */
export function maxPeriodValidator(maxPeriod: string): ValidatorFn {
  return (control) =>
    typeof control.value === 'string' && control.value > maxPeriod ? { maxPeriod: true } : null;
}
```

- [ ] **Step 5: Diálogo de confirmación reutilizable**

`frontend/src/app/shared/confirm-dialog/confirm-dialog.ts`
```ts
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { Observable, map } from 'rxjs';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText: string;
}

@Component({
  selector: 'app-confirm-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>Cancelar</button>
      <button matButton="filled" type="button" [mat-dialog-close]="true" cdkFocusInitial>
        {{ data.confirmText }}
      </button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}

/** Abre la confirmación y emite true solo si el usuario acepta. */
export function confirmAction(dialog: MatDialog, data: ConfirmDialogData): Observable<boolean> {
  return dialog
    .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, { data, width: '420px' })
    .afterClosed()
    .pipe(map((accepted) => accepted === true));
}
```

- [ ] **Step 6: Compilar**

Run: `cd frontend && npx ng build --configuration development`
Expected: `Application bundle generation complete.` sin errores.

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "feat(front): Angular Material, modelos y servicio de bonos

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Pantalla de listado (filtros, tabla, paginación, anular y eliminar)

**Files:**
- Modify (reemplazar contenido): `frontend/src/app/features/bonuses/bonuses.ts`, `bonuses.html`, `bonuses.css`

**Interfaces:**
- Consumes: `BonusApi`, `CatalogApi`, `Notification`, `confirmAction`, `EMPTY_BONUS_FILTER`, `BonusStates`, `BonusTypes` (Task 7 y base).
- Produces: componente `Bonuses` con método protegido `reload(resetPage: boolean)` y `openRegister()` (este último se completa en Task 9; aquí el botón queda deshabilitado).

- [ ] **Step 1: Componente**

`frontend/src/app/features/bonuses/bonuses.ts`
```ts
import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { EMPTY, Observable, Subject, catchError, filter, finalize, noop, switchMap } from 'rxjs';
import { BonusFilter, BonusListItem, EMPTY_BONUS_FILTER } from '../../core/models/bonus';
import { BonusStates, BonusTypes, MasterStateCodes } from '../../core/models/catalog';
import { PagedResponse } from '../../core/models/paged-response';
import { BonusApi } from '../../core/services/bonus-api';
import { CatalogApi } from '../../core/services/catalog-api';
import { Notification } from '../../core/services/notification';
import { ConfirmDialogData, confirmAction } from '../../shared/confirm-dialog/confirm-dialog';

/** Pantalla de bonos de desempeño: filtros, listado paginado en servidor y acciones por fila. */
@Component({
  selector: 'app-bonuses',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './bonuses.html',
  styleUrl: './bonuses.css',
})
export class Bonuses {
  private readonly bonusApi = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly dialog = inject(MatDialog);
  private readonly notification = inject(Notification);
  private readonly reload$ = new Subject<void>();
  private appliedFilter: BonusFilter = EMPTY_BONUS_FILTER;

  protected readonly pageSize = 10;
  protected readonly displayedColumns = [
    'actions', 'state', 'period', 'employee', 'fullName', 'type', 'value', 'reason', 'gross', 'tax', 'net',
  ];
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusStates = toSignal(this.catalogApi.getStates(MasterStateCodes.Bonus), { initialValue: [] });
  protected readonly loading = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly page = signal<PagedResponse<BonusListItem> | null>(null);

  protected readonly filterForm = inject(NonNullableFormBuilder).group({
    departmentCode: [''],
    employeeNumber: [null as number | null, Validators.min(1)],
    period: [''],
    bonusStateCode: [''],
  });

  constructor() {
    this.reload$
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          return this.bonusApi.search(this.appliedFilter, this.pageIndex() + 1, this.pageSize).pipe(
            catchError(() => EMPTY), // el interceptor ya mostró el error
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((response) => this.showPage(response));
    this.reload$.next();
  }

  protected search(): void {
    if (this.filterForm.invalid) {
      this.filterForm.markAllAsTouched();
      return;
    }
    this.appliedFilter = this.filterForm.getRawValue();
    this.reload(true);
  }

  protected clear(): void {
    this.filterForm.reset();
    this.search();
  }

  protected changePage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.reload$.next();
  }

  protected isRegistered(bonus: BonusListItem): boolean {
    return bonus.bonusStateCode === BonusStates.Registered;
  }

  protected isPercentage(bonus: BonusListItem): boolean {
    return bonus.bonusTypeId === BonusTypes.SalaryPercentage;
  }

  protected cancel(bonus: BonusListItem): void {
    this.runConfirmedAction(
      {
        title: 'Anular bono',
        message: `¿Anular el bono de ${bonus.fullName} del periodo ${bonus.period}? Esta acción no se puede deshacer.`,
        confirmText: 'Anular',
      },
      () => this.bonusApi.cancel(bonus.bonusId),
      'Bono anulado.',
    );
  }

  protected remove(bonus: BonusListItem): void {
    this.runConfirmedAction(
      {
        title: 'Eliminar bono',
        message: `¿Eliminar el bono de ${bonus.fullName} del periodo ${bonus.period}? Dejará de aparecer en el listado.`,
        confirmText: 'Eliminar',
      },
      () => this.bonusApi.delete(bonus.bonusId),
      'Bono eliminado.',
    );
  }

  protected reload(resetPage: boolean): void {
    if (resetPage) {
      this.pageIndex.set(0);
    }
    this.reload$.next();
  }

  private runConfirmedAction(data: ConfirmDialogData, action: () => Observable<void>, successMessage: string): void {
    confirmAction(this.dialog, data)
      .pipe(filter(Boolean), switchMap(action))
      .subscribe({
        next: () => {
          this.notification.show('success', successMessage);
          this.reload(false);
        },
        error: noop, // el interceptor ya mostró el error
      });
  }

  /** Si la página quedó vacía (p. ej., se eliminó su último bono), retrocede una página. */
  private showPage(response: PagedResponse<BonusListItem>): void {
    if (response.data.length === 0 && this.pageIndex() > 0) {
      this.pageIndex.update((index) => index - 1);
      this.reload$.next();
      return;
    }
    this.page.set(response);
  }
}
```

- [ ] **Step 2: Plantilla**

`frontend/src/app/features/bonuses/bonuses.html`
```html
<section class="card">
  <header class="card-header">
    <div>
      <h2>Bonos de desempeño</h2>
      <p class="subtitle">Registra, anula y elimina bonos por periodo.</p>
    </div>
    <button matButton="filled" type="button" disabled>
      <mat-icon>add</mat-icon>
      Nuevo registro
    </button>
  </header>

  <form class="filters" [formGroup]="filterForm" (ngSubmit)="search()">
    <mat-form-field>
      <mat-label>Área</mat-label>
      <mat-select formControlName="departmentCode">
        <mat-option value="">Todas</mat-option>
        @for (department of departments(); track department.departmentCode) {
          <mat-option [value]="department.departmentCode">{{ department.departmentName }}</mat-option>
        }
      </mat-select>
    </mat-form-field>

    <mat-form-field>
      <mat-label>N.º de empleado</mat-label>
      <input matInput type="number" min="1" formControlName="employeeNumber" />
      <mat-error>Debe ser un número mayor a 0</mat-error>
    </mat-form-field>

    <mat-form-field>
      <mat-label>Periodo</mat-label>
      <input matInput type="month" formControlName="period" />
    </mat-form-field>

    <mat-form-field>
      <mat-label>Estado</mat-label>
      <mat-select formControlName="bonusStateCode">
        <mat-option value="">Todos</mat-option>
        @for (state of bonusStates(); track state.stateCode) {
          <mat-option [value]="state.stateCode">{{ state.stateDescription }}</mat-option>
        }
      </mat-select>
    </mat-form-field>

    <div class="filter-actions">
      <button matButton="filled" type="submit">
        <mat-icon>search</mat-icon>
        Buscar
      </button>
      <button matButton="outlined" type="button" (click)="clear()">Limpiar</button>
    </div>
  </form>

  <div class="progress-slot">
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" aria-label="Cargando bonos" />
    }
  </div>

  <div class="table-wrapper">
    <table mat-table [dataSource]="page()?.data ?? []">
      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>Acciones</th>
        <td mat-cell *matCellDef="let bonus">
          <button
            matIconButton
            type="button"
            matTooltip="Anular"
            aria-label="Anular bono"
            [disabled]="!isRegistered(bonus)"
            (click)="cancel(bonus)"
          >
            <mat-icon>block</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            matTooltip="Eliminar"
            aria-label="Eliminar bono"
            [disabled]="!isRegistered(bonus)"
            (click)="remove(bonus)"
          >
            <mat-icon>delete</mat-icon>
          </button>
        </td>
      </ng-container>

      <ng-container matColumnDef="state">
        <th mat-header-cell *matHeaderCellDef>Estado</th>
        <td mat-cell *matCellDef="let bonus">
          <span [class]="'state-chip state-' + bonus.bonusStateCode">{{ bonus.stateDescription }}</span>
        </td>
      </ng-container>

      <ng-container matColumnDef="period">
        <th mat-header-cell *matHeaderCellDef>Periodo</th>
        <td mat-cell *matCellDef="let bonus">{{ bonus.period }}</td>
      </ng-container>

      <ng-container matColumnDef="employee">
        <th mat-header-cell *matHeaderCellDef>Área / N.º</th>
        <td mat-cell *matCellDef="let bonus">
          {{ bonus.departmentCode }}-{{ bonus.employeeNumber }}
          <span class="secondary">{{ bonus.departmentName }}</span>
        </td>
      </ng-container>

      <ng-container matColumnDef="fullName">
        <th mat-header-cell *matHeaderCellDef>Nombre</th>
        <td mat-cell *matCellDef="let bonus">{{ bonus.fullName }}</td>
      </ng-container>

      <ng-container matColumnDef="type">
        <th mat-header-cell *matHeaderCellDef>Tipo</th>
        <td mat-cell *matCellDef="let bonus">{{ bonus.bonusTypeDescription }}</td>
      </ng-container>

      <ng-container matColumnDef="value">
        <th mat-header-cell *matHeaderCellDef class="amount">Monto / %</th>
        <td mat-cell *matCellDef="let bonus" class="amount">
          {{ bonus.bonusValue | number: '1.2-2' }}{{ isPercentage(bonus) ? ' %' : '' }}
        </td>
      </ng-container>

      <ng-container matColumnDef="reason">
        <th mat-header-cell *matHeaderCellDef>Motivo</th>
        <td mat-cell *matCellDef="let bonus">{{ bonus.reasonDescription }}</td>
      </ng-container>

      <ng-container matColumnDef="gross">
        <th mat-header-cell *matHeaderCellDef class="amount">Bruto</th>
        <td mat-cell *matCellDef="let bonus" class="amount">{{ bonus.grossAmount | number: '1.2-2' }}</td>
      </ng-container>

      <ng-container matColumnDef="tax">
        <th mat-header-cell *matHeaderCellDef class="amount">Retención</th>
        <td mat-cell *matCellDef="let bonus" class="amount">{{ bonus.taxAmount | number: '1.2-2' }}</td>
      </ng-container>

      <ng-container matColumnDef="net">
        <th mat-header-cell *matHeaderCellDef class="amount">Neto</th>
        <td mat-cell *matCellDef="let bonus" class="amount net">{{ bonus.netAmount | number: '1.2-2' }}</td>
      </ng-container>

      <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
      <tr mat-row *matRowDef="let row; columns: displayedColumns"></tr>
      <tr class="mat-mdc-row" *matNoDataRow>
        <td class="empty" [attr.colspan]="displayedColumns.length">
          {{ loading() ? 'Cargando…' : 'No hay bonos con estos filtros.' }}
        </td>
      </tr>
    </table>
  </div>

  <footer class="table-footer">
    <span class="total">Total: {{ page()?.totalRecords ?? 0 }} registros</span>
    <mat-paginator
      [length]="page()?.totalRecords ?? 0"
      [pageSize]="pageSize"
      [pageIndex]="pageIndex()"
      [hidePageSize]="true"
      [showFirstLastButtons]="true"
      (page)="changePage($event)"
      aria-label="Paginación de bonos"
    />
  </footer>
</section>
```

- [ ] **Step 3: Estilos del componente**

`frontend/src/app/features/bonuses/bonuses.css`
```css
.card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 20px;
}

.card-header h2 {
  margin: 0;
  font-size: 1.5rem;
}

.subtitle {
  margin: 4px 0 0;
  color: var(--text-muted);
}

.filters {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
  align-items: start;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 56px;
}

.progress-slot {
  height: 4px;
  margin: 12px 0 8px;
}

.table-wrapper {
  overflow-x: auto;
  border: 1px solid var(--border);
  border-radius: 8px;
}

table {
  width: 100%;
  min-width: 1100px;
}

.mat-column-actions {
  width: 104px;
  white-space: nowrap;
}

.secondary {
  display: block;
  font-size: 0.8rem;
  color: var(--text-muted);
}

.net {
  font-weight: 500;
}

.state-chip {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 500;
  white-space: nowrap;
}

.state-0201 {
  background: var(--info-bg);
  color: var(--info-text);
}

.state-0202 {
  background: var(--success-bg);
  color: var(--success-text);
}

.state-0203 {
  background: var(--neutral-bg);
  color: var(--text-muted);
}

.empty {
  padding: 32px 16px;
  text-align: center;
  color: var(--text-muted);
}

.table-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}

.total {
  color: var(--text-muted);
}
```

- [ ] **Step 4: Compilar**

Run: `cd frontend && npx ng build --configuration development`
Expected: sin errores ni advertencias de presupuesto de estilos.

- [ ] **Step 5: Datos para paginar (25 bonos de TECH1-101, 2024-11 a 2026-11)**

Con la API corriendo:
```bash
for y in 2024 2025 2026; do for m in 01 02 03 04 05 06 07 08 09 10 11 12; do
  p="$y-$m"; [[ "$p" < "2024-11" || "$p" > "2026-11" ]] && continue
  curl -s -o /dev/null -X POST http://localhost:5106/api/v1/Bonus -H Content-Type:application/json -H UserCode:candidato \
    -d "{\"departmentCode\":\"TECH1\",\"employeeNumber\":101,\"period\":\"$p\",\"bonusTypeId\":2,\"bonusValue\":5,\"reasonCode\":\"000001\"}"
done; done
```
(El periodo 2026-10 ya está anulado en Task 6, así que se permite uno nuevo.)

- [ ] **Step 6: Verificar en el navegador**

Con `npx ng serve` activo, abrir http://localhost:4200/bonuses (Playwright) y comprobar:
- La tabla muestra 10 filas, "Total: N registros", y el paginador "1 – 10 de N"; al ir a la página 2 se hace un `GET /api/v1/Bonus?pageIndex=2&pageSize=10` (ver pestaña de red).
- Filtro Periodo = 2026-10 + Buscar → solo bonos de ese periodo; Limpiar → vuelve al listado completo, página 1.
- En un bono Registrado, Anular → confirmación → aviso "Bono anulado." y el chip cambia a Anulado con los botones deshabilitados.
- Eliminar → confirmación → el bono desaparece del listado.
- Captura de pantalla de escritorio (1280×800) y de celular (390×844): la tabla hace scroll horizontal dentro de su contenedor sin desbordar la página.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/bonuses
git commit -m "feat(front): listado paginado de bonos con filtros, anular y eliminar

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Modal de registro

**Files:**
- Create: `frontend/src/app/features/bonuses/register-bonus-dialog/register-bonus-dialog.ts`
- Create: `frontend/src/app/features/bonuses/register-bonus-dialog/register-bonus-dialog.html`
- Create: `frontend/src/app/features/bonuses/register-bonus-dialog/register-bonus-dialog.css`
- Modify: `frontend/src/app/features/bonuses/bonuses.ts`, `bonuses.html`

**Interfaces:**
- Consumes: `BonusApi.getEmployee`, `BonusApi.register`, `CatalogApi`, `calculateBonus`, `currentPeriod`, `PERIOD_PATTERN`, `maxPeriodValidator` (Task 7); `Bonuses.reload(resetPage)` (Task 8).
- Produces: `RegisterBonusDialog` que cierra con `true` cuando registra.

- [ ] **Step 1: Componente del modal**

`frontend/src/app/features/bonuses/register-bonus-dialog/register-bonus-dialog.ts`
```ts
import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { finalize, map, noop } from 'rxjs';
import { EmployeeLookup } from '../../../core/models/bonus';
import { BonusTypes } from '../../../core/models/catalog';
import { BonusApi } from '../../../core/services/bonus-api';
import { CatalogApi } from '../../../core/services/catalog-api';
import { calculateBonus } from '../../../core/utils/bonus-calculator';
import { PERIOD_PATTERN, currentPeriod, maxPeriodValidator } from '../../../core/utils/period';

const TWO_DECIMALS_PATTERN = /^\d+(\.\d{1,2})?$/;
const MAX_PERCENTAGE = 100;

/** Modal para buscar un empleado activo y registrarle un bono con vista previa del cálculo. */
@Component({
  selector: 'app-register-bonus-dialog',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatRadioModule,
    MatSelectModule,
  ],
  templateUrl: './register-bonus-dialog.html',
  styleUrl: './register-bonus-dialog.css',
})
export class RegisterBonusDialog {
  private readonly bonusApi = inject(BonusApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly dialogRef = inject<MatDialogRef<RegisterBonusDialog, boolean>>(MatDialogRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly maxPeriod = currentPeriod();
  protected readonly departments = toSignal(this.catalogApi.getDepartments(), { initialValue: [] });
  protected readonly bonusTypes = toSignal(this.catalogApi.getBonusTypes(), { initialValue: [] });
  protected readonly reasons = toSignal(this.catalogApi.getBonusReasons(), { initialValue: [] });
  protected readonly employee = signal<EmployeeLookup | null>(null);
  protected readonly searching = signal(false);
  protected readonly saving = signal(false);

  protected readonly searchForm = this.fb.group({
    departmentCode: ['', Validators.required],
    employeeNumber: [null as number | null, [Validators.required, Validators.min(1)]],
  });

  protected readonly bonusForm = this.fb.group({
    period: [this.maxPeriod, [Validators.required, Validators.pattern(PERIOD_PATTERN), maxPeriodValidator(this.maxPeriod)]],
    bonusTypeId: [BonusTypes.SalaryPercentage as number, Validators.required],
    bonusValue: [null as number | null, [Validators.required, (control: AbstractControl) => this.validateBonusValue(control)]],
    reasonCode: ['', Validators.required],
  });

  private readonly formValue = toSignal(
    this.bonusForm.valueChanges.pipe(map(() => this.bonusForm.getRawValue())),
    { initialValue: this.bonusForm.getRawValue() },
  );

  protected readonly isPercentage = computed(() => this.formValue().bonusTypeId === BonusTypes.SalaryPercentage);
  protected readonly valueLabel = computed(() => (this.isPercentage() ? 'Porcentaje del sueldo' : 'Monto fijo'));
  protected readonly maxBonusValue = computed(() =>
    this.isPercentage() ? MAX_PERCENTAGE : (this.employee()?.monthlySalary ?? 0),
  );
  protected readonly preview = computed(() => {
    const employee = this.employee();
    const { bonusTypeId, bonusValue } = this.formValue();
    if (!employee || bonusValue === null || bonusValue <= 0) {
      return { gross: 0, tax: 0, net: 0 };
    }
    return calculateBonus(bonusTypeId, bonusValue, employee.monthlySalary, employee.taxRate);
  });

  constructor() {
    // Cambiar el tipo cambia el máximo permitido: se revalida el valor.
    this.bonusForm.controls.bonusTypeId.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.bonusForm.controls.bonusValue.updateValueAndValidity());
    // Si se cambia el área o el número, el empleado mostrado ya no corresponde.
    this.searchForm.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.employee.set(null));
  }

  protected searchEmployee(): void {
    if (this.searchForm.invalid) {
      this.searchForm.markAllAsTouched();
      return;
    }
    const { departmentCode, employeeNumber } = this.searchForm.getRawValue();
    this.searching.set(true);
    this.bonusApi
      .getEmployee(departmentCode, employeeNumber!)
      .pipe(finalize(() => this.searching.set(false)))
      .subscribe({
        next: (employee) => {
          this.employee.set(employee);
          this.bonusForm.controls.bonusValue.updateValueAndValidity();
        },
        error: noop, // el interceptor ya mostró el motivo (no existe o está cesado)
      });
  }

  protected save(): void {
    const employee = this.employee();
    if (!employee || this.bonusForm.invalid) {
      this.bonusForm.markAllAsTouched();
      return;
    }
    const { period, bonusTypeId, bonusValue, reasonCode } = this.bonusForm.getRawValue();
    this.saving.set(true);
    this.bonusApi
      .register({
        departmentCode: employee.departmentCode,
        employeeNumber: employee.employeeNumber,
        period,
        bonusTypeId,
        bonusValue: bonusValue!,
        reasonCode,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({ next: () => this.dialogRef.close(true), error: noop });
  }

  protected bonusValueError(): string {
    const errors = this.bonusForm.controls.bonusValue.errors;
    if (errors?.['required']) return 'Ingresa un valor';
    if (errors?.['min']) return 'Debe ser mayor a 0';
    if (errors?.['max']) return `No puede superar ${errors['max'].max}`;
    if (errors?.['decimals']) return 'Usa hasta 2 decimales';
    return '';
  }

  private validateBonusValue(control: AbstractControl): ValidationErrors | null {
    const value = control.value as number | null;
    if (value === null) return null;
    if (value <= 0) return { min: true };
    if (!TWO_DECIMALS_PATTERN.test(String(value))) return { decimals: true };
    const max = this.maxBonusValue();
    return this.employee() && value > max ? { max: { max } } : null;
  }
}
```

> Nota: `validateBonusValue` lee `maxBonusValue()` (signal), y por eso el validador se reejecuta explícitamente al cambiar el tipo o el empleado (ver constructor y `searchEmployee`).

- [ ] **Step 2: Plantilla del modal**

`frontend/src/app/features/bonuses/register-bonus-dialog/register-bonus-dialog.html`
```html
<h2 mat-dialog-title>Registrar bono</h2>

<mat-dialog-content>
  <form class="search" [formGroup]="searchForm" (ngSubmit)="searchEmployee()">
    <mat-form-field>
      <mat-label>Área</mat-label>
      <mat-select formControlName="departmentCode" required>
        @for (department of departments(); track department.departmentCode) {
          <mat-option [value]="department.departmentCode">{{ department.departmentName }}</mat-option>
        }
      </mat-select>
      <mat-error>Selecciona el área</mat-error>
    </mat-form-field>

    <mat-form-field>
      <mat-label>N.º de empleado</mat-label>
      <input matInput type="number" min="1" formControlName="employeeNumber" required />
      <mat-error>Ingresa un número de empleado válido</mat-error>
    </mat-form-field>

    <button matButton="tonal" type="submit" class="search-button" [disabled]="searching()">
      <mat-icon>person_search</mat-icon>
      Buscar
    </button>
  </form>

  <div class="progress-slot">
    @if (searching()) {
      <mat-progress-bar mode="indeterminate" aria-label="Buscando empleado" />
    }
  </div>

  @if (employee(); as employee) {
    <dl class="employee-card">
      <div>
        <dt>Empleado</dt>
        <dd>{{ employee.fullName }}</dd>
      </div>
      <div>
        <dt>Fecha de ingreso</dt>
        <dd>{{ employee.hireDate | date: 'dd/MM/yyyy' }}</dd>
      </div>
      <div>
        <dt>Sueldo mensual</dt>
        <dd>{{ employee.monthlySalary | number: '1.2-2' }}</dd>
      </div>
      <div>
        <dt>Retención</dt>
        <dd>{{ employee.taxRate | number: '1.0-2' }} %</dd>
      </div>
    </dl>

    <form id="bonus-form" class="bonus-form" [formGroup]="bonusForm" (ngSubmit)="save()">
      <mat-form-field>
        <mat-label>Periodo</mat-label>
        <input matInput type="month" formControlName="period" [max]="maxPeriod" required />
        @if (bonusForm.controls.period.hasError('maxPeriod')) {
          <mat-error>No puede ser posterior a {{ maxPeriod }}</mat-error>
        } @else {
          <mat-error>Ingresa el periodo (YYYY-MM)</mat-error>
        }
      </mat-form-field>

      <mat-radio-group formControlName="bonusTypeId" aria-label="Tipo de bono" class="types">
        @for (type of bonusTypes(); track type.bonusTypeId) {
          <mat-radio-button [value]="type.bonusTypeId">{{ type.bonusTypeDescription }}</mat-radio-button>
        }
      </mat-radio-group>

      <mat-form-field>
        <mat-label>{{ valueLabel() }}</mat-label>
        <input matInput type="number" min="0.01" step="0.01" formControlName="bonusValue" required />
        @if (isPercentage()) {
          <span matTextSuffix>%</span>
        }
        <mat-hint>Máximo {{ maxBonusValue() | number: '1.2-2' }}</mat-hint>
        <mat-error>{{ bonusValueError() }}</mat-error>
      </mat-form-field>

      <mat-form-field>
        <mat-label>Motivo</mat-label>
        <mat-select formControlName="reasonCode" required>
          @for (reason of reasons(); track reason.reasonCode) {
            <mat-option [value]="reason.reasonCode">{{ reason.reasonDescription }}</mat-option>
          }
        </mat-select>
        <mat-error>Selecciona el motivo</mat-error>
      </mat-form-field>
    </form>

    <section class="preview" aria-live="polite" aria-label="Vista previa del cálculo">
      <div>
        <span>Bruto</span>
        <strong>{{ preview().gross | number: '1.2-2' }}</strong>
      </div>
      <div>
        <span>Retención</span>
        <strong>− {{ preview().tax | number: '1.2-2' }}</strong>
      </div>
      <div class="net">
        <span>Neto a pagar</span>
        <strong>{{ preview().net | number: '1.2-2' }}</strong>
      </div>
    </section>
  } @else {
    <p class="hint">Busca al empleado por área y número para registrar el bono.</p>
  }
</mat-dialog-content>

<mat-dialog-actions align="end">
  <button matButton type="button" mat-dialog-close>Cancelar</button>
  <button
    matButton="filled"
    type="submit"
    form="bonus-form"
    [disabled]="!employee() || bonusForm.invalid || saving()"
  >
    {{ saving() ? 'Guardando…' : 'Guardar' }}
  </button>
</mat-dialog-actions>
```

- [ ] **Step 3: Estilos del modal**

`frontend/src/app/features/bonuses/register-bonus-dialog/register-bonus-dialog.css`
```css
.search {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 12px;
  align-items: start;
  padding-top: 8px;
}

.search-button {
  margin-top: 10px;
}

.progress-slot {
  height: 4px;
  margin: 8px 0;
}

.employee-card {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 12px;
  margin: 0 0 16px;
  padding: 12px 16px;
  border-radius: 8px;
  background: var(--info-bg);
}

.employee-card dt {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.employee-card dd {
  margin: 2px 0 0;
  font-weight: 500;
}

.bonus-form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px 16px;
}

.types {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.preview {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-top: 16px;
  padding: 12px 16px;
  border: 1px solid var(--border);
  border-radius: 8px;
}

.preview span {
  display: block;
  font-size: 0.75rem;
  color: var(--text-muted);
}

.preview strong {
  font-size: 1.1rem;
  font-variant-numeric: tabular-nums;
}

.preview .net strong {
  color: var(--primary);
}

.hint {
  color: var(--text-muted);
}

@media (max-width: 600px) {
  .search,
  .bonus-form,
  .preview {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 4: Conectar el modal a la pantalla**

En `frontend/src/app/features/bonuses/bonuses.ts`:
- agregar el import `import { RegisterBonusDialog } from './register-bonus-dialog/register-bonus-dialog';`
- agregar este método después de `clear()`:
```ts
  protected openRegister(): void {
    this.dialog
      .open<RegisterBonusDialog, void, boolean>(RegisterBonusDialog, { width: '720px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this.notification.show('success', 'Bono registrado correctamente.');
        this.reload(true);
      });
  }
```

En `frontend/src/app/features/bonuses/bonuses.html`, reemplazar
`    <button matButton="filled" type="button" disabled>` por
`    <button matButton="filled" type="button" (click)="openRegister()">`.

- [ ] **Step 5: Compilar**

Run: `cd frontend && npx ng build --configuration development`
Expected: sin errores.

- [ ] **Step 6: Verificar en el navegador**

- "Nuevo registro" → Buscar sin datos → aparecen "Selecciona el área" e "Ingresa un número de empleado válido".
- Buscar VENTA-202 → aviso 409 "El empleado está cesado…" visible por encima del modal.
- Buscar TECH1-102 → tarjeta con Carlos Ruiz Medina, 15/07/2023, 3,000.00, 8 %.
- Tipo Porcentaje, valor 10 → vista previa 300.00 / − 24.00 / 276.00; cambiar a Monto fijo → la etiqueta cambia a "Monto fijo" y el hint a "Máximo 3,000.00"; valor 3500 → "No puede superar 3000".
- Periodo del mes siguiente → "No puede ser posterior a …".
- Guardar con periodo 2026-08, monto fijo 300, motivo "Cumplimiento de metas" → el modal se cierra, aviso "Bono registrado correctamente." y el bono aparece primero en la página 1.
- Repetir el mismo periodo → aviso 409 "El empleado ya tiene un bono activo en ese periodo." y el modal sigue abierto.
- Captura del modal en escritorio y en 390×844.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/features/bonuses
git commit -m "feat(front): modal de registro con búsqueda de empleado y vista previa

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Extra 1 — pagar bono y repartir por proyecto (backend)

**Files:**
- Create: `database/03_bonus_payment_detail.sql`
- Create: `backend/src/BonusTest.Domain/Services/BonusDistributor.cs`
- Create: `backend/src/BonusTest.Domain/Entities/BonusPaymentDetail.cs`
- Modify: `backend/src/BonusTest.Domain/Entities/Bonus.cs` (método `Pay`)
- Modify: `backend/src/BonusTest.Application/Bonuses/IBonusRepository.cs`, `BonusService.cs`
- Modify: `backend/src/BonusTest.Infrastructure/Persistence/AppDbContext.cs`, `Persistence/Configurations/BonusConfigurations.cs`, `Repositories/BonusRepository.cs`
- Modify: `backend/src/BonusTest.Api/Controllers/BonusController.cs`
- Modify: `backend/tests/BonusTest.UnitTests/Fakes/FakeBonusRepository.cs`
- Test: `backend/tests/BonusTest.UnitTests/Domain/BonusDistributorTests.cs`, `Domain/BonusTests.cs`, `Application/BonusServiceTests.cs`

**Interfaces:**
- Consumes: `Money.Round`, `BonusErrors.EmployeeWithoutProjects()`, `Bonus` (Tasks 2-3).
- Produces:
  - `record ProjectHours(string ProjectName, decimal Hours)`, `record ProjectAllocation(string ProjectName, decimal Hours, decimal Amount)`, `BonusDistributor.Distribute(decimal grossAmount, IReadOnlyList<ProjectHours> projects) : IReadOnlyList<ProjectAllocation>` (namespace `BonusTest.Domain.Services`).
  - `BonusPaymentDetail.Create(int bonusId, ProjectAllocation allocation, string userCode, DateTime now)`.
  - `Bonus.Pay(IReadOnlyList<ProjectHours> projects, string userCode, DateTime now) : IReadOnlyList<BonusPaymentDetail>`.
  - `IBonusRepository.GetProjectHoursAsync(string departmentCode, int employeeNumber, CancellationToken) : Task<IReadOnlyList<ProjectHours>>`, `SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken) : Task`.
  - `BonusService.PayAsync(int bonusId, string? userCode, CancellationToken) : Task`.
  - `POST /api/v1/Bonus/{bonusId}/pay` → 204.

- [ ] **Step 1: Script de la tabla de detalle y ejecutarlo**

`database/03_bonus_payment_detail.sql`
```sql
/* =====================================================================
   Prueba técnica - Bonos de desempeño
   Script 3: detalle del pago de un bono distribuido por proyecto (punto extra 1).
   Idempotente.
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE BonusTest;
GO

IF OBJECT_ID('dbo.BonusPaymentDetail', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BonusPaymentDetail (
        BonusPaymentDetailId INT IDENTITY(1,1) CONSTRAINT PK_BonusPaymentDetail PRIMARY KEY,
        BonusId      INT            NOT NULL CONSTRAINT FK_BonusPaymentDetail_Bonus REFERENCES dbo.Bonus (BonusId),
        ProjectName  VARCHAR(100)   NOT NULL,
        Hours        DECIMAL(8,2)   NOT NULL CONSTRAINT CK_BonusPaymentDetail_Hours CHECK (Hours > 0),
        Amount       DECIMAL(16,2)  NOT NULL,
        CreatedBy    VARCHAR(50)    NOT NULL,
        CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_BonusPaymentDetail_CreatedAt DEFAULT (SYSDATETIME())
    );
END
GO

-- Consulta del detalle por bono.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BonusPaymentDetail_BonusId' AND object_id = OBJECT_ID('dbo.BonusPaymentDetail'))
    CREATE INDEX IX_BonusPaymentDetail_BonusId ON dbo.BonusPaymentDetail (BonusId);
GO
```

```bash
export MSYS_NO_PATHCONV=1
docker cp database/03_bonus_payment_detail.sql bonustest-sql:/tmp/03.sql
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -I -b -i /tmp/03.sql
```
Expected: sin errores.

- [ ] **Step 2: Pruebas que fallan del reparto y del pago**

`backend/tests/BonusTest.UnitTests/Domain/BonusDistributorTests.cs`
```csharp
using BonusTest.Domain.Exceptions;
using BonusTest.Domain.Services;

namespace BonusTest.UnitTests.Domain;

public class BonusDistributorTests
{
    [Fact]
    public void Distributes_proportionally_to_hours()
    {
        // TECH1-102: 45 / 35 / 20 horas, bruto 300.00
        var allocations = BonusDistributor.Distribute(300m,
            [new("Portal de clientes", 45m), new("App móvil", 35m), new("Soporte", 20m)]);

        Assert.Equal(new[] { 135m, 105m, 60m }, allocations.Select(x => x.Amount));
        Assert.Equal(new[] { "Portal de clientes", "App móvil", "Soporte" }, allocations.Select(x => x.ProjectName));
    }

    [Fact]
    public void Last_project_absorbs_rounding_difference()
    {
        var allocations = BonusDistributor.Distribute(100m,
            [new("A", 10m), new("B", 10m), new("C", 10m)]);

        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, allocations.Select(x => x.Amount));
        Assert.Equal(100m, allocations.Sum(x => x.Amount));
    }

    [Fact]
    public void Each_line_rounds_half_up()
    {
        // 0.25 × 1/2 = 0.125 → 0.13; el último recibe 0.12
        var allocations = BonusDistributor.Distribute(0.25m, [new("A", 1m), new("B", 1m)]);

        Assert.Equal(new[] { 0.13m, 0.12m }, allocations.Select(x => x.Amount));
    }

    [Fact]
    public void Single_project_receives_everything()
    {
        var allocation = Assert.Single(BonusDistributor.Distribute(250.55m, [new("Campaña de verano", 160m)]));

        Assert.Equal(250.55m, allocation.Amount);
        Assert.Equal(160m, allocation.Hours);
    }

    [Fact]
    public void Fails_when_employee_has_no_projects()
    {
        var error = Assert.Throws<BusinessException>(() => BonusDistributor.Distribute(100m, []));

        Assert.Equal("EMPLOYEE_WITHOUT_PROJECTS", error.Code);
    }
}
```

Agregar al final de la clase en `backend/tests/BonusTest.UnitTests/Domain/BonusTests.cs` (agregar también `using BonusTest.Domain.Services;` arriba):
```csharp
    [Fact]
    public void Pay_marks_bonus_as_paid_and_returns_one_detail_per_project()
    {
        var bonus = NewBonus(); // bruto 500.00

        var details = bonus.Pay([new ProjectHours("Portal de clientes", 120m), new ProjectHours("App móvil", 40m)], "rrhh", Later);

        Assert.Equal(BonusStates.Paid, bonus.BonusStateCode);
        Assert.Equal("rrhh", bonus.UpdatedBy);
        Assert.Equal(new[] { 375m, 125m }, details.Select(x => x.Amount));
        Assert.All(details, x => Assert.Equal("rrhh", x.CreatedBy));
    }

    [Fact]
    public void Pay_fails_when_bonus_is_cancelled()
    {
        var bonus = NewBonus();
        bonus.Cancel("rrhh", Later);

        var error = Assert.Throws<BusinessException>(() => bonus.Pay([new ProjectHours("A", 1m)], "rrhh", Later));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
    }
```

Run: `cd backend && dotnet test`
Expected: FAIL de compilación — `BonusDistributor` / `ProjectHours` / `Pay` no existen.

- [ ] **Step 3: Implementar Domain**

`backend/src/BonusTest.Domain/Services/BonusDistributor.cs`
```csharp
using BonusTest.Domain.Exceptions;

namespace BonusTest.Domain.Services;

public record ProjectHours(string ProjectName, decimal Hours);

public record ProjectAllocation(string ProjectName, decimal Hours, decimal Amount);

/// <summary>
/// Reparte el bruto entre los proyectos en proporción a las horas. Cada línea se redondea
/// a 2 decimales y el último proyecto recibe lo que falta para cuadrar exacto.
/// </summary>
public static class BonusDistributor
{
    public static IReadOnlyList<ProjectAllocation> Distribute(decimal grossAmount, IReadOnlyList<ProjectHours> projects)
    {
        var totalHours = projects.Sum(x => x.Hours);
        if (projects.Count == 0 || totalHours <= 0)
            throw BonusErrors.EmployeeWithoutProjects();

        var allocations = new List<ProjectAllocation>(projects.Count);
        var assigned = 0m;
        foreach (var project in projects.Take(projects.Count - 1))
        {
            var amount = Money.Round(grossAmount * project.Hours / totalHours);
            allocations.Add(new ProjectAllocation(project.ProjectName, project.Hours, amount));
            assigned += amount;
        }

        var last = projects[^1];
        allocations.Add(new ProjectAllocation(last.ProjectName, last.Hours, grossAmount - assigned));
        return allocations;
    }
}
```

`backend/src/BonusTest.Domain/Entities/BonusPaymentDetail.cs`
```csharp
using BonusTest.Domain.Services;

namespace BonusTest.Domain.Entities;

/// <summary>Parte del bono pagada a un proyecto.</summary>
public class BonusPaymentDetail
{
    public int BonusPaymentDetailId { get; private set; }
    public int BonusId { get; private set; }
    public string ProjectName { get; private set; } = null!;
    public decimal Hours { get; private set; }
    public decimal Amount { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private BonusPaymentDetail() { }

    public static BonusPaymentDetail Create(int bonusId, ProjectAllocation allocation, string userCode, DateTime now) => new()
    {
        BonusId = bonusId,
        ProjectName = allocation.ProjectName,
        Hours = allocation.Hours,
        Amount = allocation.Amount,
        CreatedBy = userCode,
        CreatedAt = now
    };
}
```

En `backend/src/BonusTest.Domain/Entities/Bonus.cs`, agregar después de `Delete`:
```csharp
    /// <summary>Paga el bono: reparte el bruto por proyecto y pasa a Pagado.</summary>
    public IReadOnlyList<BonusPaymentDetail> Pay(IReadOnlyList<ProjectHours> projects, string userCode, DateTime now)
    {
        EnsureRegistered();
        var details = BonusDistributor.Distribute(GrossAmount, projects)
            .Select(allocation => BonusPaymentDetail.Create(BonusId, allocation, userCode, now))
            .ToList();

        BonusStateCode = BonusStates.Paid;
        RegisterUpdate(userCode, now);
        return details;
    }
```

Run: `cd backend && dotnet test`
Expected: compila y pasan las pruebas de Domain (`Superado: 53`).

- [ ] **Step 4: Pruebas del servicio que fallan**

En `backend/tests/BonusTest.UnitTests/Fakes/FakeBonusRepository.cs` agregar `using BonusTest.Domain.Services;`, y dentro de la clase:
```csharp
    public List<ProjectHours> Projects { get; } = [];
    public List<BonusPaymentDetail> PaymentDetails { get; } = [];

    public Task<IReadOnlyList<ProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectHours>>(Projects);

    public Task SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken cancellationToken)
    {
        PaymentDetails.AddRange(details);
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
```

Agregar al final de `backend/tests/BonusTest.UnitTests/Application/BonusServiceTests.cs` (agregar `using BonusTest.Domain.Services;` arriba):
```csharp
    [Fact]
    public async Task Pay_distributes_gross_by_project_and_marks_bonus_as_paid()
    {
        _repository.Projects.AddRange([new ProjectHours("Portal de clientes", 120m), new ProjectHours("App móvil", 40m)]);
        await _service.RegisterAsync(Request(), User, CancellationToken.None); // bruto 500.00

        await _service.PayAsync(0, User, CancellationToken.None);

        Assert.Equal(BonusStates.Paid, Assert.Single(_repository.Bonuses).BonusStateCode);
        Assert.Equal(new[] { 375m, 125m }, _repository.PaymentDetails.Select(x => x.Amount));
    }

    [Fact]
    public async Task Pay_fails_for_cancelled_bonus()
    {
        _repository.Projects.Add(new ProjectHours("Portal de clientes", 120m));
        await _service.RegisterAsync(Request(), User, CancellationToken.None);
        await _service.CancelAsync(0, User, CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessException>(() => _service.PayAsync(0, User, CancellationToken.None));

        Assert.Equal("BONUS_NOT_REGISTERED", error.Code);
        Assert.Empty(_repository.PaymentDetails);
    }
```

Run: `cd backend && dotnet test`
Expected: FAIL de compilación — `IBonusRepository` no define los métodos usados / `PayAsync` no existe.

- [ ] **Step 5: Implementar Application**

En `backend/src/BonusTest.Application/Bonuses/IBonusRepository.cs` agregar `using BonusTest.Domain.Services;` y, antes del cierre de la interfaz:
```csharp
    Task<IReadOnlyList<ProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber, CancellationToken cancellationToken);
    /// <summary>Guarda el detalle y el cambio de estado del bono en una sola transacción.</summary>
    Task SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken cancellationToken);
```

En `backend/src/BonusTest.Application/Bonuses/BonusService.cs` agregar después de `DeleteAsync`:
```csharp
    public async Task PayAsync(int bonusId, string? userCode, CancellationToken cancellationToken)
    {
        var user = RequireUser(userCode);
        var bonus = await FindBonusAsync(bonusId, cancellationToken);
        var projects = await repository.GetProjectHoursAsync(bonus.DepartmentCode, bonus.EmployeeNumber, cancellationToken);

        var details = bonus.Pay(projects, user, Now);
        await repository.SavePaymentAsync(details, cancellationToken);
    }
```

Run: `cd backend && dotnet test`
Expected: FAIL de compilación en Infrastructure — `BonusRepository does not implement interface member GetProjectHoursAsync`.

- [ ] **Step 6: Implementar Infrastructure y Api**

En `backend/src/BonusTest.Infrastructure/Persistence/AppDbContext.cs`, debajo de `Bonuses`:
```csharp
    public DbSet<BonusPaymentDetail> BonusPaymentDetails => Set<BonusPaymentDetail>();
```

Al final de `backend/src/BonusTest.Infrastructure/Persistence/Configurations/BonusConfigurations.cs`:
```csharp
public class BonusPaymentDetailConfiguration : IEntityTypeConfiguration<BonusPaymentDetail>
{
    public void Configure(EntityTypeBuilder<BonusPaymentDetail> builder)
    {
        builder.ToTable("BonusPaymentDetail").HasKey(x => x.BonusPaymentDetailId);

        builder.Property(x => x.ProjectName).HasColumnType("varchar(100)");
        builder.Property(x => x.CreatedBy).HasColumnType("varchar(50)");
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
        builder.Property(x => x.Hours).HasPrecision(8, 2);
        builder.Property(x => x.Amount).HasPrecision(16, 2);

        builder.HasOne<Bonus>().WithMany().HasForeignKey(x => x.BonusId);
    }
}
```

En `backend/src/BonusTest.Infrastructure/Repositories/BonusRepository.cs` agregar `using BonusTest.Domain.Services;` y, después de `SaveChangesAsync`:
```csharp
    public async Task<IReadOnlyList<ProjectHours>> GetProjectHoursAsync(string departmentCode, int employeeNumber,
        CancellationToken cancellationToken) =>
        await context.EmployeeProjectHours.AsNoTracking()
            .Where(x => x.DepartmentCode == departmentCode && x.EmployeeNumber == employeeNumber)
            .OrderBy(x => x.EmployeeProjectHoursId)
            .Select(x => new ProjectHours(x.ProjectName, x.Hours))
            .ToListAsync(cancellationToken);

    public async Task SavePaymentAsync(IReadOnlyList<BonusPaymentDetail> details, CancellationToken cancellationToken)
    {
        // Transacción explícita: el detalle y el paso a Pagado se confirman juntos o no se guarda nada.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.BonusPaymentDetails.AddRange(details);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
```

En `backend/src/BonusTest.Api/Controllers/BonusController.cs`, después de `Delete`:
```csharp
    [HttpPost("{bonusId:int}/pay")]
    public async Task<IActionResult> Pay(int bonusId,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken)
    {
        await bonusService.PayAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }
```

- [ ] **Step 7: Pruebas y compilación**

Detener la API. Run: `cd backend && dotnet build BonusTest.slnx && dotnet test`
Expected: `0 Errores` y `Con error: 0, Superado: 55`.

- [ ] **Step 8: Humo del pago contra la BD**

Levantar la API (como en Task 6) y:
```bash
API=http://localhost:5106/api/v1/Bonus
curl -s -X POST $API -H Content-Type:application/json -H UserCode:candidato \
  -d '{"departmentCode":"TECH1","employeeNumber":102,"period":"2026-09","bonusTypeId":1,"bonusValue":300,"reasonCode":"000001"}'; echo
ID=$(curl -s "$API?employeeNumber=102&period=2026-09" | grep -o '"bonusId":[0-9]*' | head -1 | cut -d: -f2)
curl -s -w '%{http_code}' -X POST $API/$ID/pay -H UserCode:candidato; echo           # Expected: 204
curl -s -w ' %{http_code}' -X POST $API/$ID/pay -H UserCode:candidato; echo          # Expected: BONUS_NOT_REGISTERED 409
docker exec bonustest-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BonusTest#2026' -C -d BonusTest \
  -Q "SELECT ProjectName, Hours, Amount FROM dbo.BonusPaymentDetail WHERE BonusId = $ID; SELECT BonusStateCode FROM dbo.Bonus WHERE BonusId = $ID"
```
Expected: `Portal de clientes 45.00 135.00`, `App móvil 35.00 105.00`, `Soporte 20.00 60.00` y estado `0202`.

- [ ] **Step 9: Commit**

```bash
git add database/03_bonus_payment_detail.sql backend
git commit -m "feat: pagar bono y distribuirlo por proyecto en una transacción

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Extra 1 — botón Pagar en la grilla

**Files:**
- Modify: `frontend/src/app/core/services/bonus-api.ts`
- Modify: `frontend/src/app/features/bonuses/bonuses.ts`, `bonuses.html`, `bonuses.css`

**Interfaces:**
- Consumes: `POST /api/v1/Bonus/{bonusId}/pay` (Task 10); `runConfirmedAction` (Task 8).
- Produces: `BonusApi.pay(bonusId: number): Observable<void>`.

- [ ] **Step 1: Servicio**

En `frontend/src/app/core/services/bonus-api.ts`, agregar después de `delete`:
```ts
  pay(bonusId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${bonusId}/pay`, null);
  }
```

- [ ] **Step 2: Acción en el componente**

En `frontend/src/app/features/bonuses/bonuses.ts`:
- agregar después de `remove(...)`:
```ts
  protected pay(bonus: BonusListItem): void {
    const gross = bonus.grossAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    this.runConfirmedAction(
      {
        title: 'Pagar bono',
        message: `¿Pagar el bono de ${bonus.fullName} (${bonus.period})? El bruto de ${gross} se repartirá entre sus proyectos según las horas.`,
        confirmText: 'Pagar',
      },
      () => this.bonusApi.pay(bonus.bonusId),
      'Bono pagado y distribuido por proyecto.',
    );
  }
```

- [ ] **Step 3: Botón en la plantilla**

En `frontend/src/app/features/bonuses/bonuses.html`, dentro de la celda `actions`, antes del botón Anular:
```html
          <button
            matIconButton
            type="button"
            matTooltip="Pagar"
            aria-label="Pagar bono"
            [disabled]="!isRegistered(bonus)"
            (click)="pay(bonus)"
          >
            <mat-icon>payments</mat-icon>
          </button>
```
Cambiar el subtítulo a `Registra, paga, anula y elimina bonos por periodo.`

En `bonuses.css`, cambiar `.mat-column-actions { width: 104px; ... }` a `width: 148px;`.

- [ ] **Step 4: Compilar y verificar en el navegador**

Run: `cd frontend && npx ng build --configuration development` → sin errores.
En el navegador: Pagar un bono Registrado → confirmación con el bruto → aviso "Bono pagado y distribuido por proyecto." → chip verde "Pagado" y acciones deshabilitadas. Filtrar Estado = Pagado lo muestra.

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat(front): acción para pagar bonos desde la grilla

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: README, revisión y verificación final

**Files:**
- Create: `README.md`

- [ ] **Step 1: Escribir el README**

`README.md`
````markdown
# Bonos de desempeño — Prueba técnica Full Stack

Módulo de Recursos Humanos para registrar bonos de desempeño: se busca un empleado activo, se le registra un bono (monto fijo o porcentaje del sueldo) para un periodo, y el sistema calcula bruto, retención y neto. Desde el listado se puede pagar (con reparto por proyecto), anular y eliminar.

| Capa | Tecnología |
|---|---|
| Base de datos | SQL Server 2019+ |
| Backend | .NET 10, ASP.NET Core Web API, EF Core 10 |
| Frontend | Angular 22 (standalone) + Angular Material 22 |
| Pruebas | xUnit |

## Cómo levantarlo

### 1. Base de datos

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

### 2. API

`appsettings.Development.json` apunta a `Server=localhost` con autenticación de Windows. Para otra instancia (por ejemplo, Docker), sobrescribe la cadena con una variable de entorno:

```bash
export ConnectionStrings__BonusTest="Server=localhost,1433;Database=BonusTest;User Id=sa;Password=BonusTest#2026;TrustServerCertificate=True;"
cd backend/src/BonusTest.Api
dotnet run --launch-profile http
```
Swagger: http://localhost:5106/swagger

Pruebas: `cd backend && dotnet test`

### 3. Front

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
| GET | `/api/v1/Bonus/employee/{departmentCode}/{employeeNumber}` | 200 datos del empleado activo |
| POST | `/api/v1/Bonus` | 201 `{ bonusId, grossAmount, taxAmount, netAmount }` |
| PUT | `/api/v1/Bonus/{bonusId}/cancel` | 204 |
| DELETE | `/api/v1/Bonus/{bonusId}` | 204 (eliminado lógico) |
| POST | `/api/v1/Bonus/{bonusId}/pay` | 204 (extra 1) |

El usuario que opera llega en el header `UserCode`. Los errores de negocio responden 409 con `{ code, description }`.

## Decisiones técnicas

- **Reglas en Domain y Application, no en el controlador.** `BonusCalculator` y `BonusDistributor` son funciones puras; la entidad `Bonus` solo cambia de estado con `Cancel`, `Delete` y `Pay`, que exigen el estado Registrado. `BonusService` aplica las validaciones que requieren datos (empleado activo, motivo activo, duplicados, periodo, límites del valor).
- **Dinero con `decimal` y redondeo `MidpointRounding.AwayFromZero`** (0.125 → 0.13). El redondeo bancario por defecto de .NET daría 0.12.
- **Montos guardados al registrar** (`GrossAmount`, `TaxAmount`, `NetAmount`) junto con `TaxRateApplied`: el bono conserva lo calculado aunque luego cambien el sueldo o la tasa del empleado, y el listado no recalcula.
- **Un bono activo por periodo, validado dos veces:** en el servicio (mensaje claro) y con el índice único filtrado `UX_Bonus_ActivePerPeriod` (`WHERE IsDeleted = 0 AND BonusStateCode <> '0203'`), que cubre registros concurrentes; esa violación también se traduce a 409.
- **Paginación en la base de datos:** EF Core traduce `Skip/Take` a `OFFSET/FETCH`; primero se filtra y cuenta, luego se une la página con los catálogos para devolver las descripciones.
- **Tipos `char`/`varchar` mapeados en EF** para que los parámetros no se envíen como `nvarchar` y se usen los índices.
- **Fecha actual vía `TimeProvider`** para probar la regla "periodo no posterior al mes actual" sin depender del reloj.
- **Pago en una transacción explícita:** detalle por proyecto + paso a Pagado. Cada línea se redondea y el último proyecto recibe la diferencia para cuadrar exacto.
- **Front:** Angular Material (tabla, paginador, diálogos, formularios reactivos), estado con signals, `switchMap` para descartar respuestas de páginas antiguas. La vista previa replica la fórmula, pero el monto oficial lo calcula la API. El aviso global se muestra por encima del modal para que los 409 se vean.

## Pruebas

`backend/tests/BonusTest.UnitTests` (xUnit): cálculo y redondeo, transiciones de estado, reparto por proyecto, y validaciones de `BonusService` con un repositorio en memoria.

## Pendientes / fuera de alcance

- Autenticación real (el usuario llega en el header `UserCode`).
- Edición de bonos, reversión de pagos y consulta del detalle de pago en la pantalla.
- Pruebas de integración de la API contra SQL Server y pruebas del front.
````

- [ ] **Step 2: Verificación completa (usar superpowers:verification-before-completion)**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd backend && dotnet build BonusTest.slnx && dotnet test
cd ../frontend && npx ng build
```
Expected: `0 Advertencia(s) 0 Errores`, todas las pruebas en verde, build de producción sin errores ni advertencias de presupuesto.

Levantar API + front y repetir en el navegador el recorrido de Tasks 8, 9 y 11; capturas de escritorio y celular.

- [ ] **Step 3: Revisión de código (usar superpowers:requesting-code-review)**

Revisar el diff desde `abdb37a` contra la spec; atender hallazgos con superpowers:receiving-code-review.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: README con instalación, decisiones técnicas y pendientes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
