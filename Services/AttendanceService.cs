using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AttendanceService> _logger;

        public AttendanceService(ApplicationDbContext context, ILogger<AttendanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> AddManualEntryAsync(ManualAttendanceEntryDto entry, int companyId, int userId, string ipAddress)
        {
            try
            {
                // ✅ ONLY parse InTime if provided
                DateTime? inDateTime = null;
                if (!string.IsNullOrEmpty(entry.InTime))
                {
                    inDateTime = DateTime.Parse($"{entry.Date:yyyy-MM-dd} {entry.InTime}");
                }

                // ✅ ONLY parse OutTime if provided
                DateTime? outDateTime = null;
                if (!string.IsNullOrEmpty(entry.OutTime))
                {
                    // ✅ ONLY parse OutTime if provided

if (!string.IsNullOrEmpty(entry.OutTime))
{
    string outDate = !string.IsNullOrEmpty(entry.OutDate)
        ? entry.OutDate
        : entry.Date.ToString("yyyy-MM-dd");

    outDateTime = DateTime.Parse($"{outDate} {entry.OutTime}");
}
                }

                // Set period key
                string periodKey = entry.PeriodKey;
                if (string.IsNullOrEmpty(periodKey))
                {
                    periodKey = $"{entry.Date:yyyy-MMM}";
                }

                // 1. SAVE IN ATTENDANCE - ONLY if InTime provided
                if (inDateTime.HasValue)
                {
                    var existingIn = await _context.AttendanceRecords
                        .FirstOrDefaultAsync(a => a.employee_id == entry.EmployeeId && a.datetime == inDateTime.Value);

                    if (existingIn == null)
                    {
                        _context.AttendanceRecords.Add(new AttendanceRecord
                        {
                            company_id = companyId,
                            datetime = inDateTime.Value,
                            employee_id = entry.EmployeeId,
                            input_datetime = DateTime.Now,
                            input_type = "IN"
                        });
                    }
                    else
                    {
                        existingIn.company_id = companyId;
                        existingIn.datetime = inDateTime.Value;
                        existingIn.employee_id = entry.EmployeeId;
                        existingIn.input_datetime = DateTime.Now;
                        existingIn.input_type = "IN";
                    }
                }

                // 2. SAVE OUT ATTENDANCE - ONLY if OutTime provided
                if (outDateTime.HasValue)
                {
                    var existingOut = await _context.AttendanceRecords
                        .FirstOrDefaultAsync(a => a.employee_id == entry.EmployeeId && a.datetime == outDateTime.Value);

                    if (existingOut == null)
                    {
                        _context.AttendanceRecords.Add(new AttendanceRecord
                        {
                            company_id = companyId,
                            datetime = outDateTime.Value,
                            employee_id = entry.EmployeeId,
                            input_datetime = DateTime.Now,
                            input_type = "OUT"
                        });
                    }
                    else
                    {
                        existingOut.company_id = companyId;
                        existingOut.datetime = outDateTime.Value;
                        existingOut.employee_id = entry.EmployeeId;
                        existingOut.input_datetime = DateTime.Now;
                        existingOut.input_type = "OUT";
                    }
                }

                // 3. GET EMPLOYEE DETAILS
                var employee = await _context.Employees
                    .Where(e => e.Id == entry.EmployeeId)
                    .Select(e => new { e.EmployeeNo, e.EPFNo })
                    .FirstOrDefaultAsync();

                string empNo = employee?.EmployeeNo?.ToString() ?? "Unknown";
                string epfNo = employee?.EPFNo.ToString() ?? "Unknown";

                // 4. LOG THE MANUAL ENTRY
                _context.AttendanceManualLogs.Add(new AttendanceManualLog
                {
                    EmployeeId = entry.EmployeeId,
                    Action = "ManualAttendanceUpdate",
                    RequestData = $"In: {inDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"}, Out: {outDateTime?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"}, EmpNo: {empNo}, EPF: {epfNo}",
                    IpAddress = ipAddress,
                    Timestamp = DateTime.Now,
                    UserId = userId,
                    ResponseStatus = "Success"
                });

                // 5. CHECK FOR EXISTING PROCESS QUEUE RECORD
                var existingProcessQueue = await _context.ProcessQueues
                    .Where(p => p.emp_systemID == entry.EmployeeId 
                                && p.period_ == periodKey)
                    .OrderByDescending(p => p.request_date)
                    .FirstOrDefaultAsync();

                bool shouldCreateNewQueue = false;

                if (existingProcessQueue == null)
                {
                    shouldCreateNewQueue = true;
                    _logger?.LogInformation("No existing process queue record found for employee {EmployeeId} period {PeriodKey}", 
                        entry.EmployeeId, periodKey);
                }
                else
                {
                    bool isCompleted = existingProcessQueue.process_end;
                    bool isProcessing = existingProcessQueue.processing;

                    if (isCompleted)
                    {
                        shouldCreateNewQueue = true;
                        _logger?.LogInformation("Previous process queue record completed for employee {EmployeeId} period {PeriodKey}, creating new", 
                            entry.EmployeeId, periodKey);
                    }
                    else if (isProcessing)
                    {
                        shouldCreateNewQueue = true;
                        _logger?.LogInformation("Process queue record is currently processing for employee {EmployeeId} period {PeriodKey} - Adding new record", 
                            entry.EmployeeId, periodKey);
                    }
                    else
                    {
                        shouldCreateNewQueue = false;
                        _logger?.LogWarning("Found stuck/pending process queue record for employee {EmployeeId} period {PeriodKey} - Updating existing", 
                            entry.EmployeeId, periodKey);

                        existingProcessQueue.request_date = DateTime.Now;
                        existingProcessQueue.request_ip = ipAddress;
                        existingProcessQueue.request_by = userId.ToString();
                    }
                }

                // 6. Insert new record if needed
                if (shouldCreateNewQueue)
                {
                    var processQueue = new ProcessQueue
                    {
                        emp_empID = empNo,
                        emp_systemID = entry.EmployeeId,
                        period_ = periodKey,
                        processStart_date = DateTime.Now,
                        process_end = false,
                        process_status = 0,
                        processing = false,
                        request_by = userId.ToString(),
                        request_date = DateTime.Now,
                        request_ip = ipAddress,
                        requset_from = "ManualAttendanceUpdate",
                        processEnd_stageOne_date = DateTime.Now,
                        processEnd_stageTwo_date = DateTime.Now
                    };

                    _context.ProcessQueues.Add(processQueue);
                    _logger?.LogInformation("Added new process queue entry for employee {EmployeeId} period {PeriodKey}", 
                        entry.EmployeeId, periodKey);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error adding manual attendance entry for employee {EmployeeId}", entry.EmployeeId);
                throw;
            }
        }
    }
}