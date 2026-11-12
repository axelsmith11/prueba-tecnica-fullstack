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
