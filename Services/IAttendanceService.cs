// Services/IAttendanceService.cs


namespace TRIVORA_API.Services
{
    public interface IAttendanceService
    {
        Task<bool> AddManualEntryAsync(ManualAttendanceEntryDto entry, int companyId, int userId, string ipAddress);
         // Add other methods as needed
    }

    // DTOs for attendance service
    

    public class ManualAttendanceEntryDto
{
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }

    /// <summary>Alias for Date — accepts frontend's `dateTime` field.</summary>
    public DateTime DateTime
    {
        get => Date;
        set => Date = value;
    }

    public string? InTime { get; set; }
    public string? OutTime { get; set; }
    public string? OutDate { get; set; }
    public string? ShiftType { get; set; }
    public string? PeriodKey { get; set; }
}
}