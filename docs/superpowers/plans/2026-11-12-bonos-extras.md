# Extras de bonos (resumen, CSV, detalle de pago, Docker Compose) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar al módulo de bonos un resumen por estado, exportación CSV, detalle del pago por proyecto y un Docker Compose que levante todo con un comando.

**Architecture:** Lógica pura en Application (`BonusSummaryCalculator`, `BonusCsvWriter`) con pruebas xUnit; `BonusService` orquesta; `BonusRepository` agrega consultas reutilizando el filtrado y la proyección existentes; el controlador sigue siendo un pase. En el front, un componente de tarjetas, un botón de exportación y un diálogo de detalle. Docker Compose con SQL Server, un contenedor de inicialización que corre los scripts, la API y el front en nginx.

**Tech Stack:** .NET 10 / EF Core 10 / xUnit, Angular 22.2 zoneless + Material 22.2.7, SQL Server 2022, Docker Compose v2, nginx.

**Spec:** `docs/superpowers/specs/2026-11-12-bonos-extras-design.md`

## Global Constraints

- No cambiar el stack ni la estructura de capas; ninguna regla de negocio en el controlador.
- Sin dependencias nuevas de NuGet ni npm.
- No modificar `appsettings*.json`, `environment.ts`, `Program.cs` ni `database/01_schema_seed.sql`.
- Errores de negocio con `BonusErrors` → `BusinessException` (409), mensajes en español.
- Dinero en `decimal`; CSV con decimales invariantes `0.00`.
- El front no maneja errores HTTP en componentes (el interceptor los muestra); `error: noop` / `catchError(() => EMPTY)` solo evita errores no controlados.
- Commits convencionales en español con trailer `Co-Authored-By:` del modelo que realmente trabajó.

## Entorno local

- Rutas relativas a la raíz del repositorio; rama `feat/bonos-desempeno`.
- `dotnet` fuera del PATH: `export PATH="/c/Program Files/dotnet:$PATH"`. Tras compilar: `dotnet build-server shutdown` (máquina con poca memoria).
- La API corre en segundo plano en :5106 y bloquea los DLL: detenerla con `powershell -c "Stop-Process -Name BonusTest.Api -Force -ErrorAction SilentlyContinue"` antes de compilar y relanzarla después (en segundo plano):
  `export PATH="/c/Program Files/dotnet:$PATH"; export ConnectionStrings__BonusTest='Server=localhost,1433;Database=BonusTest;User Id=sa;Password=BonusTest#2026;TrustServerCertificate=True;'; cd backend/src/BonusTest.Api && dotnet run --no-build --launch-profile http`
- Front: `ng serve` ya corre en :4200 (no iniciar otro). Verificar con `cd frontend && npx ng build`.

## Mapa de archivos

```
backend/src/BonusTest.Domain/Exceptions/BonusErrors.cs               (T1) +BonusNotPaid, +ExportTooLarge
backend/src/BonusTest.Application/Bonuses/BonusSummary.cs            (T1) BonusStateTotals, BonusSummaryDto, BonusSummaryCalculator
backend/src/BonusTest.Application/Bonuses/BonusCsvWriter.cs          (T1)
backend/src/BonusTest.Application/Bonuses/BonusDtos.cs               (T2) +PaymentLineDto, +BonusPaymentDetailDto, +BonusExportFile
backend/src/BonusTest.Application/Bonuses/IBonusRepository.cs        (T2) +3 métodos
backend/src/BonusTest.Application/Bonuses/BonusService.cs            (T2) +GetSummaryAsync, +ExportAsync, +GetPaymentDetailAsync
backend/src/BonusTest.Infrastructure/Repositories/BonusRepository.cs (T3)
backend/src/BonusTest.Api/Controllers/BonusController.cs             (T3) +3 endpoints
backend/tests/BonusTest.UnitTests/Application/BonusSummaryCalculatorTests.cs (T1)
backend/tests/BonusTest.UnitTests/Application/BonusCsvWriterTests.cs (T1)
backend/tests/BonusTest.UnitTests/Application/BonusServiceTests.cs   (T2)
backend/tests/BonusTest.UnitTests/Fakes/FakeBonusRepository.cs       (T2)
frontend/src/app/core/models/bonus.ts                                (T4)
frontend/src/app/core/services/bonus-api.ts                          (T4)
frontend/src/app/core/utils/download.ts                              (T4)
frontend/src/app/features/bonuses/bonus-summary-cards/bonus-summary-cards.{ts,html,css} (T4)
frontend/src/app/features/bonuses/bonuses.{ts,html,css}              (T4, T5)
frontend/src/app/features/bonuses/payment-detail-dialog/payment-detail-dialog.ts (T5)
backend/Dockerfile, backend/.dockerignore                             (T6)
frontend/Dockerfile, frontend/.dockerignore, frontend/nginx.conf      (T6)
docker/db-init.sh, docker-compose.yml, .env.example, .gitattributes   (T6)
.gitignore                                                            (T6) +.env
README.md                                                             (T7)
```

---

### Task 1: Lógica pura de resumen y CSV (Application)

**Files:**
- Modify: `backend/src/BonusTest.Domain/Exceptions/BonusErrors.cs`
- Create: `backend/src/BonusTest.Application/Bonuses/BonusSummary.cs`
- Create: `backend/src/BonusTest.Application/Bonuses/BonusCsvWriter.cs`
- Test: `backend/tests/BonusTest.UnitTests/Application/BonusSummaryCalculatorTests.cs`, `BonusCsvWriterTests.cs`

**Interfaces:**
- Consumes: `BonusListItemDto` (15 campos posicionales: BonusId, BonusStateCode, StateDescription, Period, DepartmentCode, DepartmentName, EmployeeNumber, FullName, BonusTypeId, BonusTypeDescription, BonusValue, ReasonDescription, GrossAmount, TaxAmount, NetAmount); `BonusStates`.
- Produces: `BonusErrors.BonusNotPaid()`, `BonusErrors.ExportTooLarge()`; `record BonusStateTotals(string BonusStateCode, int Count, decimal GrossAmount, decimal TaxAmount, decimal NetAmount)`; `record BonusSummaryDto(int PendingCount, decimal PendingNet, int PaidCount, decimal PaidNet, int CancelledCount, decimal CommittedGross, decimal CommittedTax, decimal CommittedNet)`; `BonusSummaryCalculator.Build(IReadOnlyList<BonusStateTotals>) : BonusSummaryDto`; `BonusCsvWriter.Write(IReadOnlyList<BonusListItemDto>) : string`.

- [ ] **Step 1: Pruebas que fallan**

`backend/tests/BonusTest.UnitTests/Application/BonusSummaryCalculatorTests.cs`
```csharp
using BonusTest.Application.Bonuses;
using BonusTest.Domain.Constants;

namespace BonusTest.UnitTests.Application;

public class BonusSummaryCalculatorTests
{
    [Fact]
    public void Separates_pending_paid_and_cancelled_and_commits_only_active_money()
    {
        var summary = BonusSummaryCalculator.Build(
        [
            new BonusStateTotals(BonusStates.Registered, 3, 1500m, 120m, 1380m),
            new BonusStateTotals(BonusStates.Paid, 2, 600m, 54m, 546m),
            new BonusStateTotals(BonusStates.Cancelled, 4, 900m, 72m, 828m),
        ]);

        Assert.Equal(new BonusSummaryDto(3, 1380m, 2, 546m, 4, 2100m, 174m, 1926m), summary);
    }

    [Fact]
    public void Missing_states_count_as_zero()
    {
        var summary = BonusSummaryCalculator.Build([new BonusStateTotals(BonusStates.Paid, 1, 300m, 24m, 276m)]);

        Assert.Equal(new BonusSummaryDto(0, 0m, 1, 276m, 0, 300m, 24m, 276m), summary);
    }

    [Fact]
    public void No_bonuses_gives_an_empty_summary()
    {
        Assert.Equal(new BonusSummaryDto(0, 0m, 0, 0m, 0, 0m, 0m, 0m), BonusSummaryCalculator.Build([]));
    }
}
```

`backend/tests/BonusTest.UnitTests/Application/BonusCsvWriterTests.cs`
```csharp
using BonusTest.Application.Bonuses;

namespace BonusTest.UnitTests.Application;

public class BonusCsvWriterTests
{
    private const string Header = "Estado,Periodo,Área,N.º empleado,Nombre,Tipo,Monto o %,Motivo,Bruto,Retención,Neto";

    private static BonusListItemDto Row(string fullName = "Andrea Torres Vega", string reason = "Cumplimiento de metas",
        decimal gross = 500m) =>
        new(1, "0201", "Registrado", "2026-10", "TECH1", "Tecnología", 101, fullName, 2, "Porcentaje del sueldo",
            10m, reason, gross, 40m, gross - 40m);

    [Fact]
    public void Writes_header_and_one_line_per_bonus_with_crlf()
    {
        var csv = BonusCsvWriter.Write([Row()]);

        Assert.Equal(
            Header + "\r\n" +
            "Registrado,2026-10,TECH1,101,Andrea Torres Vega,Porcentaje del sueldo,10.00,Cumplimiento de metas,500.00,40.00,460.00\r\n",
            csv);
    }

    [Fact]
    public void Quotes_fields_with_commas_quotes_or_line_breaks()
    {
        var csv = BonusCsvWriter.Write([Row(fullName: "Pérez, \"Juan\"", reason: "Línea 1\nLínea 2")]);

        Assert.Contains(",\"Pérez, \"\"Juan\"\"\",", csv);
        Assert.Contains(",\"Línea 1\nLínea 2\",", csv);
    }

    [Fact]
    public void Neutralizes_spreadsheet_formulas()
    {
        var csv = BonusCsvWriter.Write([Row(reason: "=HYPERLINK(\"x\")")]);

        Assert.Contains(",\"'=HYPERLINK(\"\"x\"\")\",", csv);
    }

    [Fact]
    public void Uses_invariant_decimals_without_thousand_separators()
    {
        var csv = BonusCsvWriter.Write([Row(gross: 12345.5m)]);

        Assert.Contains(",12345.50,40.00,12305.50\r\n", csv);
    }
}
```

- [ ] **Step 2: Ejecutar y ver el fallo**

Run: `cd backend && dotnet test`
Expected: FAIL de compilación — `BonusSummaryCalculator` / `BonusCsvWriter` no existen.

- [ ] **Step 3: Implementar**

En `backend/src/BonusTest.Domain/Exceptions/BonusErrors.cs`, antes del cierre de la clase:
```csharp
    public static BusinessException BonusNotPaid() =>
        new("BONUS_NOT_PAID", "El bono aún no ha sido pagado; no tiene detalle por proyecto.");

    public static BusinessException ExportTooLarge() =>
        new("EXPORT_TOO_LARGE", "Hay más de 10 000 bonos con estos filtros; acota la búsqueda para exportar.");
```

`backend/src/BonusTest.Application/Bonuses/BonusSummary.cs`
```csharp
using BonusTest.Domain.Constants;

namespace BonusTest.Application.Bonuses;

/// <summary>Totales de los bonos visibles agrupados por estado (una fila por estado presente).</summary>
public record BonusStateTotals(string BonusStateCode, int Count, decimal GrossAmount, decimal TaxAmount, decimal NetAmount);

/// <summary>
/// Resumen para RR. HH. y Finanzas: lo pendiente de pagar, lo pagado y lo comprometido
/// (Registrado + Pagado). Los anulados solo se cuentan; no suman dinero.
/// </summary>
public record BonusSummaryDto(
    int PendingCount,
    decimal PendingNet,
    int PaidCount,
    decimal PaidNet,
    int CancelledCount,
    decimal CommittedGross,
    decimal CommittedTax,
    decimal CommittedNet);

public static class BonusSummaryCalculator
{
    public static BonusSummaryDto Build(IReadOnlyList<BonusStateTotals> totals)
    {
        var pending = TotalsFor(totals, BonusStates.Registered);
        var paid = TotalsFor(totals, BonusStates.Paid);
        var cancelled = TotalsFor(totals, BonusStates.Cancelled);

        return new BonusSummaryDto(
            pending.Count, pending.NetAmount,
            paid.Count, paid.NetAmount,
            cancelled.Count,
            pending.GrossAmount + paid.GrossAmount,
            pending.TaxAmount + paid.TaxAmount,
            pending.NetAmount + paid.NetAmount);
    }

    private static BonusStateTotals TotalsFor(IReadOnlyList<BonusStateTotals> totals, string bonusStateCode) =>
        totals.FirstOrDefault(x => x.BonusStateCode == bonusStateCode)
        ?? new BonusStateTotals(bonusStateCode, 0, 0m, 0m, 0m);
}
```

`backend/src/BonusTest.Application/Bonuses/BonusCsvWriter.cs`
```csharp
using System.Globalization;
using System.Text;

namespace BonusTest.Application.Bonuses;

/// <summary>
/// Genera el CSV del listado para planilla: separador coma, decimales invariantes y saltos CRLF.
/// Los campos con coma, comillas o saltos van entre comillas; los que empiezan como fórmula
/// (=, +, -, @) se prefijan con ' para que Excel no los ejecute.
/// </summary>
public static class BonusCsvWriter
{
    private const string Header = "Estado,Periodo,Área,N.º empleado,Nombre,Tipo,Monto o %,Motivo,Bruto,Retención,Neto";
    private const string LineBreak = "\r\n";
    private static readonly char[] CharactersThatNeedQuotes = [',', '"', '\r', '\n'];
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    public static string Write(IReadOnlyList<BonusListItemDto> rows)
    {
        var csv = new StringBuilder(Header).Append(LineBreak);
        foreach (var row in rows)
        {
            string[] fields =
            [
                Text(row.StateDescription), Text(row.Period), Text(row.DepartmentCode),
                row.EmployeeNumber.ToString(CultureInfo.InvariantCulture),
                Text(row.FullName), Text(row.BonusTypeDescription), Amount(row.BonusValue), Text(row.ReasonDescription),
                Amount(row.GrossAmount), Amount(row.TaxAmount), Amount(row.NetAmount)
            ];
            csv.AppendJoin(',', fields).Append(LineBreak);
        }

        return csv.ToString();
    }

    private static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Text(string value)
    {
        var safe = value.Length > 0 && FormulaPrefixes.Contains(value[0]) ? "'" + value : value;
        return safe.IndexOfAny(CharactersThatNeedQuotes) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
    }
}
```

- [ ] **Step 4: Ejecutar y ver que pasa**

Run: `cd backend && dotnet test` → `Con error: 0, Superado: 65` (58 + 7).

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat(application): resumen por estado y escritor CSV de bonos"
```

---

### Task 2: Casos de uso de resumen, exportación y detalle (BonusService)

**Files:**
- Modify: `backend/src/BonusTest.Application/Bonuses/BonusDtos.cs`, `IBonusRepository.cs`, `BonusService.cs`
- Modify: `backend/tests/BonusTest.UnitTests/Fakes/FakeBonusRepository.cs`
- Test: `backend/tests/BonusTest.UnitTests/Application/BonusServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 (`BonusStateTotals`, `BonusSummaryCalculator`, `BonusCsvWriter`, errores nuevos).
- Produces:
  - DTOs: `record PaymentLineDto(string ProjectName, decimal Hours, decimal Amount)`, `record BonusPaymentDetailDto(int BonusId, decimal GrossAmount, IReadOnlyList<PaymentLineDto> Lines)`, `record BonusExportFile(byte[] Content, string FileName)`.
  - `IBonusRepository`: `Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter, CancellationToken)`, `Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter, int maxRows, CancellationToken)`, `Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken)`.
  - `BonusService.MaxExportRows = 10_000`; `GetSummaryAsync(BonusFilter, ct) : Task<BonusSummaryDto>`; `ExportAsync(BonusFilter, ct) : Task<BonusExportFile>`; `GetPaymentDetailAsync(int bonusId, ct) : Task<BonusPaymentDetailDto>`.

- [ ] **Step 1: Contratos (DTOs e interfaz)**

Al final de `backend/src/BonusTest.Application/Bonuses/BonusDtos.cs`:
```csharp
public record PaymentLineDto(string ProjectName, decimal Hours, decimal Amount);

public record BonusPaymentDetailDto(int BonusId, decimal GrossAmount, IReadOnlyList<PaymentLineDto> Lines);

/// <summary>Archivo listo para descargar (bytes con BOM UTF-8 y nombre sugerido).</summary>
public record BonusExportFile(byte[] Content, string FileName);
```

En `IBonusRepository.cs`, antes del cierre de la interfaz:
```csharp
    /// <summary>Cantidad y montos de los bonos visibles (no eliminados) que cumplen el filtro, por estado.</summary>
    Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter filter, CancellationToken cancellationToken);
    /// <summary>Bonos visibles que cumplen el filtro, más recientes primero, hasta <paramref name="maxRows"/>.</summary>
    Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter filter, int maxRows, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken cancellationToken);
```

En `FakeBonusRepository.cs`, dentro de la clase:
```csharp
    public List<BonusStateTotals> StateTotals { get; } = [];
    public List<BonusListItemDto> ExportRows { get; } = [];
    public List<PaymentLineDto> PaymentLines { get; } = [];

    public Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BonusStateTotals>>(StateTotals);

    public Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter filter, int maxRows, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BonusListItemDto>>(ExportRows.Take(maxRows).ToList());

    public Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PaymentLineDto>>(PaymentLines);
```

- [ ] **Step 2: Pruebas que fallan**

Agregar `using System.Text;` arriba y estas pruebas al final de `BonusServiceTests` (la clase ya tiene `_repository`, `_service`, `User`, `Request(...)` y `Today = 2026-11-12`):
```csharp
    private static BonusListItemDto ExportRow(int id) =>
        new(id, "0201", "Registrado", "2026-10", "TECH1", "Tecnología", 101, "Andrea Torres Vega", 2,
            "Porcentaje del sueldo", 10m, "Cumplimiento de metas", 500m, 40m, 460m);

    [Fact]
    public async Task Summary_is_built_from_state_totals()
    {
        _repository.StateTotals.Add(new BonusStateTotals(BonusStates.Registered, 2, 1000m, 80m, 920m));

        var summary = await _service.GetSummaryAsync(new BonusFilter(), CancellationToken.None);

        Assert.Equal(new BonusSummaryDto(2, 920m, 0, 0m, 0, 1000m, 80m, 920m), summary);
    }

    [Fact]
    public async Task Export_returns_utf8_csv_with_bom_and_dated_file_name()
    {
        _repository.ExportRows.AddRange([ExportRow(2), ExportRow(1)]);

        var file = await _service.ExportAsync(new BonusFilter(), CancellationToken.None);

        Assert.Equal("bonos-20261112.csv", file.FileName);
        Assert.Equal(Encoding.UTF8.GetPreamble(), file.Content.Take(3));
        var text = Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3);
        Assert.StartsWith("Estado,Periodo,Área,", text);
        Assert.Equal(3, text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task Export_rejects_more_rows_than_the_limit()
    {
        _repository.ExportRows.AddRange(Enumerable.Range(1, BonusService.MaxExportRows + 1).Select(ExportRow));

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => _service.ExportAsync(new BonusFilter(), CancellationToken.None));

        Assert.Equal("EXPORT_TOO_LARGE", error.Code);
    }

    [Fact]
    public async Task Payment_detail_fails_for_unknown_bonus()
    {
        var error = await Assert.ThrowsAsync<BusinessException>(
            () => _service.GetPaymentDetailAsync(99, CancellationToken.None));

        Assert.Equal("BONUS_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task Payment_detail_fails_for_unpaid_bonus()
    {
        await _service.RegisterAsync(Request(), User, CancellationToken.None);

        var error = await Assert.ThrowsAsync<BusinessException>(
            () => _service.GetPaymentDetailAsync(0, CancellationToken.None));

        Assert.Equal("BONUS_NOT_PAID", error.Code);
    }

    [Fact]
    public async Task Payment_detail_returns_lines_of_a_paid_bonus()
    {
        _repository.Projects.Add(new ProjectHours("Portal de clientes", 160m));
        await _service.RegisterAsync(Request(), User, CancellationToken.None); // bruto 500.00
        await _service.PayAsync(0, User, CancellationToken.None);
        _repository.PaymentLines.Add(new PaymentLineDto("Portal de clientes", 160m, 500m));

        var detail = await _service.GetPaymentDetailAsync(0, CancellationToken.None);

        Assert.Equal(500m, detail.GrossAmount);
        Assert.Equal(new PaymentLineDto("Portal de clientes", 160m, 500m), Assert.Single(detail.Lines));
    }
```

Run: `cd backend && dotnet test` → FAIL de compilación (`GetSummaryAsync`, `ExportAsync`, `GetPaymentDetailAsync`, `MaxExportRows` no existen; `BonusRepository` aún no implementa la interfaz — esperado, se implementa en Task 3; si la compilación de Infrastructure bloquea las pruebas, continuar con el Step 3 y luego Task 3 Step 1 antes de correr las pruebas, y dejar constancia en el reporte).

- [ ] **Step 3: Implementar en BonusService**

Agregar `using System.Text;` y, dentro de la clase (después de `SearchAsync`):
```csharp
    public const int MaxExportRows = 10_000;

    public async Task<BonusSummaryDto> GetSummaryAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        BonusSummaryCalculator.Build(await repository.GetStateTotalsAsync(filter, cancellationToken));

    /// <summary>CSV con todos los bonos del filtro (no solo la página), en UTF-8 con BOM para que Excel muestre acentos.</summary>
    public async Task<BonusExportFile> ExportAsync(BonusFilter filter, CancellationToken cancellationToken)
    {
        var rows = await repository.GetForExportAsync(filter, MaxExportRows + 1, cancellationToken);
        if (rows.Count > MaxExportRows)
            throw BonusErrors.ExportTooLarge();

        byte[] content = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(BonusCsvWriter.Write(rows))];
        return new BonusExportFile(content, $"bonos-{Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.csv");
    }

    public async Task<BonusPaymentDetailDto> GetPaymentDetailAsync(int bonusId, CancellationToken cancellationToken)
    {
        var bonus = await FindBonusAsync(bonusId, cancellationToken);
        if (bonus.BonusStateCode != BonusStates.Paid)
            throw BonusErrors.BonusNotPaid();

        var lines = await repository.GetPaymentLinesAsync(bonus.BonusId, cancellationToken);
        return new BonusPaymentDetailDto(bonus.BonusId, bonus.GrossAmount, lines);
    }
```

- [ ] **Step 4: Commit (junto con Task 3 si la compilación lo exige)**

Si Task 3 aún no implementa los métodos, no se puede compilar la solución: el implementador de esta tarea también hace Task 3 Step 1 (repositorio) para dejar la solución compilando, corre `dotnet test` → `Superado: 71`, y hace dos commits:
```bash
git add backend/src/BonusTest.Application backend/tests
git commit -m "feat(application): casos de uso de resumen, exportación CSV y detalle de pago"
git add backend/src/BonusTest.Infrastructure
git commit -m "feat(infrastructure): consultas de resumen, exportación y detalle de pago"
```

---

### Task 3: Repositorio y endpoints

**Files:**
- Modify: `backend/src/BonusTest.Infrastructure/Repositories/BonusRepository.cs`
- Modify: `backend/src/BonusTest.Api/Controllers/BonusController.cs`

**Interfaces:**
- Consumes: Task 2 (`IBonusRepository` nuevos métodos, `BonusService` nuevos casos de uso).
- Produces: `GET /api/v1/Bonus/summary` → `BonusSummaryDto`; `GET /api/v1/Bonus/export` → `text/csv`; `GET /api/v1/Bonus/{bonusId}/payment-detail` → `BonusPaymentDetailDto`. Mismos query params de filtro que `GET /api/v1/Bonus`.

- [ ] **Step 1: Repositorio (si no se hizo en Task 2)**

En `BonusRepository.cs`: extraer el filtrado común y agregar los tres métodos.
```csharp
    /// <summary>Bonos no eliminados que cumplen el filtro, sin seguimiento (base de listado, resumen y exportación).</summary>
    private IQueryable<Bonus> VisibleBonuses(BonusFilter filter) =>
        ApplyFilter(context.Bonuses.AsNoTracking().Where(x => !x.IsDeleted), filter);
```
- En `GetPagedAsync`, reemplazar `var bonuses = ApplyFilter(context.Bonuses.AsNoTracking().Where(x => !x.IsDeleted), filter);` por `var bonuses = VisibleBonuses(filter);`.
- Agregar (después de `GetPagedAsync`):
```csharp
    public async Task<IReadOnlyList<BonusStateTotals>> GetStateTotalsAsync(BonusFilter filter, CancellationToken cancellationToken) =>
        await VisibleBonuses(filter)
            .GroupBy(x => x.BonusStateCode)
            .Select(g => new BonusStateTotals(g.Key, g.Count(), g.Sum(x => x.GrossAmount), g.Sum(x => x.TaxAmount), g.Sum(x => x.NetAmount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BonusListItemDto>> GetForExportAsync(BonusFilter filter, int maxRows,
        CancellationToken cancellationToken) =>
        await ToListItems(VisibleBonuses(filter).OrderByDescending(x => x.BonusId).Take(maxRows)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentLineDto>> GetPaymentLinesAsync(int bonusId, CancellationToken cancellationToken) =>
        await context.BonusPaymentDetails.AsNoTracking()
            .Where(x => x.BonusId == bonusId)
            .OrderBy(x => x.BonusPaymentDetailId)
            .Select(x => new PaymentLineDto(x.ProjectName, x.Hours, x.Amount))
            .ToListAsync(cancellationToken);
```

- [ ] **Step 2: Endpoints**

En `BonusController.cs`, después de `Search`:
```csharp
    [ProducesResponseType(typeof(BonusSummaryDto), StatusCodes.Status200OK)]
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] BonusFilter filter, CancellationToken cancellationToken) =>
        Ok(await bonusService.GetSummaryAsync(filter, cancellationToken));

    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] BonusFilter filter, CancellationToken cancellationToken)
    {
        var file = await bonusService.ExportAsync(filter, cancellationToken);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    [ProducesResponseType(typeof(BonusPaymentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpGet("{bonusId:int}/payment-detail")]
    public async Task<IActionResult> GetPaymentDetail(int bonusId, CancellationToken cancellationToken) =>
        Ok(await bonusService.GetPaymentDetailAsync(bonusId, cancellationToken));
```

- [ ] **Step 3: Compilar, probar y humo**

Detener la API, `dotnet build BonusTest.slnx` (0 advertencias), `dotnet test` (71), relanzar la API y:
```bash
API=http://localhost:5106/api/v1/Bonus
curl -s "$API/summary"; echo                                   # JSON con pendingCount, paidNet, committedNet...
curl -s "$API/summary?bonusStateCode=0202"; echo               # solo pagados: pendingCount 0
curl -s -D - -o /tmp/bonos.csv "$API/export?departmentCode=TECH1" | grep -i -E "content-type|content-disposition"
head -c 3 /tmp/bonos.csv | od -An -tx1                          # ef bb bf (BOM)
sed -n '1,3p' /tmp/bonos.csv                                    # encabezado + filas
PAID=$(curl -s "$API?bonusStateCode=0202&pageSize=1" | grep -o '"bonusId":[0-9]*' | head -1 | cut -d: -f2)
curl -s "$API/$PAID/payment-detail"; echo                       # lines con suma = grossAmount
REG=$(curl -s "$API?bonusStateCode=0201&pageSize=1" | grep -o '"bonusId":[0-9]*' | head -1 | cut -d: -f2)
curl -s -w ' %{http_code}' "$API/$REG/payment-detail"; echo    # BONUS_NOT_PAID 409
```
Dejar la API corriendo; `dotnet build-server shutdown`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/BonusTest.Api
git commit -m "feat(api): endpoints de resumen, exportación CSV y detalle de pago"
```

---

### Task 4: Front — modelos, API, tarjetas de resumen y exportación

**Files:**
- Modify: `frontend/src/app/core/models/bonus.ts`, `frontend/src/app/core/services/bonus-api.ts`
- Create: `frontend/src/app/core/utils/download.ts`
- Create: `frontend/src/app/features/bonuses/bonus-summary-cards/bonus-summary-cards.ts`, `.html`, `.css`
- Modify: `frontend/src/app/features/bonuses/bonuses.ts`, `bonuses.html`

**Interfaces:**
- Consumes: endpoints de Task 3.
- Produces: `BonusSummary`, `PaymentLine`, `BonusPaymentDetail` (modelos); `BonusApi.summary(filter)`, `BonusApi.export(filter): Observable<Blob>`, `BonusApi.paymentDetail(bonusId)`; `downloadFile(content: Blob, fileName: string)`, `csvFileName(date?: Date)`; componente `BonusSummaryCards` con `summary = input<BonusSummary | null>(null)`.

- [ ] **Step 1: Modelos y servicio**

Al final de `core/models/bonus.ts`:
```ts
export interface BonusSummary {
  pendingCount: number;
  pendingNet: number;
  paidCount: number;
  paidNet: number;
  cancelledCount: number;
  committedGross: number;
  committedTax: number;
  committedNet: number;
}

export interface PaymentLine {
  projectName: string;
  hours: number;
  amount: number;
}

export interface BonusPaymentDetail {
  bonusId: number;
  grossAmount: number;
  lines: PaymentLine[];
}

/** Debe coincidir con BonusService.MaxExportRows en la API. */
export const MAX_EXPORT_ROWS = 10_000;
```

En `core/services/bonus-api.ts`: importar `BonusPaymentDetail` y `BonusSummary`; reemplazar el armado de parámetros de `search` por un helper reutilizado y agregar los métodos:
```ts
  search(filter: BonusFilter, pageIndex: number, pageSize: number): Observable<PagedResponse<BonusListItem>> {
    const params = this.filterParams(filter).set('pageIndex', pageIndex).set('pageSize', pageSize);
    return this.http.get<PagedResponse<BonusListItem>>(this.baseUrl, { params });
  }

  summary(filter: BonusFilter): Observable<BonusSummary> {
    return this.http.get<BonusSummary>(`${this.baseUrl}/summary`, { params: this.filterParams(filter) });
  }

  export(filter: BonusFilter): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/export`, { params: this.filterParams(filter), responseType: 'blob' });
  }

  paymentDetail(bonusId: number): Observable<BonusPaymentDetail> {
    return this.http.get<BonusPaymentDetail>(`${this.baseUrl}/${bonusId}/payment-detail`);
  }

  /** Filtros opcionales compartidos por listado, resumen y exportación. */
  private filterParams(filter: BonusFilter): HttpParams {
    let params = new HttpParams();
    if (filter.departmentCode) params = params.set('departmentCode', filter.departmentCode);
    if (filter.employeeNumber !== null) params = params.set('employeeNumber', filter.employeeNumber);
    if (filter.period) params = params.set('period', filter.period);
    if (filter.bonusStateCode) params = params.set('bonusStateCode', filter.bonusStateCode);
    return params;
  }
```

`core/utils/download.ts`
```ts
/** Descarga un Blob como archivo con un enlace temporal. */
export function downloadFile(content: Blob, fileName: string): void {
  const url = URL.createObjectURL(content);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url));
}

/** bonos-AAAAMMDD.csv con la fecha local. */
export function csvFileName(date: Date = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, '0');
  return `bonos-${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}.csv`;
}
```

- [ ] **Step 2: Tarjetas de resumen**

`features/bonuses/bonus-summary-cards/bonus-summary-cards.ts`
```ts
import { DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { BonusSummary } from '../../../core/models/bonus';

/** Tarjetas con lo pendiente de pagar, lo pagado y lo comprometido según los filtros aplicados. */
@Component({
  selector: 'app-bonus-summary-cards',
  imports: [DecimalPipe],
  templateUrl: './bonus-summary-cards.html',
  styleUrl: './bonus-summary-cards.css',
})
export class BonusSummaryCards {
  readonly summary = input<BonusSummary | null>(null);
  protected readonly placeholders = [0, 1, 2, 3];
}
```

`bonus-summary-cards.html`
```html
<section class="summary" aria-label="Resumen de bonos con los filtros aplicados">
  @if (summary(); as totals) {
    <article class="tile">
      <span class="label">Por pagar</span>
      <strong class="value">{{ totals.pendingNet | number: '1.2-2' }}</strong>
      <span class="hint">{{ totals.pendingCount }} {{ totals.pendingCount === 1 ? 'bono registrado' : 'bonos registrados' }}</span>
    </article>
    <article class="tile">
      <span class="label">Pagado</span>
      <strong class="value">{{ totals.paidNet | number: '1.2-2' }}</strong>
      <span class="hint">{{ totals.paidCount }} {{ totals.paidCount === 1 ? 'bono pagado' : 'bonos pagados' }}</span>
    </article>
    <article class="tile">
      <span class="label">Anulados</span>
      <strong class="value">{{ totals.cancelledCount }}</strong>
      <span class="hint">No suman a los montos</span>
    </article>
    <article class="tile tile-total">
      <span class="label">Total comprometido (neto)</span>
      <strong class="value">{{ totals.committedNet | number: '1.2-2' }}</strong>
      <span class="hint">Bruto {{ totals.committedGross | number: '1.2-2' }} · Retención {{ totals.committedTax | number: '1.2-2' }}</span>
    </article>
  } @else {
    @for (placeholder of placeholders; track placeholder) {
      <article class="tile skeleton" aria-hidden="true"></article>
    }
  }
</section>
```

`bonus-summary-cards.css`
```css
.summary {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin-bottom: 20px;
}

.tile {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-height: 96px;
  padding: 14px 16px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
}

.tile-total {
  background: var(--info-bg);
  border-color: transparent;
}

.label {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.value {
  font-size: 1.5rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.tile-total .value {
  color: var(--primary);
}

.hint {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.skeleton {
  background: var(--neutral-bg);
  animation: pulse 1.2s ease-in-out infinite;
}

@keyframes pulse {
  50% {
    opacity: 0.55;
  }
}

@media (prefers-reduced-motion: reduce) {
  .skeleton {
    animation: none;
  }
}

@media (max-width: 900px) {
  .summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 520px) {
  .summary {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 3: Integrar en la pantalla**

En `features/bonuses/bonuses.ts`:
- Imports: agregar `BonusSummary` y `MAX_EXPORT_ROWS` desde `../../core/models/bonus`; `downloadFile`, `csvFileName` desde `../../core/utils/download`; `BonusSummaryCards` desde `./bonus-summary-cards/bonus-summary-cards` (y agregarlo al arreglo `imports` del componente).
- Campos nuevos:
```ts
  private readonly summaryReload$ = new Subject<void>();
  protected readonly summary = signal<BonusSummary | null>(null);
  protected readonly exporting = signal(false);
```
- En el constructor, después de la suscripción de `reload$` y reemplazando la última línea `this.reload$.next();` por `this.reload(false);`:
```ts
    this.summaryReload$
      .pipe(
        switchMap(() => this.bonusApi.summary(this.appliedFilter).pipe(catchError(() => EMPTY))),
        takeUntilDestroyed(),
      )
      .subscribe((summary) => this.summary.set(summary));
    this.reload(false);
```
- `reload(resetPage)` también refresca el resumen (lo usan Buscar, Limpiar, registrar y las acciones; cambiar de página no):
```ts
  protected reload(resetPage: boolean): void {
    if (resetPage) {
      this.pageIndex.set(0);
    }
    this.reload$.next();
    this.summaryReload$.next();
  }
```
- Exportación:
```ts
  protected exportCsv(): void {
    const total = this.page()?.totalRecords ?? 0;
    if (total === 0) {
      this.notification.show('warning', 'No hay bonos para exportar con estos filtros.');
      return;
    }
    if (total > MAX_EXPORT_ROWS) {
      this.notification.show('warning', 'Hay más de 10 000 bonos con estos filtros; acota la búsqueda para exportar.');
      return;
    }
    this.exporting.set(true);
    this.bonusApi
      .export(this.appliedFilter)
      .pipe(finalize(() => this.exporting.set(false)))
      .subscribe({ next: (file) => downloadFile(file, csvFileName()), error: noop });
  }
```

En `features/bonuses/bonuses.html`:
- Justo antes de `<form class="filters" ...>`: `<app-bonus-summary-cards [summary]="summary()" />`
- En `.filter-actions`, después de Limpiar:
```html
      <button matButton="outlined" type="button" (click)="exportCsv()" [disabled]="exporting()">
        <mat-icon>download</mat-icon>
        {{ exporting() ? 'Exportando…' : 'Exportar CSV' }}
      </button>
```
- Subtítulo: `Registra, paga, anula, elimina y exporta bonos por periodo.`

- [ ] **Step 4: Compilar**

Run: `cd frontend && npx ng build` → sin errores ni avisos de presupuesto (anotar "Initial total" y el tamaño del chunk `bonuses`).

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat(front): resumen por estado y exportación CSV del listado"
```

---

### Task 5: Front — detalle del pago

**Files:**
- Create: `frontend/src/app/features/bonuses/payment-detail-dialog/payment-detail-dialog.ts`
- Modify: `frontend/src/app/features/bonuses/bonuses.ts`, `bonuses.html`, `bonuses.css`

**Interfaces:**
- Consumes: `BonusApi.paymentDetail` (Task 4); `BonusStates.Paid`.
- Produces: `PaymentDetailDialog` con datos `PaymentDetailDialogData { bonusId: number; fullName: string; period: string }`.

- [ ] **Step 1: Diálogo**

`features/bonuses/payment-detail-dialog/payment-detail-dialog.ts`
```ts
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { catchError, of } from 'rxjs';
import { BonusApi } from '../../../core/services/bonus-api';

export interface PaymentDetailDialogData {
  bonusId: number;
  fullName: string;
  period: string;
}

/** Reparto del bono pagado entre los proyectos del empleado (para imputación contable). */
@Component({
  selector: 'app-payment-detail-dialog',
  imports: [DecimalPipe, MatButtonModule, MatDialogModule, MatProgressBarModule],
  template: `
    <h2 mat-dialog-title>Detalle del pago</h2>
    <mat-dialog-content>
      <p class="subtitle">{{ data.fullName }} · {{ data.period }}</p>
      @if (detail() === undefined) {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando detalle del pago" />
      } @else if (detail(); as paid) {
        <table class="detail">
          <thead>
            <tr>
              <th scope="col">Proyecto</th>
              <th scope="col" class="amount">Horas</th>
              <th scope="col" class="amount">Monto</th>
            </tr>
          </thead>
          <tbody>
            @for (line of paid.lines; track $index) {
              <tr>
                <td>{{ line.projectName }}</td>
                <td class="amount">{{ line.hours | number: '1.2-2' }}</td>
                <td class="amount">{{ line.amount | number: '1.2-2' }}</td>
              </tr>
            }
          </tbody>
          <tfoot>
            <tr>
              <th scope="row">Total</th>
              <td class="amount">{{ totalHours() | number: '1.2-2' }}</td>
              <td class="amount">{{ paid.grossAmount | number: '1.2-2' }}</td>
            </tr>
          </tfoot>
        </table>
      } @else {
        <p class="error">No se pudo cargar el detalle del pago.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton="filled" type="button" mat-dialog-close>Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    .subtitle { margin: 0 0 12px; color: var(--text-muted); }
    .detail { width: 100%; border-collapse: collapse; }
    .detail th, .detail td { padding: 8px 4px; border-bottom: 1px solid var(--border); text-align: left; }
    .detail .amount { text-align: right; font-variant-numeric: tabular-nums; }
    .detail tfoot th, .detail tfoot td { font-weight: 600; border-bottom: 0; }
    .error { color: var(--text-muted); }
  `,
})
export class PaymentDetailDialog {
  protected readonly data = inject<PaymentDetailDialogData>(MAT_DIALOG_DATA);
  // undefined = cargando; null = falló (el interceptor ya mostró el motivo).
  protected readonly detail = toSignal(
    inject(BonusApi).paymentDetail(this.data.bonusId).pipe(catchError(() => of(null))),
    { initialValue: undefined },
  );
  protected readonly totalHours = computed(() =>
    (this.detail()?.lines ?? []).reduce((sum, line) => sum + line.hours, 0),
  );
}
```

- [ ] **Step 2: Acción en la grilla**

En `bonuses.ts`: importar `PaymentDetailDialog`, `PaymentDetailDialogData` y agregar:
```ts
  protected isPaid(bonus: BonusListItem): boolean {
    return bonus.bonusStateCode === BonusStates.Paid;
  }

  protected showPaymentDetail(bonus: BonusListItem): void {
    this.dialog.open<PaymentDetailDialog, PaymentDetailDialogData>(PaymentDetailDialog, {
      data: { bonusId: bonus.bonusId, fullName: bonus.fullName, period: bonus.period },
      width: '520px',
      maxWidth: '95vw',
      injector: this.injector,
    });
  }
```

En `bonuses.html`, en la celda `actions`, después del botón Eliminar:
```html
          <button
            matIconButton
            type="button"
            matTooltip="Ver detalle del pago"
            [attr.aria-label]="'Ver detalle del pago de ' + bonus.fullName + ' ' + bonus.period"
            [disabled]="!isPaid(bonus)"
            (click)="showPaymentDetail(bonus)"
          >
            <mat-icon>receipt_long</mat-icon>
          </button>
```

En `bonuses.css`, ampliar la columna de acciones (`.mat-column-actions`) para cuatro íconos: `width: 192px;`.

- [ ] **Step 3: Compilar y commit**

Run: `cd frontend && npx ng build` → sin errores ni avisos.
```bash
git add frontend/src
git commit -m "feat(front): ver el detalle del pago por proyecto"
```

---

### Task 6: Docker Compose de un comando

**Files:**
- Create: `backend/Dockerfile`, `backend/.dockerignore`, `frontend/Dockerfile`, `frontend/.dockerignore`, `frontend/nginx.conf`, `docker/db-init.sh`, `docker-compose.yml`, `.env.example`, `.gitattributes`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: scripts `database/01..03`, API y front existentes (puertos 5106 y 4200, CORS de Development).
- Produces: `docker compose up --build` levanta `sql`, `db-init`, `api`, `web`.

- [ ] **Step 1: Archivos**

`backend/Dockerfile`
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY nuget.config ./
COPY src/BonusTest.Domain/BonusTest.Domain.csproj src/BonusTest.Domain/
COPY src/BonusTest.Application/BonusTest.Application.csproj src/BonusTest.Application/
COPY src/BonusTest.Infrastructure/BonusTest.Infrastructure.csproj src/BonusTest.Infrastructure/
COPY src/BonusTest.Api/BonusTest.Api.csproj src/BonusTest.Api/
RUN dotnet restore src/BonusTest.Api/BonusTest.Api.csproj
COPY src/ src/
RUN dotnet publish src/BonusTest.Api/BonusTest.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BonusTest.Api.dll"]
```

`backend/.dockerignore`
```
**/bin/
**/obj/
tests/
*.user
.vs/
```

`frontend/Dockerfile`
```dockerfile
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
EXPOSE 80
```

`frontend/.dockerignore`
```
node_modules/
dist/
.angular/
```

`frontend/nginx.conf`
```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  index index.html;

  # Rutas del SPA (p. ej. /bonuses) caen en index.html.
  location / {
    try_files $uri $uri/ /index.html;
  }
}
```

`docker/db-init.sh`
```bash
#!/bin/bash
# Inicializa la BD en Docker: 01 solo si BonusTest no existe (no es idempotente); 02 y 03 siempre (lo son).
set -euo pipefail
SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -P "$MSSQL_SA_PASSWORD" -C -I -b)

exists=$("${SQLCMD[@]}" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'BonusTest'")
if [ "$exists" = "0" ]; then
  "${SQLCMD[@]}" -i /database/01_schema_seed.sql
else
  echo "BonusTest ya existe: se omite 01_schema_seed.sql"
fi
"${SQLCMD[@]}" -i /database/02_bonus.sql
"${SQLCMD[@]}" -i /database/03_bonus_payment_detail.sql
echo "Base de datos lista."
```

`.gitattributes`
```
# El script de inicialización corre en Linux: siempre con LF.
*.sh text eol=lf
```

`docker-compose.yml`
```yaml
name: bonustest

services:
  sql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:-BonusTest#2026}"
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q \"SELECT 1\" -b -o /dev/null"]
      interval: 5s
      timeout: 5s
      retries: 30
      start_period: 20s

  db-init:
    image: mcr.microsoft.com/mssql/server:2022-latest
    depends_on:
      sql:
        condition: service_healthy
    environment:
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD:-BonusTest#2026}"
    volumes:
      - ./database:/database:ro
      - ./docker/db-init.sh:/db-init.sh:ro
    entrypoint: ["/bin/bash", "/db-init.sh"]
    restart: "no"

  api:
    build: ./backend
    depends_on:
      db-init:
        condition: service_completed_successfully
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__BonusTest: "Server=sql;Database=BonusTest;User Id=sa;Password=${MSSQL_SA_PASSWORD:-BonusTest#2026};TrustServerCertificate=True;"
    ports:
      - "5106:8080"

  web:
    build: ./frontend
    depends_on:
      - api
    ports:
      - "4200:80"
```

`.env.example`
```
# Copia este archivo a .env para cambiar la contraseña del SA (por defecto: BonusTest#2026).
MSSQL_SA_PASSWORD=BonusTest#2026
```

`.gitignore`: agregar al final
```
# Variables locales de Docker Compose
.env
```

- [ ] **Step 2: Validar la configuración (liviano)**

Run: `docker compose config --quiet && echo OK` → `OK`.

- [ ] **Step 3: Levantar de punta a punta (lo coordina el controlador)**

Precondición (el controlador la deja lista antes de despachar): servidores de desarrollo en :4200 y :5106 detenidos y contenedor `bonustest-sql` detenido para liberar memoria.
```bash
docker compose up --build -d
docker compose ps -a                      # db-init: Exited (0); sql healthy; api y web Up
docker compose logs db-init | tail -5     # "Base de datos lista."
curl -s -o /dev/null -w 'web:%{http_code}\n' http://localhost:4200/bonuses
curl -s -o /dev/null -w 'swagger:%{http_code}\n' http://localhost:5106/swagger/index.html
curl -s http://localhost:5106/api/v1/Bonus/summary; echo
docker compose up -d                      # segunda vez: db-init omite 01 y termina 0
docker compose ps -a | grep db-init
docker compose down
```
Expected: web 200, swagger 200, summary JSON con ceros (BD nueva), db-init termina 0 ambas veces.

- [ ] **Step 4: Commit**

```bash
git add backend/Dockerfile backend/.dockerignore frontend/Dockerfile frontend/.dockerignore frontend/nginx.conf docker docker-compose.yml .env.example .gitattributes .gitignore
git commit -m "feat: Docker Compose para levantar BD, API y front con un comando"
```

---

### Task 7: README

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Actualizar**

- Sección "Cómo levantarlo": nueva **Opción 1: Docker (un comando)** — `docker compose up --build`, luego http://localhost:4200/bonuses y http://localhost:5106/swagger; nota: detener antes cualquier SQL/API/front local en esos puertos; `.env.example` para cambiar la contraseña; `docker compose down` para apagar (la BD de Docker se recrea limpia en el siguiente `up`). La configuración actual pasa a **Opción 2: local**.
- Tabla de endpoints: agregar `GET /api/v1/Bonus/summary`, `GET /api/v1/Bonus/export` (CSV, 409 `EXPORT_TOO_LARGE` si hay más de 10 000), `GET /api/v1/Bonus/{bonusId}/payment-detail` (409 `BONUS_NOT_PAID`).
- Nueva sección **Funciones para RR. HH. y Finanzas**: resumen por estado (por pagar, pagado, anulados, comprometido; respeta filtros), exportar CSV para planilla (UTF-8 con BOM, coma, decimales con punto, protección contra fórmulas), detalle del pago por proyecto.
- Decisiones: CSV generado en Application sin dependencias; el resumen es una sola consulta `GROUP BY`; Docker `db-init` corre 01 solo si la BD no existe.
- Pruebas: actualizar el conteo (71).

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: README con Docker, endpoints y funciones nuevas"
```

---

## Verificación final (controlador)

- `dotnet build` 0 advertencias, `dotnet test` 71/71, `npx ng build` sin avisos.
- Navegador (escritorio y 390 px): tarjetas cambian con filtros y tras pagar/anular/registrar; Exportar CSV descarga `bonos-AAAAMMDD.csv` y con 0 resultados avisa sin llamar a la API; Ver detalle solo en Pagado, total = bruto.
- `docker compose up --build` (Task 6 Step 3).
- Revisión final de la rama (desde `d74fcb9`).
