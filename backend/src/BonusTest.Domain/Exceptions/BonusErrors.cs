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

    public static BusinessException BonusNotPaid() =>
        new("BONUS_NOT_PAID", "El bono aún no ha sido pagado; no tiene detalle por proyecto.");

    public static BusinessException ExportTooLarge() =>
        new("EXPORT_TOO_LARGE", "Hay más de 10 000 bonos con estos filtros; acota la búsqueda para exportar.");
}
