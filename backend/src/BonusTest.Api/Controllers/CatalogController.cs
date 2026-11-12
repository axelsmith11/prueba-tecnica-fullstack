using Microsoft.AspNetCore.Mvc;
using BonusTest.Application.Catalogs;

namespace BonusTest.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CatalogController(CatalogService catalogService) : ControllerBase
{
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetDepartmentsAsync(cancellationToken));

    /// <param name="masterStateCode">01 = estados de empleado, 02 = estados de bono.</param>
    [HttpGet("states/{masterStateCode}")]
    public async Task<IActionResult> GetStates(string masterStateCode, CancellationToken cancellationToken) =>
        Ok(await catalogService.GetStatesAsync(masterStateCode, cancellationToken));

    [HttpGet("bonus-types")]
    public async Task<IActionResult> GetBonusTypes(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetBonusTypesAsync(cancellationToken));

    [HttpGet("bonus-reasons")]
    public async Task<IActionResult> GetBonusReasons(CancellationToken cancellationToken) =>
        Ok(await catalogService.GetActiveBonusReasonsAsync(cancellationToken));
}
