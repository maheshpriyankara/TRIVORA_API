public interface IPayrollPeriodLockService
{
    // Return true if the period is locked; throw if it cannot be verified.
    Task<bool> IsLockedAsync(int companyId, string periodKey, CancellationToken ct = default);
}