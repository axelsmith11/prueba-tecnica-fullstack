namespace BonusTest.Application.Catalogs;

public interface ICatalogRepository
{
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StateDto>> GetStatesAsync(string masterStateCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<BonusTypeDto>> GetBonusTypesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BonusReasonDto>> GetActiveBonusReasonsAsync(CancellationToken cancellationToken);
}
