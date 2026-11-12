namespace BonusTest.Domain.Exceptions;

/// <summary>
/// Error de regla de negocio. El middleware de la API lo devuelve como HTTP 409
/// con el cuerpo { code, description }.
/// </summary>
public class BusinessException(string code, string description) : Exception(description)
{
    public string Code { get; } = code;
    public string Description { get; } = description;
}
