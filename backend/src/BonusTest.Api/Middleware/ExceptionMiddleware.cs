using BonusTest.Domain.Exceptions;

namespace BonusTest.Api.Middleware;

/// <summary>
/// Convierte las excepciones en respuestas JSON { code, description }:
/// BusinessException → 409, cualquier otra → 500 sin exponer el detalle.
/// </summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BusinessException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { code = ex.Code, description = ex.Description });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error no controlado");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "UNHANDLED",
                description = "Ocurrió un error inesperado. Intenta nuevamente."
            });
        }
    }
}
