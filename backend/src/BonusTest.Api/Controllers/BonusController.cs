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
