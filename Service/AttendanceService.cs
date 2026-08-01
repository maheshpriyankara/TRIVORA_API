using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public class AttendanceService : IAttendanceService
{
    private readonly TRIVORA_API.Data.ApplicationDbContext _context;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(TRIVORA_API.Data.ApplicationDbContext context, ILogger<AttendanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> AddManualEntryAsync(ManualAttendanceEntryDto entry, int companyId, int userId, string ipAddress)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Insert IN and/or OUT records into Attendance table
            var dateOnly = entry.Date.Date;
            if (!string.IsNullOrEmpty(entry.InTime))
            {
                var inDateTime = dateOnly.Add(TimeSpan.Parse(entry.InTime));
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    company_id = companyId,
                    employee_id = entry.EmployeeId,
                    datetime = inDateTime,
                    input_type = "ManualIn",
                    input_datetime = DateTime.UtcNow
                });
            }

            if (!string.IsNullOrEmpty(entry.OutTime))
            {
                var outDate = entry.OutDate ?? entry.Date;
                var outDateTime = outDate.Date.Add(TimeSpan.Parse(entry.OutTime));
                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    company_id = companyId,
                    employee_id = entry.EmployeeId,
                    datetime = outDateTime,
                    input_type = "ManualOut",
                    input_datetime = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            // 2. Log success
            await LogManualAction(entry.EmployeeId, "ManualUpdate", entry, "Success", null, userId, ipAddress);

            // 3. Queue salary processing if not already pending
            string periodKey = GetCurrentSalaryPeriodKey(); // e.g., "2026-May"
            bool alreadyQueued = await _context.ProcessQueues
                .AnyAsync(pq => pq.emp_systemID == entry.EmployeeId
                             && pq.period_ == periodKey
                             && (pq.process_status == false || pq.processing == true || pq.process_end == false));

            if (!alreadyQueued)
            {
                var employee = await _context.Employees.FindAsync(entry.EmployeeId);
                _context.ProcessQueues.Add(new ProcessQueue
                {
                    emp_systemID = entry.EmployeeId,
                    emp_empID = employee?.EmployeeNo ?? "",
                    requset_from = "ManualAttendanceUpdate",
                    request_date = DateTime.UtcNow,
                    request_by = userId.ToString(),
                    request_ip = ipAddress,
                    process_status = false,   // pending
                    processing = false,
                    process_end = false,
                    processStart_date = DateTime.UtcNow,
                    processEnd_stageOne_date = DateTime.UtcNow,
                    processEnd_stageTwo_date = DateTime.UtcNow,
                    period_ = periodKey
                });
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Manual attendance save failed for employee {EmployeeId}", entry.EmployeeId);
            await LogManualAction(entry.EmployeeId, "Error", entry, "Error", ex.Message, userId, ipAddress);
            return false;
        }
    }

    private async Task LogManualAction(int employeeId, string action, object data, string status, string errorMsg, int? userId, string ip)
    {
        _context.AttendanceManualLogs.Add(new AttendanceManualLog
        {
            EmployeeId = employeeId,
            Action = action,
            RequestData = JsonSerializer.Serialize(data),
            ResponseStatus = status,
            ErrorMessage = errorMsg,
            Timestamp = DateTime.UtcNow,
            IpAddress = ip,
            UserId = userId
        });
        await _context.SaveChangesAsync();
    }

    private string GetCurrentSalaryPeriodKey()
    {
        var now = DateTime.UtcNow;
        return $"{now.Year}-{now:MMMM}";
    }
}