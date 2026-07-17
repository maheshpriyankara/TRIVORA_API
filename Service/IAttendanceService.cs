public interface IAttendanceService
{
    Task<bool> AddManualEntryAsync(ManualAttendanceEntryDto entry, int companyId, int userId, string ipAddress);
}