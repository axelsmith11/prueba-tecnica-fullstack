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
