using Microsoft.AspNetCore.Mvc;
using BonusTest.Application.Bonuses;
using BonusTest.Application.Common;

namespace BonusTest.Api.Controllers;

/// <summary>Módulo de bonos de desempeño. Sin reglas de negocio: delega todo en BonusService.</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class BonusController(BonusService bonusService) : ControllerBase
{
    private const string UserCodeHeader = "UserCode";

    [ProducesResponseType(typeof(PagedResponse<BonusListItemDto>), StatusCodes.Status200OK)]
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] BonusFilter filter, CancellationToken cancellationToken) =>
        Ok(await bonusService.SearchAsync(filter, cancellationToken));

    [ProducesResponseType(typeof(EmployeeLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpGet("employee/{departmentCode}/{employeeNumber:int}")]
    public async Task<IActionResult> GetEmployee(string departmentCode, int employeeNumber, CancellationToken cancellationToken) =>
        Ok(await bonusService.GetActiveEmployeeAsync(departmentCode, employeeNumber, cancellationToken));

    [ProducesResponseType(typeof(RegisteredBonusDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost]
    public async Task<IActionResult> Register(RegisterBonusRequest request,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await bonusService.RegisterAsync(request, userCode, cancellationToken));

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPut("{bonusId:int}/cancel")]
    public async Task<IActionResult> Cancel(int bonusId,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken)
    {
        await bonusService.CancelAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpDelete("{bonusId:int}")]
    public async Task<IActionResult> Delete(int bonusId,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken)
    {
        await bonusService.DeleteAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [HttpPost("{bonusId:int}/pay")]
    public async Task<IActionResult> Pay(int bonusId,
        [FromHeader(Name = UserCodeHeader)] string? userCode, CancellationToken cancellationToken)
    {
        await bonusService.PayAsync(bonusId, userCode, cancellationToken);
        return NoContent();
    }
}
