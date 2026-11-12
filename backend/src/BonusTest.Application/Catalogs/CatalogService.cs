namespace BonusTest.Application.Catalogs;

public class CatalogService(ICatalogRepository repository)
{
    public Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken) =>
        repository.GetDepartmentsAsync(cancellationToken);

    public Task<IReadOnlyList<StateDto>> GetStatesAsync(string masterStateCode, CancellationToken cancellationToken) =>
        repository.GetStatesAsync(masterStateCode, cancellationToken);

    public Task<IReadOnlyList<BonusTypeDto>> GetBonusTypesAsync(CancellationToken cancellationToken) =>
        repository.GetBonusTypesAsync(cancellationToken);

    public Task<IReadOnlyList<BonusReasonDto>> GetActiveBonusReasonsAsync(CancellationToken cancellationToken) =>
        repository.GetActiveBonusReasonsAsync(cancellationToken);
}
