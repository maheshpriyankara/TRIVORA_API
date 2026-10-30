using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using TRIVORA_API.Data;
using TRIVORA_API.Services;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers
{
    // ========== DTO CLASSES ==========
    public class LeaveReportRequest
{
    public int CompanyId { get; set; }
    public string Scope { get; set; } = "selected";       // selected | all | lines
    public int? EmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    public string PeriodMode { get; set; } = "summary";   // range | summary

    // Range mode
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }

    // Summary mode
    public int? FromYear { get; set; }
    public string? FromMonthName { get; set; }
    public int? ToYear { get; set; }
    public string? ToMonthName { get; set; }
}

public class LeaveBucket { public double Full { get; set; } public double Half { get; set; } }

public class LeaveBuckets
{
    public LeaveBucket Annual { get; set; } = new();
    public LeaveBucket Casual { get; set; } = new();
    public LeaveBucket Sick   { get; set; } = new();
    public LeaveBucket Other  { get; set; } = new();
}

public class LeavePeriodEmployeeRow
{
    public int EmployeeId { get; set; }
    public int EpfNo { get; set; } = 0;
    public string EmployeeName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public LeaveBuckets Buckets { get; set; } = new();
    public double Total { get; set; }
}

public class LeavePeriodBlock
{
    public int Year { get; set; }
    public string MonthName { get; set; } = "";
    public bool Locked { get; set; }
    public bool Processing { get; set; }
    public List<LeavePeriodEmployeeRow> Employees { get; set; } = new();
}

public class LeaveDayRow
{
    public string Date { get; set; } = "";             // yyyy-mm-dd
    public double Annual { get; set; }
    public double AnnualHalf { get; set; }
    public double Casual { get; set; }
    public double CasualHalf { get; set; }
    public double Sick { get; set; }
    public double SickHalf { get; set; }
    public double Other { get; set; }
    public string? OtherType { get; set; }
}

public class LeaveRangeEmployeeBlock
{
    public int EmployeeId { get; set; }
    public int EpfNo { get; set; }
    public string EmployeeName { get; set; } = "";
    public string DepartmentName { get; set; } = "";
    public List<LeaveDayRow> Days { get; set; } = new();
}
    public class AttendanceDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public DateTime DateTime { get; set; }
        public string InputType { get; set; } = "Manual";
        public DateTime? InputDateTime { get; set; }
    }

    public class PayrollPeriod
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public DateTime PayrollStartDate { get; set; }
        public bool Processing { get; set; }
        public bool Locked { get; set; }
    }

    public class SaveTimesheetRequest
    {
        public int EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public List<TimesheetDayData> TimesheetData { get; set; } = new List<TimesheetDayData>();
    }

    public class TimesheetDayData
    {
        public string Date { get; set; } = string.Empty;
        public string ShiftType { get; set; } = string.Empty;
        public string ShiftBlockId { get; set; } = string.Empty;
        public string TimeIn { get; set; } = string.Empty;
        public string DateOut { get; set; } = string.Empty;
        public string TimeOut { get; set; } = string.Empty;
        public string FirstShiftBlockId { get; set; } = string.Empty;
        public string FirstTimeIn { get; set; } = string.Empty;
        public string FirstDateOut { get; set; } = string.Empty;
        public string FirstTimeOut { get; set; } = string.Empty;
        public string SecondShiftBlockId { get; set; } = string.Empty;
        public string SecondTimeIn { get; set; } = string.Empty;
        public string SecondDateOut { get; set; } = string.Empty;
        public string SecondTimeOut { get; set; } = string.Empty;
        public List<LeaveApplicationData> LeaveApplications { get; set; } = new List<LeaveApplicationData>();
    }

    public class LeaveApplicationData
    {
        public string LeaveType { get; set; } = string.Empty;
        public bool IsHalfDay { get; set; }
    }

    public class ManualAttendanceEntryDto
    {
        public int EmployeeId { get; set; }
        public DateTime DateTime { get; set; }
        public string? PeriodKey { get; set; }
    }

    public class SaveShiftBlockRequest
    {
        public int EmployeeId { get; set; }
        public string Date { get; set; } = string.Empty;
        public int ShiftBlockIdFirstShift { get; set; }
        public int? ShiftBlockIdSecondShift { get; set; }
        public string PeriodKey { get; set; } = string.Empty;
    }

    public class NightDateRequest
    {
        public int EmployeeId { get; set; }
        public string Date { get; set; } = string.Empty;
        public bool IsFullNight { get; set; }
        public string? PeriodKey { get; set; }
        public string? Remarks { get; set; }
    }

    // ========== CONTROLLER ==========
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private readonly IAttendanceService _attendanceService;
        private readonly ICompanySettingsService _companySettingsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AttendanceController> _logger;

        public AttendanceController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IAttendanceService attendanceService,
            IHttpContextAccessor httpContextAccessor,
            ICompanySettingsService companySettingsService,
            ILogger<AttendanceController> logger)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            _attendanceService = attendanceService;
            _httpContextAccessor = httpContextAccessor;
            _companySettingsService = companySettingsService;
            _logger = logger;
        }

        // GET: api/attendance/manual-entries
        [HttpGet("manual-entries")]
        public async Task<IActionResult> GetManualEntries(
            [FromQuery] int employeeId,
            [FromQuery] string periodKey)
        {
            try
            {
                if (employeeId <= 0 || string.IsNullOrEmpty(periodKey))
                    return BadRequest(new { success = false, message = "EmployeeId and PeriodKey are required" });

                using var connection = new SqlConnection(_connectionString);

                var periodParts = periodKey.Split('-');
                if (periodParts.Length < 2)
                    return BadRequest(new { success = false, message = "Invalid PeriodKey format" });

                if (!int.TryParse(periodParts[0], out int year))
                    return BadRequest(new { success = false, message = "Invalid year in PeriodKey" });

                int month = GetMonthNumber(periodParts[1]);
                if (month == 0)
                    return BadRequest(new { success = false, message = "Invalid month in PeriodKey" });

                var employee = await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                    return NotFound(new { success = false, message = "Employee not found" });

                var companySettings = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
                int salaryStartDay = companySettings?.SalaryStartDate_Wages ?? 1;

                DateTime startDate, endDate;
                if (salaryStartDay == 1)
                {
                    startDate = new DateTime(year, month, 1);
                    endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                }
                else
                {
                    startDate = new DateTime(year, month, salaryStartDay);
                    var nextMonth = startDate.AddMonths(1);
                    endDate = new DateTime(nextMonth.Year, nextMonth.Month, salaryStartDay).AddDays(-1);
                }

                var sql = @"
                    SELECT 
                        id as Id,
                        employee_id as EmployeeId,
                        company_id as CompanyId,
                        datetime as DateTime,
                        input_type as InputType,
                        input_datetime as InputDateTime
                    FROM Attendance
                    WHERE employee_id = @EmployeeId 
                        AND datetime >= @StartDate 
                        AND datetime <= @EndDate
                    ORDER BY datetime";

                var entries = await connection.QueryAsync<AttendanceDto>(sql, new
                {
                    EmployeeId = employeeId,
                    StartDate = startDate,
                    EndDate = endDate
                });

                return Ok(new { success = true, data = entries });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting manual entries for employee {EmployeeId}", employeeId);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpPost("manual-entry")]
public async Task<IActionResult> AddManualEntry([FromBody] ManualAttendanceEntryDto entry)
{
    try
    {
        _logger.LogInformation(
            "📥 Received manual entry: {Entry}",
            System.Text.Json.JsonSerializer.Serialize(entry));

        string ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // ── 1. Validate input ──
        if (entry == null || entry.EmployeeId <= 0)
            return BadRequest(new { success = false, message = "Invalid data" });

        if (entry.DateTime == DateTime.MinValue)
            return BadRequest(new { success = false, message = "DateTime is required" });

        if (string.IsNullOrEmpty(entry.PeriodKey))
            entry.PeriodKey = $"{entry.DateTime.Year}-{entry.DateTime:MMMM}";

        // ── 2. Confirm employee exists ──
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entry.EmployeeId);

        if (employee == null)
            return NotFound(new { success = false, message = $"Employee with ID {entry.EmployeeId} not found" });

        using var connection = new SqlConnection(_connectionString);

        // ── 3. Exact-match duplicate check scoped to (employee_id, datetime) ──
        //     Truncate to the second so 08:00:00.000 and 08:00:00.123 both match.
        const string existsSql = @"
            SELECT COUNT(*)
            FROM Attendance
            WHERE employee_id = @EmployeeId
              AND datetime    >= @DateTime
              AND datetime    <  DATEADD(SECOND, 1, @DateTime)";

        var exists = await connection.ExecuteScalarAsync<int>(existsSql, new
        {
            EmployeeId = entry.EmployeeId,
            DateTime   = entry.DateTime
        });

        if (exists > 0)
        {
            // Row already present for THIS employee at THIS instant — report as duplicate
            _logger.LogInformation(
                "Duplicate manual entry blocked — employee {EmployeeId}, datetime {DateTime}",
                entry.EmployeeId, entry.DateTime);

            return Conflict(new
            {
                success = false,
                duplicate = true,
                message = $"Attendance already recorded for employee {entry.EmployeeId} at {entry.DateTime:yyyy-MM-dd HH:mm:ss}"
            });
        }

        // ── 4. Insert (no update branch — duplicates are rejected above) ──
        const string insertSql = @"
            INSERT INTO Attendance (employee_id, company_id, datetime, input_type, input_datetime)
            VALUES (@EmployeeId, @CompanyId, @DateTime, 'Manual', GETDATE())";

        var rowsAffected = await connection.ExecuteAsync(insertSql, new
        {
            EmployeeId = entry.EmployeeId,
            CompanyId  = employee.CompanyId,
            DateTime   = entry.DateTime
        });

                _logger.LogInformation("✅ Inserted {Rows} attendance row(s) for employee {EmployeeId}",
            rowsAffected, entry.EmployeeId);

        if (rowsAffected <= 0)
            return StatusCode(500, new { success = false, message = "Failed to save attendance" });

        // ── 5. Make sure the period exists (year + monthName, from PeriodKey) ──
        //      PeriodKey is always "YYYY-MonthName" (e.g. "2025-May") because
        //      we set it at the top of this method when the caller omits it.
        bool periodCreated = false;
        string? periodError = null;
        try
        {
            var pkParts = entry.PeriodKey.Split('-');
            if (pkParts.Length >= 2 &&
                int.TryParse(pkParts[0], out int pkYear) &&
                GetMonthNumber(pkParts[1]) > 0)
            {
                int monthNum = GetMonthNumber(pkParts[1]);
                string monthName = GetMonthNameFromNumber(monthNum);

                int userId = 0;
                var uidClaim = User?.FindFirst("UserId")?.Value ?? User?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(uidClaim)) int.TryParse(uidClaim, out userId);

                var ensured = await EnsurePayrollPeriodAsync(
                    employee.CompanyId, pkYear, monthName, userId);

                periodCreated = ensured.created;

                if (ensured.locked)
                {
                    // Period exists but is locked — log it (not fatal to saving the punch,
                    // but the caller should know the queue won't process it)
                    _logger.LogWarning(
                        "Manual entry saved for locked period {PeriodKey} (employee {EmployeeId})",
                        entry.PeriodKey, entry.EmployeeId);
                }
            }
        }
        catch (Exception ex)
        {
            // Never fail the whole save because of the ensure step — the punch is already in
            periodError = ex.Message;
            _logger.LogError(ex,
                "Failed to auto-create payroll period for {PeriodKey} (employee {EmployeeId})",
                entry.PeriodKey, entry.EmployeeId);
        }

        // ── 6. Queue reprocessing (only meaningful when a period now exists) ──
        await TriggerProcessQueue(entry.EmployeeId, entry.PeriodKey, ipAddress);

        return Ok(new
        {
            success        = true,
            message        = "Attendance saved successfully",
            employeeId     = entry.EmployeeId,
            dateTime       = entry.DateTime,
            periodKey      = entry.PeriodKey,
            periodCreated  = periodCreated,     // ← tell the caller whether a period was created
            periodError    = periodError        // ← null on success; string on ensure failure
        });
    }
    catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 2601 || sqlEx.Number == 2627)
    {
        // 2601 = unique index violation, 2627 = unique constraint violation
        _logger.LogWarning(sqlEx,
            "Unique constraint blocked duplicate attendance for employee {EmployeeId} at {DateTime}",
            entry?.EmployeeId, entry?.DateTime);

        return Conflict(new
        {
            success = false,
            duplicate = true,
            message = "Attendance already exists for this employee at this date/time."
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error saving manual attendance");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
        
        [HttpDelete("manual-entry/{id}")]
        public async Task<IActionResult> DeleteManualEntry(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var getEntrySql = @"
                    SELECT id as Id, employee_id as EmployeeId, company_id as CompanyId,
                           datetime as DateTime, input_type as InputType
                    FROM Attendance WHERE id = @Id";

                var entry = await connection.QueryFirstOrDefaultAsync<AttendanceDto>(getEntrySql, new { Id = id });
                if (entry == null)
                    return NotFound(new { success = false, message = "Attendance entry not found" });

                string periodKey = GetPeriodKeyFromDate(entry.DateTime);

                var isLocked = await IsSalaryPeriodLocked(periodKey);
                if (isLocked)
                    return BadRequest(new { success = false, message = "Cannot delete attendance for locked salary period" });

                int userId = 0;
                var userIdClaim = User.FindFirst("UserId")?.Value ?? User.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out userId);

                string ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                await LogAttendanceAction(entry, "DELETE", userId, ipAddress);

                var deleteSql = "DELETE FROM Attendance WHERE id = @Id";
                var rowsAffected = await connection.ExecuteAsync(deleteSql, new { Id = id });

                if (rowsAffected > 0)
                {
                    await TriggerProcessQueue(entry.EmployeeId, periodKey, ipAddress);
                    return Ok(new { success = true, message = "Attendance deleted successfully and recalculation queued" });
                }

                return StatusCode(500, new { success = false, message = "Failed to delete attendance entry" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting manual attendance entry {Id}", id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("check-period-lock")]
        public async Task<IActionResult> CheckPeriodLock([FromQuery] string periodKey)
        {
            try
            {
                var isLocked = await IsSalaryPeriodLocked(periodKey);
                return Ok(new { success = true, isLocked = isLocked });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking period lock for {PeriodKey}", periodKey);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("employee-shift/{employeeId}")]
        public async Task<IActionResult> GetEmployeeShiftInfo(int employeeId)
        {
            try
            {
                var employeeShift = await (from e in _context.Employees
                                           join sb in _context.ShiftBlocks on e.ShiftBlockId equals sb.Id into shiftJoin
                                           from shift in shiftJoin.DefaultIfEmpty()
                                           where e.Id == employeeId
                                           select new
                                           {
                                               EmployeeId = e.Id,
                                               ShiftBlockId = e.ShiftBlockId,
                                               ShiftType = shift != null ? shift.ShiftType : "Single",
                                               ShiftName = shift != null ? shift.ShiftName : "Default",
                                               DefaultInTime = shift != null ? shift.DefaultInTime : TimeSpan.FromHours(9),
                                               DefaultOutTime = shift != null ? shift.DefaultOutTime : TimeSpan.FromHours(17)
                                           }).FirstOrDefaultAsync();

                if (employeeShift == null)
                    return NotFound(new { success = false, message = "Employee not found" });

                return Ok(new { success = true, data = employeeShift });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("shift-blocks")]
        public async Task<IActionResult> GetShiftBlocks([FromQuery] int companyId)
        {
            try
            {
                var shiftBlocks = await _context.ShiftBlocks
                    .Where(sb => sb.CompanyId == companyId && sb.IsActive)
                    .Select(sb => new
                    {
                        sb.Id,
                        sb.ShiftName,
                        sb.ShiftType,
                        sb.DefaultInTime,
                        sb.DefaultOutTime,
                        sb.FirstShiftInTime,
                        sb.FirstShiftOutTime,
                        sb.SecondShiftInTime,
                        sb.SecondShiftOutTime,
                        sb.DayType2
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = shiftBlocks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("current-period")]
        public async Task<IActionResult> GetCurrentSalaryPeriod()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sql = @"
                    SELECT TOP 1 
                        id as Id, company_id as CompanyId, year as Year, month as Month,
                        payrollStartDate as PayrollStartDate, processing as Processing, locked as Locked
                    FROM Payroll_Process 
                    WHERE locked = 0 
                    ORDER BY payrollStartDate";

                var period = await connection.QueryFirstOrDefaultAsync<PayrollPeriod>(sql);
                if (period == null)
                    return NotFound(new { success = false, message = "No active salary period found" });

                return Ok(new { success = true, data = period });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("employee-salary-start-date/{employeeId}")]
        public async Task<IActionResult> GetEmployeeSalaryStartDate(int employeeId)
        {
            try
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId);
                if (employee == null)
                    return NotFound(new { success = false, message = "Employee not found" });

                int salaryStartDay = 1;
                return Ok(new { success = true, data = salaryStartDay });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // GET TIMESHEET  —  FIXED (loads NightDates + Calendar)
        // ============================================================
        // ============================================================
// GET /api/attendance/timesheet
//   Either year+monthName  OR  fromDate+toDate (overrides)
// ============================================================
// ============================================================
// GET /api/attendance/timesheet
//   Either year+monthName  OR  fromDate+toDate (overrides)
// ============================================================
// ============================================================
// GET /api/attendance/timesheet
//   Mode 1: year+monthName
//   Mode 2: fromYear+fromMonth+toYear+toMonth (period range)
// ============================================================
[HttpGet("timesheet")]
public async Task<IActionResult> GetTimesheet(
    [FromQuery] int employeeId,
    [FromQuery] string? year,
    [FromQuery] string? monthName,
    [FromQuery] int? fromYear,
    [FromQuery] string? fromMonth,
    [FromQuery] int? toYear,
    [FromQuery] string? toMonth)
{
    try
    {
        // ── 1. Employee ──
        var employee = await _context.Employees
            .Join(_context.Company_Departments,
                  e => e.DepartmentId,
                  d => d.Id,
                  (e, d) => new { Employee = e, Department = d })
            .Where(x => x.Employee.Id == employeeId)
            .Select(x => new
            {
                Employee       = x.Employee,
                DepartmentName = x.Department.DepartmentName,
                DepartmentId   = x.Department.Id
            })
            .FirstOrDefaultAsync();

        if (employee == null)
            return NotFound(new { success = false, message = "Employee not found" });

        var companySettings = await _companySettingsService.GetCompanySettingsAsync(employee.Employee.CompanyId);
        if (companySettings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        int salaryStartDay = !string.IsNullOrEmpty(employee.DepartmentName) &&
            employee.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase)
            ? companySettings.SalaryStartDate_Office
            : companySettings.SalaryStartDate_Wages;

        int targetYear  = string.IsNullOrEmpty(year) ? DateTime.Now.Year : int.Parse(year);
        int targetMonth = GetMonthNumberFromName(monthName) ?? DateTime.Now.Month;
        if (targetMonth == 0)
            return BadRequest(new { success = false, message = "Invalid month name provided" });

        // ── 2. Base range from single period ──
        DateTime startDate, endDate;
        if (salaryStartDay == 1)
        {
            startDate = new DateTime(targetYear, targetMonth, 1);
            endDate   = new DateTime(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
        }
        else
        {
            startDate = new DateTime(targetYear, targetMonth, salaryStartDay);
            var nextMonth = startDate.AddMonths(1);
            endDate = new DateTime(nextMonth.Year, nextMonth.Month, salaryStartDay).AddDays(-1);
        }

        // ── 2b. Period-range override ──
        if (fromYear.HasValue && toYear.HasValue
            && !string.IsNullOrWhiteSpace(fromMonth) && !string.IsNullOrWhiteSpace(toMonth))
        {
            int fromMonthNum = GetMonthNumber(fromMonth);
            int toMonthNum   = GetMonthNumber(toMonth);

            if (fromMonthNum > 0 && toMonthNum > 0)
            {
                // Start of FROM period
                if (salaryStartDay == 1)
                    startDate = new DateTime(fromYear.Value, fromMonthNum, 1);
                else
                    startDate = new DateTime(fromYear.Value, fromMonthNum, salaryStartDay);

                // End of TO period
                if (salaryStartDay == 1)
                {
                    endDate = new DateTime(toYear.Value, toMonthNum,
                        DateTime.DaysInMonth(toYear.Value, toMonthNum));
                }
                else
                {
                    var toPeriodStart = new DateTime(toYear.Value, toMonthNum, salaryStartDay);
                    var afterToMonth  = toPeriodStart.AddMonths(1);
                    endDate = new DateTime(afterToMonth.Year, afterToMonth.Month, salaryStartDay).AddDays(-1);
                }
            }
        }

        // ── 3. Load data ──
        var timesheetData = new List<object>();
        var currentDate = startDate;

        var existingTimesheets = await _context.TimeSheets
            .Where(t => t.EmployeeId == employeeId &&
                ((t.FirstInDate  >= startDate && t.FirstInDate  <= endDate) ||
                 (t.SecondInDate >= startDate && t.SecondInDate <= endDate)))
            .ToListAsync();

        var savedShiftBlocks = await _context.ShiftsTimesheet
            .Where(s => s.EmployeeId == employeeId && s.Date >= startDate && s.Date <= endDate)
            .ToListAsync();

        var shiftBlockDict = await _context.ShiftBlocks
            .Where(sb => sb.CompanyId == employee.Employee.CompanyId)
            .ToDictionaryAsync(sb => sb.Id, sb => sb.DayType2);

        var shiftBlockDictFull = await _context.ShiftBlocks
            .Where(sb => sb.CompanyId == employee.Employee.CompanyId)
            .ToDictionaryAsync(sb => sb.Id);

        var leaveRecords = await _context.Leave
            .Where(l => l.EmployeeId == employeeId &&
                        l.LeaveDate >= startDate && l.LeaveDate <= endDate &&
                        l.Status == "Approved")
            .Select(l => new { l.LeaveDate, l.LeaveType, l.IsHalfDay, l.Status })
            .ToListAsync();

        var otherLeaves = await _context.OtherLeaves
            .Where(ol => ol.EmployeeId == employeeId &&
                         ol.FromDate <= endDate && ol.ToDate >= startDate &&
                         (ol.Status == "Approved" || ol.Status == "Pending"))
            .ToListAsync();

        var employeeShift = await (from e in _context.Employees
                                   join sb in _context.ShiftBlocks on e.ShiftBlockId equals sb.Id into sj
                                   from shift in sj.DefaultIfEmpty()
                                   where e.Id == employeeId
                                   select new
                                   {
                                       ShiftBlockId = e.ShiftBlockId,
                                       ShiftType    = shift != null ? shift.ShiftType : "Single"
                                   }).FirstOrDefaultAsync();

        string employeeShiftType = employeeShift?.ShiftType ?? "Single";

        var nightDates = await _context.NightDates
            .Where(n => n.EmployeeId == employeeId &&
                        n.Date >= startDate && n.Date <= endDate &&
                        n.IsFullNight)
            .Select(n => n.Date)
            .ToListAsync();
        var nightDateSet = new HashSet<DateTime>(nightDates.Select(d => d.Date));

        var calendarRows = await _context.Calendar
            .Where(c => c.CompanyId == employee.Employee.CompanyId &&
                        c.Date >= startDate && c.Date <= endDate)
            .ToListAsync();
        var calendarDict = calendarRows
            .GroupBy(c => c.Date.Date)
            .ToDictionary(g => g.Key, g => g.First());

        // ── 4. Loop day by day ──
        while (currentDate <= endDate)
        {
            var existingTimesheet = existingTimesheets
                .FirstOrDefault(t =>
                    (t.FirstInDate.HasValue  && t.FirstInDate.Value.Date  == currentDate.Date) ||
                    (t.SecondInDate.HasValue && t.SecondInDate.Value.Date == currentDate.Date));

            var savedShiftBlock = existingTimesheet == null
                ? savedShiftBlocks.FirstOrDefault(s => s.Date.Date == currentDate.Date)
                : null;

            int? activeShiftBlockId =
                existingTimesheet?.FirstShiftBlockId
                ?? savedShiftBlock?.ShiftBlockIdFirstShift
                ?? employeeShift?.ShiftBlockId;

            string? activeDayType2 = activeShiftBlockId.HasValue &&
                shiftBlockDict.TryGetValue(activeShiftBlockId.Value, out var dt) ? dt : null;

            calendarDict.TryGetValue(currentDate.Date, out var cal);

            bool isPoya        = cal?.IsPoya        ?? false;
            bool isBankHoliday = cal?.IsBankHoliday ?? false;
            bool isMercantile  = cal?.IsMercantile  ?? false;
            bool isHoliday     = isPoya || isBankHoliday || isMercantile;

            int? shiftBlockIdForWeeklyOff = employeeShift?.ShiftBlockId ?? activeShiftBlockId;
            shiftBlockDictFull.TryGetValue(shiftBlockIdForWeeklyOff ?? 0, out var shiftBlockSettings);

            string? firstWeeklyDayOff     = shiftBlockSettings?.FirstWeeklyDayOffDay;
            string? firstWeeklyHalfDayOff = shiftBlockSettings?.FirstWeeklyHalfDayOffDay;

            bool isWeeklyDayOff =
                !string.IsNullOrEmpty(firstWeeklyDayOff) &&
                currentDate.DayOfWeek.ToString().Equals(firstWeeklyDayOff, StringComparison.OrdinalIgnoreCase);

            bool isWeeklyHalfDayOff =
                !string.IsNullOrEmpty(firstWeeklyHalfDayOff) &&
                currentDate.DayOfWeek.ToString().Equals(firstWeeklyHalfDayOff, StringComparison.OrdinalIgnoreCase);

            bool allowFullDayLeave = !isHoliday && !isWeeklyDayOff && !isWeeklyHalfDayOff;
            bool allowHalfDayLeave = !isHoliday && !isWeeklyDayOff;

            bool isNightFromShift = string.Equals(activeDayType2, "Next day", StringComparison.OrdinalIgnoreCase);
            bool isNightFromTable = nightDateSet.Contains(currentDate.Date);
            bool hasFullNight     = isNightFromShift || isNightFromTable;

            var defaultOutDate = hasFullNight ? currentDate.AddDays(1) : currentDate;
            bool isHalfDayOfWeek = isWeeklyHalfDayOff;

            var dayLeaves = leaveRecords
                .Where(l => l.LeaveDate.Date == currentDate.Date)
                .ToList();

            var isBlockedByOtherLeave = otherLeaves
                .Any(ol => currentDate >= ol.FromDate && currentDate <= ol.ToDate);

            var hasAnnualLeave     = dayLeaves.Any(l => l.LeaveType == "Annual" && !l.IsHalfDay);
            var hasAnnualHalfLeave = dayLeaves.Any(l => l.LeaveType == "Annual" &&  l.IsHalfDay);
            var hasCasualLeave     = dayLeaves.Any(l => l.LeaveType == "Casual" && !l.IsHalfDay);
            var hasCasualHalfLeave = dayLeaves.Any(l => l.LeaveType == "Casual" &&  l.IsHalfDay);
            var hasSickLeave       = dayLeaves.Any(l => l.LeaveType == "Sick"   && !l.IsHalfDay);
            var hasSickHalfLeave   = dayLeaves.Any(l => l.LeaveType == "Sick"   &&  l.IsHalfDay);

            var otherLeaveType = "-";

            if (hasFullNight)
            {
                hasAnnualHalfLeave = false;
                hasCasualHalfLeave = false;
                hasSickHalfLeave   = false;
            }

            if (isBlockedByOtherLeave)
            {
                var blockingLeave = otherLeaves.FirstOrDefault(ol =>
                    currentDate >= ol.FromDate && currentDate <= ol.ToDate);
                otherLeaveType = blockingLeave?.LeaveType ?? "Blocked";
            }

            // Safe-sum (null → 0) — return type is TimeSpan (not TimeSpan?)
            TimeSpan nz(TimeSpan? t) => t ?? TimeSpan.Zero;

            TimeSpan otDoubleCombined =
                nz(existingTimesheet?.OtHoursPoya) +
                nz(existingTimesheet?.OtHoursBankHoliday) +
                nz(existingTimesheet?.OtHoursSunday) +
                nz(existingTimesheet?.OtHoursWeeklyDayOff) +
                nz(existingTimesheet?.OtHoursWeeklyHalfDayOff);

            var dayData = new
            {
                Date    = currentDate,
                DayName = currentDate.DayOfWeek.ToString(),

                TimesheetRecord = existingTimesheet != null
                    ? new
                    {
                        ShiftBlockId  = existingTimesheet.FirstShiftBlockId,
                        InTime        = existingTimesheet.FirstInTime,
                        OutDate       = (DateTime?)(existingTimesheet.FirstOutDate ?? defaultOutDate),
                        OutTime       = existingTimesheet.FirstOutTime,
                        DayType       = existingTimesheet.DayType,
                        WorkHours     = existingTimesheet.WorkHours,

                        FirstShiftBlockId  = existingTimesheet.FirstShiftBlockId,
                        FirstInTime        = existingTimesheet.FirstInTime,
                        FirstOutDate       = (DateTime?)(existingTimesheet.FirstOutDate ?? defaultOutDate),
                        FirstOutTime       = existingTimesheet.FirstOutTime,
                        SecondShiftBlockId = existingTimesheet.SecondShiftBlockId,
                        SecondInTime       = existingTimesheet.SecondInTime,
                        SecondOutDate      = (DateTime?)(existingTimesheet.SecondOutDate ?? defaultOutDate),
                        SecondOutTime      = existingTimesheet.SecondOutTime,

                        LateHours               = existingTimesheet.LateHours,
                        OtHoursNormal           = existingTimesheet.OtHoursNormal,
                        OtHoursDouble           = (TimeSpan?)otDoubleCombined,
                        OtHoursWeeklyDayOff     = (TimeSpan?)existingTimesheet.OtHoursWeeklyDayOff,
                        OtHoursWeeklyHalfDayOff = (TimeSpan?)existingTimesheet.OtHoursWeeklyHalfDayOff,
                        OtHoursPoya             = (TimeSpan?)existingTimesheet.OtHoursPoya,
                        OtHoursBankHoliday      = (TimeSpan?)existingTimesheet.OtHoursBankHoliday,
                        OtHoursSunday           = (TimeSpan?)existingTimesheet.OtHoursSunday,
                        PayType                 = (string?)existingTimesheet.PayType,
                        FromShiftBlock          = false
                    }
                    : savedShiftBlock != null
                        ? new
                        {
                            ShiftBlockId  = savedShiftBlock.ShiftBlockIdFirstShift,
                            InTime        = (TimeSpan?)null,
                            OutDate       = (DateTime?)defaultOutDate,
                            OutTime       = (TimeSpan?)null,
                            DayType       = "Normal",
                            WorkHours     = (TimeSpan?)null,

                            FirstShiftBlockId  = savedShiftBlock.ShiftBlockIdFirstShift,
                            FirstInTime        = (TimeSpan?)null,
                            FirstOutDate       = (DateTime?)defaultOutDate,
                            FirstOutTime       = (TimeSpan?)null,
                            SecondShiftBlockId = savedShiftBlock.ShiftBlockIdSecondShift,
                            SecondInTime       = (TimeSpan?)null,
                            SecondOutDate      = (DateTime?)defaultOutDate,
                            SecondOutTime      = (TimeSpan?)null,

                            LateHours               = (TimeSpan?)null,
                            OtHoursNormal           = (TimeSpan?)null,
                            OtHoursDouble           = (TimeSpan?)otDoubleCombined,
                            OtHoursWeeklyDayOff     = (TimeSpan?)null,
                            OtHoursWeeklyHalfDayOff = (TimeSpan?)null,
                            OtHoursPoya             = (TimeSpan?)null,
                            OtHoursBankHoliday      = (TimeSpan?)null,
                            OtHoursSunday           = (TimeSpan?)null,
                            PayType                 = (string?)null,
                            FromShiftBlock          = true
                        }
                        : null,

                ActiveLeave    = dayLeaves,
                IsBlocked      = isBlockedByOtherLeave,
                OtherLeaveType = otherLeaveType,

                HasFullNight       = hasFullNight,
                NightLockedByShift = isNightFromShift,

                IsPoya           = isPoya,
                IsBankHoliday    = isBankHoliday,
                IsMercantile     = isMercantile,
                IsHoliday        = isHoliday,
                IsHalfDayOfWeek  = isHalfDayOfWeek,

                IsWeeklyDayOff     = isWeeklyDayOff,
                IsWeeklyHalfDayOff = isWeeklyHalfDayOff,

                AllowFullDayLeave  = allowFullDayLeave,
                AllowHalfDayLeave  = allowHalfDayLeave,

                HasAnnualLeave     = hasAnnualLeave,
                HasAnnualHalfLeave = hasAnnualHalfLeave,
                HasCasualLeave     = hasCasualLeave,
                HasCasualHalfLeave = hasCasualHalfLeave,
                HasSickLeave       = hasSickLeave,
                HasSickHalfLeave   = hasSickHalfLeave,

                WorkHours  = existingTimesheet?.WorkHours?.ToString(@"hh\:mm")     ?? "00:00",
                LateHours  = existingTimesheet?.LateHours?.ToString(@"hh\:mm")     ?? "00:00",
                OtNormal   = existingTimesheet?.OtHoursNormal?.ToString(@"hh\:mm") ?? "00:00",
                OtDouble   = otDoubleCombined.ToString(@"hh\:mm"),

                IsWeekend          = currentDate.DayOfWeek == DayOfWeek.Saturday ||
                                     currentDate.DayOfWeek == DayOfWeek.Sunday,
                SalaryStartDayUsed = salaryStartDay
            };

            timesheetData.Add(dayData);
            currentDate = currentDate.AddDays(1);
        }

        // ── 5. Return ──
        return Ok(new
        {
            success = true,
            data = new
            {
                Days      = timesheetData,
                StartDate = startDate,
                EndDate   = endDate,
                EmployeeShiftType = employeeShiftType,
                PeriodInfo = new
                {
                    Year        = targetYear,
                    Month       = targetMonth,
                    MonthName   = GetMonthNameFromNumber(targetMonth),
                    SalaryStartDay = salaryStartDay,
                    DepartmentId   = employee.DepartmentId,
                    DepartmentName = employee.DepartmentName ?? "No Department",
                    DepartmentType = !string.IsNullOrEmpty(employee.DepartmentName) &&
                        employee.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase)
                        ? "Office" : "Wages",
                    SalaryStartDate_Office = companySettings.SalaryStartDate_Office,
                    SalaryStartDate_Wages  = companySettings.SalaryStartDate_Wages
                }
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error getting timesheet for employee {EmployeeId}", employeeId);
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// POST /api/attendance/report/leave/run
//   Runs the whole Leave report server-side.
//   One HTTP round-trip, regardless of how many employees/periods.
// ============================================================
[HttpPost("report/leave/run")]
public async Task<IActionResult> RunLeaveReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        // ── 1. Load employees matching the scope ──
        var empQuery = _context.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == req.CompanyId && e.IsActive == true);

        if (req.Scope == "selected")
        {
            if (!req.EmployeeId.HasValue)
                return BadRequest(new { success = false, message = "EmployeeId required for selected scope" });
            empQuery = empQuery.Where(e => e.Id == req.EmployeeId.Value);
        }
        else if (req.Scope == "lines")
        {
            if (!req.DepartmentId.HasValue)
                return BadRequest(new { success = false, message = "DepartmentId required for lines scope" });
            empQuery = empQuery.Where(e => e.DepartmentId == req.DepartmentId.Value);
        }

        var employees = await empQuery
    .Join(_context.Company_Departments,
          e => e.DepartmentId, d => d.Id,
          (e, d) => new
          {
              e.Id,
              e.EPFNo,
              e.FirstName,
              e.LastName,
              e.CompanyId,
              DepartmentName = d.DepartmentName
          })
    .OrderBy(x => x.EPFNo)
    .ToListAsync();
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = req.PeriodMode, periods = new List<LeavePeriodBlock>() } });

        var employeeIds = employees.Select(e => e.Id).ToList();
        var empById     = employees.ToDictionary(e => e.Id);

        // ── 2. Company settings (needed for salary-period start day) ──
        var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
        if (settings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        using var conn = new SqlConnection(_connectionString);

        // ════════════════════════════════════════════════════════════════
        //  RANGE MODE
        // ════════════════════════════════════════════════════════════════
        if (string.Equals(req.PeriodMode, "range", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(req.FromDate, out var from) || !DateTime.TryParse(req.ToDate, out var to))
                return BadRequest(new { success = false, message = "Invalid From/To date" });

            if (from > to)
                return BadRequest(new { success = false, message = "From date must be before To date" });

            var toEnd = to.Date.AddDays(1).AddSeconds(-1);

            // One aggregate query for the whole date range, all employees
            const string sql = @"
                SELECT EmployeeId, LeaveDate, LeaveType, IsHalfDay
                FROM [Leave]
                WHERE EmployeeId IN @Ids
                  AND LeaveDate >= @From
                  AND LeaveDate <= @To
                  AND Status = 'Approved'
                ORDER BY EmployeeId, LeaveDate";

            var rows = await conn.QueryAsync<dynamic>(sql,
                new { Ids = employeeIds, From = from.Date, To = toEnd });

            // Group by employee, then per-day matrix
            var rangeBlocks = new List<LeaveRangeEmployeeBlock>();

            foreach (var emp in employees)
            {
                var empRows = rows.Where(r => (int)r.EmployeeId == emp.Id).ToList();
                if (!empRows.Any()) continue;

                var dayMap = new Dictionary<string, LeaveDayRow>();

                foreach (var r in empRows)
                {
                    var d        = ((DateTime)r.LeaveDate).Date;
                    var key      = d.ToString("yyyy-MM-dd");
                    var type     = ((string?)r.LeaveType ?? "").Trim().ToLowerInvariant();
                    var isHalf   = (bool?)r.IsHalfDay ?? false;
                    var add      = isHalf ? 0.5 : 1.0;

                    if (!dayMap.TryGetValue(key, out var day))
                    {
                        day = new LeaveDayRow { Date = key };
                        dayMap[key] = day;
                    }

                    switch (type)
                    {
                        case "annual":
                            if (isHalf) day.AnnualHalf += add; else day.Annual += add;
                            break;
                        case "casual":
                            if (isHalf) day.CasualHalf += add; else day.Casual += add;
                            break;
                        case "sick":
                            if (isHalf) day.SickHalf += add; else day.Sick += add;
                            break;
                        default:
                            day.Other += add;
                            if (string.IsNullOrEmpty(day.OtherType))
                                day.OtherType = (string?)r.LeaveType ?? "Other";
                            break;
                    }
                }

                rangeBlocks.Add(new LeaveRangeEmployeeBlock
                {
                    EmployeeId     = emp.Id,
                    EpfNo          = emp.EPFNo,
                    EmployeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    DepartmentName = emp.DepartmentName ?? "",
                    Days           = dayMap.Values.OrderBy(d => d.Date).ToList()
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mode = "range",
                    from = from.ToString("yyyy-MM-dd"),
                    to   = to.ToString("yyyy-MM-dd"),
                    employees = rangeBlocks
                }
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  SUMMARY MODE
        // ════════════════════════════════════════════════════════════════
        if (!req.FromYear.HasValue || string.IsNullOrWhiteSpace(req.FromMonthName)
            || !req.ToYear.HasValue || string.IsNullOrWhiteSpace(req.ToMonthName))
            return BadRequest(new { success = false, message = "From/To year+month required for summary mode" });

        int fromMon = GetMonthNumber(req.FromMonthName);
        int toMon   = GetMonthNumber(req.ToMonthName);
        if (fromMon == 0 || toMon == 0)
            return BadRequest(new { success = false, message = "Invalid month name" });

        // Build the month list
        var months = new List<(int year, int month, string monthName)>();
        int curY = req.FromYear.Value;
        int curM = fromMon;
        int endY = req.ToYear.Value;
        int endM = toMon;

        if (curY * 12 + curM > endY * 12 + endM)
            return BadRequest(new { success = false, message = "From must be <= To" });

        while (curY < endY || (curY == endY && curM <= endM))
        {
            months.Add((curY, curM, GetMonthNameFromNumber(curM)));
            curM++; if (curM > 12) { curM = 1; curY++; }
        }

        // Fetch payroll status for ALL months in a single SQL query
        var monthKeys = months.Select(m => m.monthName).ToList();

        const string statusSql = @"
            SELECT year, month, [processing] AS Processing, [locked] AS Locked
            FROM Payroll_Process
            WHERE company_id = @CompanyId
              AND year = @Year
              AND month IN @Months";

        // Since year might span multiple years, loop distinct years (usually 1)
        var statusMap = new Dictionary<string, (bool exists, bool locked, bool processing)>();
        foreach (var yearGrp in months.GroupBy(m => m.year))
        {
            var rs = await conn.QueryAsync<dynamic>(statusSql, new
            {
                CompanyId = req.CompanyId,
                Year      = yearGrp.Key,
                Months    = yearGrp.Select(m => m.monthName).ToList()
            });

            foreach (var r in rs)
            {
                var k = $"{yearGrp.Key}-{r.month}";
                statusMap[k] = (true, (bool)(r.Locked ?? false), (bool)(r.Processing ?? false));
            }
        }

        // Fetch employee → start day (Office vs Wages)
        int startDayWages  = settings.SalaryStartDate_Wages;
        int startDayOffice = settings.SalaryStartDate_Office;

        // Aggregate SQL per period — grouped buckets per (employee, leave type)
        const string bucketSql = @"
            SELECT
                EmployeeId,
                ISNULL(LeaveType, 'Unknown') AS LeaveType,
                SUM(CASE WHEN IsHalfDay = 1 THEN 0.5 ELSE 1.0 END) AS Days
            FROM [Leave]
            WHERE EmployeeId IN @Ids
              AND LeaveDate >= @From
              AND LeaveDate <= @To
              AND Status = 'Approved'
            GROUP BY EmployeeId, LeaveType";

        var periodBlocks = new List<LeavePeriodBlock>();

        foreach (var (year, month, monthName) in months)
        {
            var sk = $"{year}-{monthName}";
            bool exists     = statusMap.TryGetValue(sk, out var st) && st.exists;
            bool locked     = exists && st.locked;
            bool processing = exists && st.processing;

            if (!exists) continue;      // skip periods with no payroll record

            // Salary period date range — may differ per employee (office vs wages)
            // Do two aggregate queries: one for Office emp, one for Wages emp
            var officeEmps = employees
                .Where(e => string.Equals(e.DepartmentName, "Office", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Id).ToList();
            var wagesEmps = employees
                .Where(e => !string.Equals(e.DepartmentName, "Office", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Id).ToList();

            var bucketMap = new Dictionary<int, Dictionary<string, double>>();

            async Task FetchForGroupAsync(List<int> ids, int startDay)
            {
                if (!ids.Any()) return;
                var (fromDate, toDate) = GetSalaryPeriodRange(year, month, startDay);

                var rs = await conn.QueryAsync<dynamic>(bucketSql, new
                {
                    Ids = ids,
                    From = fromDate,
                    To   = toDate
                });

                foreach (var r in rs)
                {
                    int empId    = (int)r.EmployeeId;
                    string type  = (string)r.LeaveType;
                    double days  = (double)(decimal)r.Days;

                    if (!bucketMap.TryGetValue(empId, out var dict))
                    {
                        dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        bucketMap[empId] = dict;
                    }

                    if (dict.ContainsKey(type)) dict[type] += days;
                    else                        dict[type]  = days;
                }
            }

            await FetchForGroupAsync(officeEmps, startDayOffice);
            await FetchForGroupAsync(wagesEmps,  startDayWages);

            // Build per-employee rows
            var empRows = new List<LeavePeriodEmployeeRow>();

            foreach (var emp in employees)
            {
                var dict = bucketMap.TryGetValue(emp.Id, out var d)
                    ? d
                    : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                var buckets = new LeaveBuckets();

                foreach (var kv in dict)
                {
                    var cat      = kv.Key;
                    double days  = kv.Value;
                    bool isHalf =
    cat.EndsWith("1/2", StringComparison.OrdinalIgnoreCase) ||
    cat.EndsWith("½",   StringComparison.OrdinalIgnoreCase) ||
    cat.IndexOf("(half)", StringComparison.OrdinalIgnoreCase) >= 0 ||
    cat.EndsWith("half", StringComparison.OrdinalIgnoreCase) ||
    (Math.Abs(days * 2 - Math.Round(days * 2)) < 1e-9 && (days % 1) != 0);

                    var baseName = System.Text.RegularExpressions.Regex
                        .Replace(cat, @"\s*(1/2|½|\(half\)|half)$", "",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                        .Trim();

                   LeaveBucket target;
if      (baseName.StartsWith("annual", StringComparison.OrdinalIgnoreCase)) target = buckets.Annual;
else if (baseName.StartsWith("casual", StringComparison.OrdinalIgnoreCase)) target = buckets.Casual;
else if (baseName.StartsWith("sick",   StringComparison.OrdinalIgnoreCase)) target = buckets.Sick;
else                                                                        target = buckets.Other;

                    if (isHalf) target.Half += days;
                    else        target.Full += days;
                }

                double total =
                    buckets.Annual.Full + buckets.Annual.Half +
                    buckets.Casual.Full + buckets.Casual.Half +
                    buckets.Sick.Full   + buckets.Sick.Half   +
                    buckets.Other.Full;

                if (total <= 0) continue;   // skip employees with zero in this period

                empRows.Add(new LeavePeriodEmployeeRow
                {
                    EmployeeId     = emp.Id,
                    EpfNo          = emp.EPFNo,
                    EmployeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    DepartmentName = emp.DepartmentName ?? "",
                    Buckets        = buckets,
                    Total          = Math.Round(total, 2)
                });
            }

            if (!empRows.Any()) continue;

            periodBlocks.Add(new LeavePeriodBlock
            {
                Year      = year,
                MonthName = monthName,
                Locked    = locked,
                Processing = processing,
                Employees = empRows.OrderBy(e => e.EpfNo).ToList()
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                mode = "summary",
                periods = periodBlocks
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunLeaveReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// Add once, inside AttendanceController (e.g. next to EmployeeRow)
private class LeaveRow
{
    public int EmployeeId { get; set; }
    public DateTime LeaveDate { get; set; }
    public string LeaveType { get; set; } = "";
    public bool IsHalfDay { get; set; }
}
[HttpPost("report/nopay/run")]
public async Task<IActionResult> RunNoPayReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = req.PeriodMode, employees = new List<object>(), periods = new List<object>() } });

        var ids = employees.Select(e => e.Id).ToList();

        using var conn = new SqlConnection(_connectionString);

        // ── RANGE (unchanged — already a single aggregate) ──
        if (string.Equals(req.PeriodMode, "range", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(req.FromDate, out var from) || !DateTime.TryParse(req.ToDate, out var to))
                return BadRequest(new { success = false, message = "Invalid From/To date" });
            if (from > to)
                return BadRequest(new { success = false, message = "From must be before To" });

            var toEnd = to.Date.AddDays(1).AddSeconds(-1);

            const string sql = @"
                SELECT EmployeeId, FirstInDate AS [Date], DayType
                FROM TimeSheet
                WHERE EmployeeId IN @Ids
                  AND FirstInDate >= @From
                  AND FirstInDate <= @To
                  AND DayType IN ('No Pay', 'No Pay - Half')
                ORDER BY EmployeeId, FirstInDate";

            var rows = await conn.QueryAsync<dynamic>(sql,
                new { Ids = ids, From = from.Date, To = toEnd });

            var result = new List<object>();
            foreach (var emp in employees)
            {
                var empRows = rows.Where(r => (int)r.EmployeeId == emp.Id).ToList();
                if (!empRows.Any()) continue;

                result.Add(new
                {
                    employeeId     = emp.Id,
                    employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo          = emp.EPFNo,
                    departmentName = emp.DepartmentName ?? "",
                    rows = empRows.Select(r => new
                    {
                        date    = (DateTime)r.Date,
                        dayType = (string?)r.DayType ?? ""
                    }).ToList()
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mode = "range",
                    from = from.ToString("yyyy-MM-dd"),
                    to   = to.ToString("yyyy-MM-dd"),
                    employees = result
                }
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  SUMMARY — batched by start-day group (Office / Wages), like Leave
        // ════════════════════════════════════════════════════════════════
        // ── SUMMARY ──
var periods = BuildPeriodList(req);

var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
if (settings == null)
    return BadRequest(new { success = false, message = "Company settings not found" });

int startDayWages  = settings.SalaryStartDate_Wages;
int startDayOffice = settings.SalaryStartDate_Office;

// Split employees ONCE by start-day — NOT per period
var officeEmps = employees
    .Where(e => !string.IsNullOrEmpty(e.DepartmentName) &&
                e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
    .Select(e => e.Id).ToList();
var wagesEmps = employees
    .Where(e => string.IsNullOrEmpty(e.DepartmentName) ||
                !e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
    .Select(e => e.Id).ToList();

// One aggregate per start-day group — reused across every period
const string bucketSql = @"
    SELECT
        EmployeeId,
        ISNULL(SUM(
            CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
        ), 0) AS TotalHours
    FROM TimeSheet
    WHERE EmployeeId IN @Ids
      AND FirstInDate >= @From
      AND FirstInDate <= @To
      AND LateHours IS NOT NULL
    GROUP BY EmployeeId";

var periodBlocks = new List<object>();

foreach (var (year, month, monthName) in periods)
{
    var state = await GetPayrollPeriodStateAsync(conn, req.CompanyId, year, monthName);
    if (!state.exists) continue;

    // empId → total late hours for this period
    var totals = new Dictionary<int, decimal>();

    async Task FetchForGroupAsync(List<int> groupIds, int startDay)
    {
        if (!groupIds.Any()) return;

        var (fromDate, toDate) = GetSalaryPeriodRange(year, month, startDay);

        var rs = await conn.QueryAsync<dynamic>(bucketSql, new
        {
            Ids  = groupIds,
            From = fromDate,
            To   = toDate
        });

        foreach (var r in rs)
        {
            totals[(int)r.EmployeeId] = (decimal)r.TotalHours;
        }
    }

    await FetchForGroupAsync(officeEmps, startDayOffice);   // 1 query
    await FetchForGroupAsync(wagesEmps,  startDayWages);    // 1 query

    // Shape response — only employees with late hours > 0
    var empRows = new List<object>();

    foreach (var emp in employees)
    {
        if (!totals.TryGetValue(emp.Id, out decimal totalHours)) continue;
        if (totalHours <= 0) continue;

        empRows.Add(new
        {
            employeeId     = emp.Id,
            employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
            epfNo          = emp.EPFNo,
            departmentName = emp.DepartmentName ?? "",
            totalHours     = Math.Round(totalHours, 2)
        });
    }

    if (!empRows.Any()) continue;

    periodBlocks.Add(new
    {
        year,
        monthName,
        locked     = state.locked,
        processing = state.processing,
        employees  = empRows
    });
}

return Ok(new
{
    success = true,
    data = new { mode = "summary", periods = periodBlocks }
});
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunNoPayReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
[HttpPost("report/timesheet/run")]
public async Task<IActionResult> RunTimesheetReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        // ── 1. Resolve employees by scope ──
        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = "summary", periods = Array.Empty<object>() } });

        // ── 2. Build period list ──
        List<(int Year, int Month, string MonthName)> periodList;
        try { periodList = BuildPeriodList(req); }
        catch (Exception ex) { return BadRequest(new { success = false, message = ex.Message }); }

        // ── 3. Company settings (one lookup) ──
        var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
        if (settings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        int startWages  = settings.SalaryStartDate_Wages;
        int startOffice = settings.SalaryStartDate_Office;

        var employeeIds = employees.Select(e => e.Id).ToList();
        var empById     = employees.ToDictionary(e => e.Id);

        using var conn = new SqlConnection(_connectionString);

        // ── 4. Compute the whole span once ──
        //  (min over Office + Wages start days, max over Office + Wages end days)
        DateTime overallStart = DateTime.MaxValue;
        DateTime overallEnd   = DateTime.MinValue;
        foreach (var (y, m, _) in periodList)
        {
            foreach (var sd in new[] { startWages, startOffice }.Distinct())
            {
                var (s, e) = GetSalaryPeriodRange(y, m, sd);
                if (s < overallStart) overallStart = s;
                if (e > overallEnd)   overallEnd   = e;
            }
        }

        // ── 5. Who has TimeSheet data in that whole span? ──
        //  One SELECT DISTINCT — this is what kills the empty cards.
        var employeesWithData = (await conn.QueryAsync<int>(@"
            SELECT DISTINCT EmployeeId
            FROM TimeSheet
            WHERE EmployeeId IN @Ids
              AND FirstInDate >= @From
              AND FirstInDate <= @To",
            new { Ids = employeeIds, From = overallStart, To = overallEnd }))
            .ToHashSet();

        // If nobody has data, exit early — the frontend already handles empty array
        if (employeesWithData.Count == 0)
            return Ok(new { success = true, data = new { mode = "summary", periods = Array.Empty<object>() } });

        // Trim the employee list to only those with data
        var activeEmployees = employees.Where(e => employeesWithData.Contains(e.Id)).ToList();
        var activeIds       = activeEmployees.Select(e => e.Id).ToList();

        // ── 6. One-shot prefetch for the active employees ──

        // TimeSheets
        var allTimeSheets = (await conn.QueryAsync<TRIVORA_API.Models.TimeSheet>(@"
            SELECT *
            FROM TimeSheet
            WHERE EmployeeId IN @Ids
              AND FirstInDate >= @From
              AND FirstInDate <= @To",
            new { Ids = activeIds, From = overallStart, To = overallEnd })).ToList();

        // Leave rows
        var allLeaves = (await conn.QueryAsync<LeaveRow>(@"
            SELECT EmployeeId, LeaveDate, LeaveType, IsHalfDay
            FROM [Leave]
            WHERE EmployeeId IN @Ids
              AND LeaveDate >= @From
              AND LeaveDate <= @To
              AND Status = 'Approved'",
            new { Ids = activeIds, From = overallStart, To = overallEnd })).ToList();

        // Night dates
        var allNights = (await conn.QueryAsync<(int EmployeeId, DateTime Date)>(@"
            SELECT EmployeeId, [Date]
            FROM NightDates
            WHERE EmployeeId IN @Ids
              AND [Date] >= @From
              AND [Date] <= @To
              AND IsFullNight = 1",
            new { Ids = activeIds, From = overallStart, To = overallEnd })).ToList();

        // Shift rows
        var allShifts = (await conn.QueryAsync<TRIVORA_API.Models.ShiftsTimesheet>(@"
            SELECT *
            FROM ShiftsTimesheet
            WHERE EmployeeId IN @Ids
              AND [Date] >= @From
              AND [Date] <= @To",
            new { Ids = activeIds, From = overallStart, To = overallEnd })).ToList();

        // Shift blocks (per-company, small table)
        var allShiftBlocks = await _context.ShiftBlocks
            .AsNoTracking()
            .Where(sb => sb.CompanyId == req.CompanyId)
            .ToListAsync();

        // Calendar (per-company, small table)
        var allCalendar = await _context.Calendar
            .AsNoTracking()
            .Where(c => c.CompanyId == req.CompanyId &&
                        c.Date >= overallStart && c.Date <= overallEnd)
            .ToListAsync();

        // ── 7. Index for O(1) lookups ──
        var tsByEmp     = allTimeSheets.GroupBy(t => t.EmployeeId)
                                       .ToDictionary(g => g.Key, g => g.ToList());
        var leavesByEmp = allLeaves.GroupBy(l => l.EmployeeId)
                                   .ToDictionary(g => g.Key, g => g.ToList());
        var nightsByEmp = allNights.GroupBy(n => n.EmployeeId)
                                   .ToDictionary(g => g.Key, g => new HashSet<DateTime>(g.Select(x => x.Date.Date)));
        var shiftsByEmp = allShifts.GroupBy(s => s.EmployeeId)
                                   .ToDictionary(g => g.Key, g => g.ToList());

        // ── 8. Loop periods × employees ──
        var periodBlocks = new List<object>();

        foreach (var (year, month, monthName) in periodList)
        {
            var state = await GetPayrollPeriodStateAsync(conn, req.CompanyId, year, monthName);
            if (!state.exists) continue;

            var empRows = new List<object>();

            foreach (var emp in activeEmployees)
            {
                int startDay = (emp.DepartmentName != null &&
                                emp.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
                                ? startOffice : startWages;

                var (startDate, endDate) = GetSalaryPeriodRange(year, month, startDay);

                // ── Filter to this period's rows (in-memory, no DB) ──
                var empTs = tsByEmp.TryGetValue(emp.Id, out var tsAll)
                    ? tsAll.Where(t => t.FirstInDate >= startDate && t.FirstInDate <= endDate).ToList()
                    : new List<TRIVORA_API.Models.TimeSheet>();

                // Per-period data check — skip employees who have data elsewhere but not here
                if (empTs.Count == 0) continue;

                var empLeaves = leavesByEmp.TryGetValue(emp.Id, out var lvAll)
                    ? lvAll.Where(l => l.LeaveDate >= startDate && l.LeaveDate <= endDate).ToList()
                    : new List<LeaveRow>();

                var empNights = nightsByEmp.TryGetValue(emp.Id, out var ntAll)
                    ? ntAll.Where(d => d >= startDate && d <= endDate).ToHashSet()
                    : new HashSet<DateTime>();

                var empShifts = shiftsByEmp.TryGetValue(emp.Id, out var shAll)
                    ? shAll.Where(s => s.Date >= startDate && s.Date <= endDate).ToList()
                    : new List<TRIVORA_API.Models.ShiftsTimesheet>();

                // ── Summary numbers ──
                int totalWorkingDays = empTs.Count(t =>
                    t.FirstInTime.HasValue && t.FirstOutTime.HasValue &&
                    (t.DayType == null || (t.DayType != "No Pay" && t.DayType != "No Pay - Half")));

                decimal totalNoPay = empTs.Sum(t =>
                    t.DayType == "No Pay" ? 1m :
                    t.DayType == "No Pay - Half" ? 0.5m : 0m);

                decimal totalLeaveTaken = empLeaves.Sum(l => l.IsHalfDay ? 0.5m : 1.0m);

                decimal totalPayCutHours = empTs
                    .Where(t => t.LateHours.HasValue)
                    .Sum(t => (decimal)t.LateHours!.Value.TotalHours);

                decimal totalWorkingHours = empTs
                    .Where(t => t.WorkHours.HasValue)
                    .Sum(t => (decimal)t.WorkHours!.Value.TotalHours);

                decimal otNormal  = empTs.Sum(t => (decimal)(t.OtHoursNormal?.TotalHours ?? 0));
                decimal otWOff    = empTs.Sum(t => (decimal)(t.OtHoursWeeklyDayOff?.TotalHours ?? 0));
                decimal otWHalf   = empTs.Sum(t => (decimal)(t.OtHoursWeeklyHalfDayOff?.TotalHours ?? 0));
                decimal otPoya    = empTs.Sum(t => (decimal)(t.OtHoursPoya?.TotalHours ?? 0));
                decimal otBank    = empTs.Sum(t => (decimal)(t.OtHoursBankHoliday?.TotalHours ?? 0));

                int totalFullNight = empNights.Count;

                var summary = new
                {
                    periodKey               = $"{year}-{monthName}",
                    startDate,
                    endDate,
                    totalWorkingDays,
                    totalNoPayCount         = Math.Round(totalNoPay, 2),
                    totalLeaveTaken         = Math.Round(totalLeaveTaken, 2),
                    totalPayCutHours        = Math.Round(totalPayCutHours, 2),
                    totalWorkingHours       = Math.Round(totalWorkingHours, 2),
                    totalOtNormal           = Math.Round(otNormal, 2),
                    totalOtWeeklyDayOff     = Math.Round(otWOff, 2),
                    totalOtWeeklyHalfDayOff = Math.Round(otWHalf, 2),
                    totalOtPoya             = Math.Round(otPoya, 2),
                    totalOtMercantile       = 0m,
                    totalOtBankHoliday      = Math.Round(otBank, 2),
                    totalFullNight
                };

                // ── Day rows ──
                var days = new List<object>();
                var cur  = startDate;
                while (cur <= endDate)
                {
                    var ts = empTs.FirstOrDefault(t =>
                        (t.FirstInDate.HasValue  && t.FirstInDate.Value.Date  == cur.Date) ||
                        (t.SecondInDate.HasValue && t.SecondInDate.Value.Date == cur.Date));

                    // (Optional) match a saved shift for this day, if you need it downstream
                    var shift = empShifts.FirstOrDefault(s => s.Date.Date == cur.Date);

                    days.Add(new
                    {
                        date      = cur,
                        dayName   = cur.DayOfWeek.ToString(),
                        workHours = ts?.WorkHours?.ToString(@"hh\:mm")     ?? "00:00",
                        lateHours = ts?.LateHours?.ToString(@"hh\:mm")     ?? "00:00",
                        otNormal  = ts?.OtHoursNormal?.ToString(@"hh\:mm") ?? "00:00",
                        otDouble  = "00:00",
                        timesheetRecord = ts == null ? null : new
                        {
                            dayType           = ts.DayType ?? "Normal",
                            firstInTime       = ts.FirstInTime,
                            firstOutTime      = ts.FirstOutTime,
                            firstOutDate      = ts.FirstOutDate,
                            firstShiftBlockId = ts.FirstShiftBlockId
                        }
                    });

                    cur = cur.AddDays(1);
                }

                empRows.Add(new
                {
                    employeeId     = emp.Id,
                    employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo          = emp.EPFNo,
                    departmentName = emp.DepartmentName ?? "",
                    summary,
                    days
                });
            }

            if (!empRows.Any()) continue;

            periodBlocks.Add(new
            {
                year,
                monthName,
                locked     = state.locked,
                processing = state.processing,
                employees  = empRows
            });
        }

        // ── 9. Return ──
        return Ok(new
        {
            success = true,
            data = new
            {
                mode = "summary",
                periods = periodBlocks
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunTimesheetReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}


[HttpPost("report/late/run")]
public async Task<IActionResult> RunLateReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = req.PeriodMode, employees = new List<object>(), periods = new List<object>() } });

        var ids = employees.Select(e => e.Id).ToList();
        using var conn = new SqlConnection(_connectionString);

        // ═══════════════════════════════════════════════════════════
        //  RANGE MODE
        // ═══════════════════════════════════════════════════════════
        if (string.Equals(req.PeriodMode, "range", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(req.FromDate, out var from) || !DateTime.TryParse(req.ToDate, out var to))
                return BadRequest(new { success = false, message = "Invalid From/To date" });

            var toEnd = to.Date.AddDays(1).AddSeconds(-1);

            // One aggregate — no per-employee loop, no employees without late hours
            const string rangeSql = @"
                SELECT
                    t.EmployeeId,
                    t.FirstInDate AS [Date],
                    t.LateHours
                FROM TimeSheet t
                WHERE t.EmployeeId IN @Ids
                  AND t.FirstInDate >= @From
                  AND t.FirstInDate <= @To
                  AND t.LateHours IS NOT NULL
                  AND t.LateHours > '00:00:00'
                ORDER BY t.EmployeeId, t.FirstInDate";

            var rows = await conn.QueryAsync<dynamic>(rangeSql,
                new { Ids = ids, From = from.Date, To = toEnd });

            // Index for O(1) grouping
            var byEmp = rows.ToLookup(r => (int)r.EmployeeId);

            var result = new List<object>();
            foreach (var emp in employees)
            {
                var empRows = byEmp[emp.Id].ToList();
                if (empRows.Count == 0) continue;   // ← no late hours = skip

                result.Add(new
                {
                    employeeId     = emp.Id,
                    employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo          = emp.EPFNo,
                    departmentName = emp.DepartmentName ?? "",
                    rows = empRows.Select(r =>
                    {
                        var ts = (TimeSpan?)r.LateHours ?? TimeSpan.Zero;
                        return new
                        {
                            date             = (DateTime)r.Date,
                            lateHours        = ts.ToString(@"hh\:mm"),
                            lateHoursDecimal = Math.Round(ts.TotalHours, 4)
                        };
                    }).ToList()
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mode = "range",
                    from = from.ToString("yyyy-MM-dd"),
                    to   = to.ToString("yyyy-MM-dd"),
                    employees = result
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  SUMMARY MODE  — batched, only employees with > 0 late hours
        // ═══════════════════════════════════════════════════════════
        var periods = BuildPeriodList(req);

        var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
        if (settings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        int startDayWages  = settings.SalaryStartDate_Wages;
        int startDayOffice = settings.SalaryStartDate_Office;

        // Split once — NOT per period
        var officeEmps = employees
            .Where(e => !string.IsNullOrEmpty(e.DepartmentName) &&
                        e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id).ToList();
        var wagesEmps = employees
            .Where(e => string.IsNullOrEmpty(e.DepartmentName) ||
                        !e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id).ToList();

        // Per-period aggregate — one query per start-day group
        const string bucketSql = @"
            SELECT
                EmployeeId,
                ISNULL(SUM(
                    CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
                ), 0) AS TotalHours
            FROM TimeSheet
            WHERE EmployeeId IN @Ids
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND LateHours IS NOT NULL
              AND LateHours > '00:00:00'
            GROUP BY EmployeeId
            HAVING ISNULL(SUM(
                CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
            ), 0) > 0";

        var periodBlocks = new List<object>();

        foreach (var (year, month, monthName) in periods)
        {
            var state = await GetPayrollPeriodStateAsync(conn, req.CompanyId, year, monthName);
            if (!state.exists) continue;

            var totals = new Dictionary<int, decimal>();

            async Task FetchForGroupAsync(List<int> groupIds, int startDay)
            {
                if (!groupIds.Any()) return;

                var (fromDate, toDate) = GetSalaryPeriodRange(year, month, startDay);

                var rs = await conn.QueryAsync<dynamic>(bucketSql, new
                {
                    Ids  = groupIds,
                    From = fromDate,
                    To   = toDate
                });

                foreach (var r in rs)
                {
                    totals[(int)r.EmployeeId] = (decimal)r.TotalHours;
                }
            }

            await FetchForGroupAsync(officeEmps, startDayOffice);
            await FetchForGroupAsync(wagesEmps,  startDayWages);

            // Only push employees whose total > 0 AND that came from the aggregate
            var empRows = employees
                .Where(emp => totals.TryGetValue(emp.Id, out var v) && v > 0)
                .Select(emp => (object)new
                {
                    employeeId     = emp.Id,
                    employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo          = emp.EPFNo,
                    departmentName = emp.DepartmentName ?? "",
                    totalHours     = Math.Round(totals[emp.Id], 2)
                })
                .ToList();

            if (!empRows.Any()) continue;

            periodBlocks.Add(new
            {
                year,
                monthName,
                locked     = state.locked,
                processing = state.processing,
                employees  = empRows
            });
        }

        return Ok(new
        {
            success = true,
            data = new { mode = "summary", periods = periodBlocks }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunLateReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// POST /api/attendance/report/combined/run
//   Runs Working Days + No Pay + Late Hours in ONE query.
//   Same shape as other reports, but each employee row carries
//   ALL THREE metrics so the frontend renders one unified card.
// ============================================================
[HttpPost("report/combined/run")]
public async Task<IActionResult> RunCombinedReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new
            {
                success = true,
                data = new
                {
                    mode = req.PeriodMode,
                    employees = new List<object>(),
                    periods = new List<object>()
                }
            });

        var ids = employees.Select(e => e.Id).ToList();
        using var conn = new SqlConnection(_connectionString);

        // ═══════════════════════════════════════════════════════════
        //  RANGE MODE — one query per metric, merged by employee
        // ═══════════════════════════════════════════════════════════
        if (string.Equals(req.PeriodMode, "range", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(req.FromDate, out var from) || !DateTime.TryParse(req.ToDate, out var to))
                return BadRequest(new { success = false, message = "Invalid From/To date" });
            if (from > to)
                return BadRequest(new { success = false, message = "From must be before To" });

            var toEnd = to.Date.AddDays(1).AddSeconds(-1);

            // 1) Working days + NoPay rows in one pass
            const string tsSql = @"
                SELECT
                    EmployeeId,
                    FirstInDate AS [Date],
                    DayType,
                    LateHours
                FROM TimeSheet
                WHERE EmployeeId IN @Ids
                  AND FirstInDate >= @From
                  AND FirstInDate <= @To
                ORDER BY EmployeeId, FirstInDate";

            var tsRows = (await conn.QueryAsync<dynamic>(tsSql,
                new { Ids = ids, From = from.Date, To = toEnd })).ToList();

            var result = new List<object>();

            foreach (var emp in employees)
            {
                var empRows = tsRows.Where(r => (int)r.EmployeeId == emp.Id).ToList();

                // Working days: has FirstInTime, not NoPay
                int workingDays = empRows.Count(r =>
                    (string?)r.DayType == null ||
                    (string?)r.DayType != "No Pay" && (string?)r.DayType != "No Pay - Half");

                // NoPay: count and half-day breakdown
                decimal noPayFull = 0, noPayHalf = 0;
                foreach (var r in empRows)
                {
                    var dt = (string?)r.DayType;
                    if (dt == "No Pay")           noPayFull += 1m;
                    else if (dt == "No Pay - Half") noPayHalf += 0.5m;
                }

                // Late hours: per-day breakdown
                // Late hours: per-day breakdown
var lateRows = empRows
    .Where(r => r.LateHours != null && (TimeSpan)r.LateHours > TimeSpan.Zero)
    .Select(r => new
    {
        date        = (DateTime)r.Date,
        lateHours   = ((TimeSpan)r.LateHours).ToString(@"hh\:mm"),
        lateDecimal = Math.Round((decimal)((TimeSpan)r.LateHours).TotalHours, 4)  // ← cast to decimal
    })
    .ToList();

decimal totalLate = lateRows.Sum(x => x.lateDecimal);   // now decimal + decimal = decimal ✓
                // Skip employees with ZERO across all three metrics
                if (workingDays == 0 && noPayFull + noPayHalf == 0 && totalLate == 0)
                    continue;

                result.Add(new
                {
                    employeeId     = emp.Id,
                    employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo          = emp.EPFNo,
                    departmentName = emp.DepartmentName ?? "",
                    workingDays,
                    noPayFull      = Math.Round(noPayFull, 2),
                    noPayHalf      = Math.Round(noPayHalf, 2),
                    noPayTotal     = Math.Round(noPayFull + noPayHalf, 2),
                    lateTotal      = Math.Round(totalLate, 2),
                    lateRows
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mode = "range",
                    from = from.ToString("yyyy-MM-dd"),
                    to   = to.ToString("yyyy-MM-dd"),
                    employees = result
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  SUMMARY MODE — one aggregate query per start-day group,
        //  all three metrics computed in SQL.
        // ═══════════════════════════════════════════════════════════
        var periods = BuildPeriodList(req);

        var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
        if (settings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        int startDayWages  = settings.SalaryStartDate_Wages;
        int startDayOffice = settings.SalaryStartDate_Office;

        var officeEmps = employees
            .Where(e => !string.IsNullOrEmpty(e.DepartmentName) &&
                        e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id).ToList();
        var wagesEmps = employees
            .Where(e => string.IsNullOrEmpty(e.DepartmentName) ||
                        !e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id).ToList();

        // ★ ONE query returns all three metrics per employee
        const string combinedSql = @"
            SELECT
                EmployeeId,
                SUM(CASE
                    WHEN (DayType IS NULL OR DayType NOT IN ('No Pay','No Pay - Half'))
                     AND FirstInTime IS NOT NULL
                    THEN 1 ELSE 0
                END) AS WorkingDays,
                SUM(CASE WHEN DayType = 'No Pay'        THEN 1.0
                         WHEN DayType = 'No Pay - Half' THEN 0.5
                         ELSE 0 END) AS NoPayTotal,
                SUM(CASE WHEN DayType = 'No Pay'        THEN 1.0 ELSE 0 END) AS NoPayFull,
                SUM(CASE WHEN DayType = 'No Pay - Half' THEN 1.0 ELSE 0 END) AS NoPayHalf,
                ISNULL(SUM(
                    CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
                ), 0) AS LateTotal
            FROM TimeSheet
            WHERE EmployeeId IN @Ids
              AND FirstInDate >= @From
              AND FirstInDate <= @To
            GROUP BY EmployeeId
            HAVING SUM(CASE
                    WHEN (DayType IS NULL OR DayType NOT IN ('No Pay','No Pay - Half'))
                     AND FirstInTime IS NOT NULL
                    THEN 1 ELSE 0
                END) > 0
                OR SUM(CASE WHEN DayType IN ('No Pay','No Pay - Half') THEN 1 ELSE 0 END) > 0
                OR ISNULL(SUM(
                    CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
                ), 0) > 0";

        var periodBlocks = new List<object>();

        foreach (var (year, month, monthName) in periods)
        {
            var state = await GetPayrollPeriodStateAsync(conn, req.CompanyId, year, monthName);
            if (!state.exists) continue;

            // empId → metrics
            var metrics = new Dictionary<int, dynamic>();

            async Task FetchForGroupAsync(List<int> groupIds, int startDay)
            {
                if (!groupIds.Any()) return;

                var (fromDate, toDate) = GetSalaryPeriodRange(year, month, startDay);

                var rs = await conn.QueryAsync<dynamic>(combinedSql, new
                {
                    Ids  = groupIds,
                    From = fromDate,
                    To   = toDate
                });

                foreach (var r in rs)
                {
                    metrics[(int)r.EmployeeId] = r;
                }
            }

            await FetchForGroupAsync(officeEmps, startDayOffice);   // 1 query
            await FetchForGroupAsync(wagesEmps,  startDayWages);    // 1 query

            var empRows = new List<object>();

            foreach (var emp in employees)
{
    if (!metrics.TryGetValue(emp.Id, out var m)) continue;

    // Local helpers — dynamic + null coalesce is ambiguous at compile-time,
    // so normalize via IConvertible which dispatches on the actual boxed type.
    static decimal D(dynamic v) =>
        v == null ? 0m : ((IConvertible)v).ToDecimal(System.Globalization.CultureInfo.InvariantCulture);

    static int I(dynamic v) =>
        v == null ? 0 : ((IConvertible)v).ToInt32(System.Globalization.CultureInfo.InvariantCulture);

    int     wd       = I(m.WorkingDays);
    decimal npTotal  = D(m.NoPayTotal);
    decimal npFull   = D(m.NoPayFull);
    decimal npHalf   = D(m.NoPayHalf);
    decimal lateHrs  = D(m.LateTotal);

    if (wd == 0 && npTotal == 0 && lateHrs == 0) continue;

    empRows.Add(new
    {
        employeeId     = emp.Id,
        employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
        epfNo          = emp.EPFNo,
        departmentName = emp.DepartmentName ?? "",
        workingDays    = wd,
        noPayFull      = Math.Round(npFull, 2),
        noPayHalf      = Math.Round(npHalf, 2),
        noPayTotal     = Math.Round(npTotal, 2),
        lateTotal      = Math.Round(lateHrs, 2)
    });
}
            if (!empRows.Any()) continue;

            periodBlocks.Add(new
            {
                year,
                monthName,
                locked     = state.locked,
                processing = state.processing,
                employees  = empRows
            });
        }

        return Ok(new
        {
            success = true,
            data = new { mode = "summary", periods = periodBlocks }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunCombinedReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
[HttpPost("report/ot/run")]
public async Task<IActionResult> RunOtReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = req.PeriodMode, employees = new List<object>(), periods = new List<object>() } });

        var ids = employees.Select(e => e.Id).ToList();
        using var conn = new SqlConnection(_connectionString);

        decimal H(TimeSpan? t) => (decimal)(t?.TotalHours ?? 0);

        // ── RANGE ──
        if (string.Equals(req.PeriodMode, "range", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(req.FromDate, out var from) || !DateTime.TryParse(req.ToDate, out var to))
                return BadRequest(new { success = false, message = "Invalid From/To date" });

            var toEnd = to.Date.AddDays(1).AddSeconds(-1);

            const string sql = @"
                SELECT EmployeeId, FirstInDate AS [Date],
                       OtHoursNormal, OtHoursWeeklyDayOff, OtHoursWeeklyHalfDayOff,
                       OtHoursPoya, OtHoursBankHoliday
                FROM TimeSheet
                WHERE EmployeeId IN @Ids
                  AND FirstInDate >= @From
                  AND FirstInDate <= @To
                ORDER BY EmployeeId, FirstInDate";

            var rows = await conn.QueryAsync<dynamic>(sql,
                new { Ids = ids, From = from.Date, To = toEnd });

            var result = new List<object>();
            foreach (var emp in employees)
            {
                var empRows = rows.Where(r => (int)r.EmployeeId == emp.Id)
                    .Select(r => new
                    {
                        date             = (DateTime)r.Date,
                        normal           = Math.Round(H((TimeSpan?)r.OtHoursNormal), 4),
                        weeklyDayOff     = Math.Round(H((TimeSpan?)r.OtHoursWeeklyDayOff), 4),
                        weeklyHalfDayOff = Math.Round(H((TimeSpan?)r.OtHoursWeeklyHalfDayOff), 4),
                        poya             = Math.Round(H((TimeSpan?)r.OtHoursPoya), 4),
                        mercantile       = 0m,
                        bankHoliday      = Math.Round(H((TimeSpan?)r.OtHoursBankHoliday), 4)
                    })
                    .Where(x => x.normal + x.weeklyDayOff + x.weeklyHalfDayOff + x.poya + x.bankHoliday > 0)
                    .ToList();

                if (!empRows.Any()) continue;

                result.Add(new
                {
                    employeeId     = emp.Id,
                    employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo          = emp.EPFNo,
                    departmentName = emp.DepartmentName ?? "",
                    rows           = empRows
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    mode = "range",
                    from = from.ToString("yyyy-MM-dd"),
                    to   = to.ToString("yyyy-MM-dd"),
                    employees = result
                }
            });
        }

        // ── SUMMARY ──
var periods = BuildPeriodList(req);
var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
if (settings == null)
    return BadRequest(new { success = false, message = "Company settings not found" });

int startDayWages  = settings.SalaryStartDate_Wages;
int startDayOffice = settings.SalaryStartDate_Office;

// Split employees ONCE — NOT per period
var officeEmps = employees
    .Where(e => !string.IsNullOrEmpty(e.DepartmentName) &&
                e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
    .Select(e => e.Id).ToList();
var wagesEmps = employees
    .Where(e => string.IsNullOrEmpty(e.DepartmentName) ||
                !e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
    .Select(e => e.Id).ToList();

// ONE aggregate per start-day group, all six OT buckets in one SELECT
const string bucketSql = @"
    SELECT
        EmployeeId,
        ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursNormal)          AS DECIMAL(18,4))), 0) / 60.0 AS Normal,
        ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyDayOff)    AS DECIMAL(18,4))), 0) / 60.0 AS WeeklyDayOff,
        ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyHalfDayOff) AS DECIMAL(18,4))), 0) / 60.0 AS WeeklyHalfDayOff,
        ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursPoya)            AS DECIMAL(18,4))), 0) / 60.0 AS Poya,
        ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursBankHoliday)     AS DECIMAL(18,4))), 0) / 60.0 AS BankHoliday
    FROM TimeSheet
    WHERE EmployeeId IN @Ids
      AND FirstInDate >= @From
      AND FirstInDate <= @To
    GROUP BY EmployeeId
    HAVING ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursNormal)          AS DECIMAL(18,4))), 0)
         + ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyDayOff)    AS DECIMAL(18,4))), 0)
         + ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyHalfDayOff) AS DECIMAL(18,4))), 0)
         + ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursPoya)            AS DECIMAL(18,4))), 0)
         + ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursBankHoliday)     AS DECIMAL(18,4))), 0) > 0";

var periodBlocks = new List<object>();

foreach (var (year, month, monthName) in periods)
{
    var state = await GetPayrollPeriodStateAsync(conn, req.CompanyId, year, monthName);
    if (!state.exists) continue;

    // empId → dynamic row (all six buckets)
    var totals = new Dictionary<int, dynamic>();

    async Task FetchForGroupAsync(List<int> groupIds, int startDay)
    {
        if (!groupIds.Any()) return;

        var (fromDate, toDate) = GetSalaryPeriodRange(year, month, startDay);

        var rs = await conn.QueryAsync<dynamic>(bucketSql, new
        {
            Ids  = groupIds,
            From = fromDate,
            To   = toDate
        });

        foreach (var r in rs)
        {
            totals[(int)r.EmployeeId] = r;
        }
    }

    await FetchForGroupAsync(officeEmps, startDayOffice);   // 1 query
    await FetchForGroupAsync(wagesEmps,  startDayWages);    // 1 query

    var empRows = new List<object>();

    foreach (var emp in employees)
    {
        if (!totals.TryGetValue(emp.Id, out var r)) continue;

        decimal normal           = Math.Round((decimal)(r.Normal           ?? 0m), 2);
        decimal weeklyDayOff     = Math.Round((decimal)(r.WeeklyDayOff     ?? 0m), 2);
        decimal weeklyHalfDayOff = Math.Round((decimal)(r.WeeklyHalfDayOff ?? 0m), 2);
        decimal poya             = Math.Round((decimal)(r.Poya             ?? 0m), 2);
        decimal bankHoliday      = Math.Round((decimal)(r.BankHoliday      ?? 0m), 2);

        decimal sum = normal + weeklyDayOff + weeklyHalfDayOff + poya + bankHoliday;
        if (sum <= 0) continue;   // HAVING already filters, but defensive

        empRows.Add(new
        {
            employeeId       = emp.Id,
            employeeName     = $"{emp.FirstName} {emp.LastName}".Trim(),
            epfNo            = emp.EPFNo,
            departmentName   = emp.DepartmentName ?? "",
            normal,
            weeklyDayOff,
            weeklyHalfDayOff,
            poya,
            mercantile       = 0m,
            bankHoliday
        });
    }

    if (!empRows.Any()) continue;

    periodBlocks.Add(new
    {
        year,
        monthName,
        locked     = state.locked,
        processing = state.processing,
        employees  = empRows
    });
}

return Ok(new
{
    success = true,
    data = new { mode = "summary", periods = periodBlocks }
});
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunOtReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
[HttpPost("report/workingdays/run")]
public async Task<IActionResult> RunWorkingDaysReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = "summary", periods = new List<object>() } });

        var ids = employees.Select(e => e.Id).ToList();

        var periods = BuildPeriodList(req);

        var settings = await _companySettingsService.GetCompanySettingsAsync(req.CompanyId);
        if (settings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        int startDayWages  = settings.SalaryStartDate_Wages;
        int startDayOffice = settings.SalaryStartDate_Office;

        // Split employees ONCE by start-day rule (Office vs Wages)
        var officeEmps = employees
            .Where(e => !string.IsNullOrEmpty(e.DepartmentName) &&
                        e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id).ToList();
        var wagesEmps  = employees
            .Where(e => string.IsNullOrEmpty(e.DepartmentName) ||
                        !e.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id).ToList();

        using var conn = new SqlConnection(_connectionString);

        // Batched aggregate — one query per start-day group per period
        const string bucketSql = @"
            SELECT
                EmployeeId,
                COUNT(*) AS WorkingDays
            FROM TimeSheet
            WHERE EmployeeId IN @Ids
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND FirstInTime  != '00:00'
              AND (DayType IS NULL OR DayType NOT IN ('No Pay','No Pay - Half'))
            GROUP BY EmployeeId";

        var periodBlocks = new List<object>();

        foreach (var (year, month, monthName) in periods)
        {
            var state = await GetPayrollPeriodStateAsync(conn, req.CompanyId, year, monthName);
            if (!state.exists) continue;

            // empId → working days
            var counts = new Dictionary<int, int>();

            async Task FetchForGroupAsync(List<int> groupIds, int startDay)
            {
                if (!groupIds.Any()) return;

                var (fromDate, toDate) = GetSalaryPeriodRange(year, month, startDay);

                var rs = await conn.QueryAsync<dynamic>(bucketSql, new
                {
                    Ids  = groupIds,
                    From = fromDate,
                    To   = toDate
                });

                foreach (var r in rs)
                {
                    counts[(int)r.EmployeeId] = (int)r.WorkingDays;
                }
            }

            await FetchForGroupAsync(officeEmps, startDayOffice);   // 1 query
            await FetchForGroupAsync(wagesEmps,  startDayWages);    // 1 query

            var empRows = new List<object>();

            foreach (var emp in employees)
            {
                if (!counts.TryGetValue(emp.Id, out int wd) || wd <= 0) continue;

                empRows.Add(new
                {
                    employeeId       = emp.Id,
                    employeeName     = $"{emp.FirstName} {emp.LastName}".Trim(),
                    epfNo            = emp.EPFNo,
                    departmentName   = emp.DepartmentName ?? "",
                    totalWorkingDays = wd
                });
            }

            if (!empRows.Any()) continue;

            periodBlocks.Add(new
            {
                year,
                monthName,
                locked     = state.locked,
                processing = state.processing,
                employees  = empRows
            });
        }

        return Ok(new
        {
            success = true,
            data = new { mode = "summary", periods = periodBlocks }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunWorkingDaysReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
[HttpPost("report/rowattendance/run")]
public async Task<IActionResult> RunRowAttendanceReport([FromBody] LeaveReportRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0)
            return BadRequest(new { success = false, message = "Invalid request" });

        if (!DateTime.TryParse(req.FromDate, out var from) || !DateTime.TryParse(req.ToDate, out var to))
            return BadRequest(new { success = false, message = "Invalid From/To date" });
        if (from > to)
            return BadRequest(new { success = false, message = "From must be before To" });

        var employees = await ResolveReportEmployeesAsync(req);
        if (!employees.Any())
            return Ok(new { success = true, data = new { mode = "range", employees = new List<object>() } });

        var ids = employees.Select(e => e.Id).ToList();
        var toEnd = to.Date.AddDays(1).AddSeconds(-1);

        using var conn = new SqlConnection(_connectionString);

        const string sql = @"
            SELECT a.id AS Id, a.employee_id AS EmployeeId, a.datetime AS DateTime,
                   a.input_type AS InputType, a.input_datetime AS InputDateTime
            FROM Attendance a
            WHERE a.employee_id IN @Ids
              AND a.datetime >= @From
              AND a.datetime <= @To
            ORDER BY a.employee_id, a.datetime";

        var rows = await conn.QueryAsync<dynamic>(sql,
            new { Ids = ids, From = from.Date, To = toEnd });

        var result = new List<object>();
        foreach (var emp in employees)
        {
            var empRows = rows.Where(r => (int)r.EmployeeId == emp.Id).ToList();
            if (!empRows.Any()) continue;

            result.Add(new
            {
                employeeId     = emp.Id,
                employeeName   = $"{emp.FirstName} {emp.LastName}".Trim(),
                epfNo          = emp.EPFNo,
                departmentName = emp.DepartmentName ?? "",
                rows = empRows.Select(r => new
                {
                    id            = (int)r.Id,
                    dateTime      = (DateTime)r.DateTime,
                    inputType     = (string?)r.InputType ?? "Manual",
                    inputDateTime = (DateTime?)r.InputDateTime
                }).ToList()
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                mode = "range",
                from = from.ToString("yyyy-MM-dd"),
                to   = to.ToString("yyyy-MM-dd"),
                employees = result
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "RunRowAttendanceReport failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// Shared: resolve employees for the current scope.
// ============================================================
private async Task<List<EmployeeRow>> ResolveReportEmployeesAsync(LeaveReportRequest req)
{
    var empQuery = _context.Employees
        .AsNoTracking()
        .Where(e => e.CompanyId == req.CompanyId && e.IsActive == true);

    if (req.Scope == "selected")
    {
        if (!req.EmployeeId.HasValue) return new List<EmployeeRow>();
        empQuery = empQuery.Where(e => e.Id == req.EmployeeId.Value);
    }
    else if (req.Scope == "lines")
    {
        if (!req.DepartmentId.HasValue) return new List<EmployeeRow>();
        empQuery = empQuery.Where(e => e.DepartmentId == req.DepartmentId.Value);
    }

    return await empQuery
        .Join(_context.Company_Departments,
              e => e.DepartmentId, d => d.Id,
              (e, d) => new EmployeeRow
              {
                  Id             = e.Id,
                  EPFNo          = e.EPFNo,
                  FirstName      = e.FirstName,
                  LastName       = e.LastName,
                  DepartmentName = d.DepartmentName
              })
        .OrderBy(x => x.EPFNo)
        .ToListAsync();
}

// ============================================================
// Shared: build the list of (year, month, monthName) to process.
//   - if From/To are supplied → that range (inclusive)
//   - else error
// ============================================================
private List<(int Year, int Month, string MonthName)> BuildPeriodList(LeaveReportRequest req)
{
    if (!req.FromYear.HasValue || string.IsNullOrWhiteSpace(req.FromMonthName)
        || !req.ToYear.HasValue || string.IsNullOrWhiteSpace(req.ToMonthName))
        throw new InvalidOperationException("From/To year+month required");

    int fromMon = GetMonthNumber(req.FromMonthName);
    int toMon   = GetMonthNumber(req.ToMonthName);
    if (fromMon == 0 || toMon == 0)
        throw new InvalidOperationException("Invalid month name");

    int y = req.FromYear.Value, m = fromMon;
    int endY = req.ToYear.Value, endM = toMon;

    if (y * 12 + m > endY * 12 + endM)
        throw new InvalidOperationException("From must be <= To");

    var list = new List<(int, int, string)>();
    while (y < endY || (y == endY && m <= endM))
    {
        list.Add((y, m, GetMonthNameFromNumber(m)));
        m++; if (m > 12) { m = 1; y++; }
    }
    return list;
}
// ============================================================
// POST /api/attendance/ensure-payroll-period
//   Body: { companyId, year, monthName }
//   Creates the Payroll_Process row if it doesn't already exist.
//   Idempotent — safe to call from any module that needs to
//   guarantee a period exists before queuing work.
// ============================================================
public class EnsurePayrollPeriodRequest
{
    public int CompanyId { get; set; }
    public int Year { get; set; }
    public string MonthName { get; set; } = "";
}
// ============================================================
// Ensure a Payroll_Process row exists for (companyId, year, monthName).
// If it doesn't exist → INSERT one in "not processing, not locked" state.
// If it already exists → no-op.
// Returns: (exists, created, locked, processing) — created = true only when
// this call actually inserted the row.
// Safe to call concurrently — uses INSERT … WHERE NOT EXISTS, which is
// atomic against another insert racing to create the same row.
// ============================================================
private async Task<(bool exists, bool created, bool locked, bool processing)>
    EnsurePayrollPeriodAsync(int companyId, int year, string monthName, int userId)
{
    using var conn = new SqlConnection(_connectionString);

    // 1) Already there?
    const string getSql = @"
        SELECT TOP 1
            [processing] AS Processing,
            [locked]     AS Locked
        FROM Payroll_Process
        WHERE company_id = @CompanyId
          AND year       = @Year
          AND month      = @Month";

    var existing = await conn.QueryFirstOrDefaultAsync<dynamic>(
        getSql, new { CompanyId = companyId, Year = year, Month = monthName });

    if (existing != null)
    {
        return (
            true,
            false,
            (bool)(existing.Locked     ?? false),
            (bool)(existing.Processing ?? false));
    }

    // 2) Not there → insert atomically
    //
    //    Payroll_Process has a UNIQUE constraint on (company_id, year, month)
    //    in this schema. If it does NOT, the INSERT below still works because
    //    of the WHERE NOT EXISTS; but the race window is only closed when the
    //    unique constraint exists. In practice it always does for this table.
    //
    //    The columns we set on insert match the shape GetPayrollPeriodStateAsync
    //    expects, and set "not processing / not locked" so the client can
    //    start the queue immediately.
    const string insertSql = @"
        INSERT INTO Payroll_Process
            (company_id, year, month, payrollStartDate, processing, locked)
        SELECT
            @CompanyId, @Year, @Month, @PayrollStartDate, 0, 0
        WHERE NOT EXISTS (
            SELECT 1
            FROM Payroll_Process
            WHERE company_id = @CompanyId
              AND year       = @Year
              AND month      = @Month
        );";

    // payrollStartDate: first day of the month of `year`/`monthName`
    int monthNum = GetMonthNumber(monthName);
    var payrollStart = new DateTime(year, monthNum, 1);

    var inserted = await conn.ExecuteAsync(insertSql, new
    {
        CompanyId        = companyId,
        Year             = year,
        Month            = monthName,       // Payroll_Process.month stores the name ("April")
        PayrollStartDate = payrollStart
    });

    if (inserted > 0)
    {
        _logger.LogInformation(
            "Auto-created Payroll_Process row — company {CompanyId}, {Year}-{Month}",
            companyId, year, monthName);
    }
    else
    {
        // Someone else created it between our two statements → just report it exists
        _logger.LogInformation(
            "Payroll_Process row for {CompanyId}/{Year}-{Month} was created concurrently",
            companyId, year, monthName);
    }

    return (true, inserted > 0, false, false);
}


[HttpPost("ensure-payroll-period")]
public async Task<IActionResult> EnsurePayrollPeriod([FromBody] EnsurePayrollPeriodRequest req)
{
    try
    {
        if (req == null || req.CompanyId <= 0 || req.Year <= 0 || string.IsNullOrWhiteSpace(req.MonthName))
            return BadRequest(new { success = false, message = "companyId, year and monthName are required" });

        // Normalize "April" / "apr" / "04" → "April" so the DB column stays consistent
        int monthNum = GetMonthNumber(req.MonthName);
        if (monthNum == 0)
            return BadRequest(new { success = false, message = "Invalid month name" });

        string canonicalMonth = GetMonthNameFromNumber(monthNum);

        // Optional user id, same pattern as elsewhere
        int userId = 0;
        var userIdClaim = User?.FindFirst("UserId")?.Value ?? User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out userId);

        var result = await EnsurePayrollPeriodAsync(req.CompanyId, req.Year, canonicalMonth, userId);

        return Ok(new
        {
            success = true,
            data = new
            {
                exists     = result.exists,
                created    = result.created,
                locked     = result.locked,
                processing = result.processing,
                companyId  = req.CompanyId,
                year       = req.Year,
                monthName  = canonicalMonth
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Error ensuring payroll period for company {CompanyId}, {Year}-{Month}",
            req?.CompanyId, req?.Year, req?.MonthName);
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// Shared: timesheet summary numbers for one employee in one period.
// Mirrors GetTimesheetSummary — but usable inline from other actions.
// ============================================================
private async Task<object> BuildTimesheetSummaryAsync(int employeeId, int year, int month)
{
    var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
    if (employee == null) return null;

    var settings   = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
    var department = await _context.Company_Departments.AsNoTracking()
        .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);

    int startDay = (department != null &&
        department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
        ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

    var (startDate, endDate) = GetSalaryPeriodRange(year, month, startDay);

    using var conn = new SqlConnection(_connectionString);

    var totalWorkingDays = await conn.ExecuteScalarAsync<int>(@"
        SELECT COUNT(*) FROM TimeSheet
        WHERE EmployeeId = @Emp AND FirstInDate >= @From AND FirstInDate <= @To
          AND FirstInTime IS NOT NULL AND FirstOutTime IS NOT NULL
          AND (DayType IS NULL OR DayType NOT IN ('No Pay','No Pay - Half'))",
        new { Emp = employeeId, From = startDate, To = endDate });

    var totalNoPayCount = await conn.ExecuteScalarAsync<decimal?>(@"
        SELECT ISNULL(SUM(CASE WHEN DayType = 'No Pay' THEN 1.0
                               WHEN DayType = 'No Pay - Half' THEN 0.5
                               ELSE 0.0 END), 0)
        FROM TimeSheet
        WHERE EmployeeId = @Emp AND FirstInDate >= @From AND FirstInDate <= @To
          AND DayType IN ('No Pay','No Pay - Half')",
        new { Emp = employeeId, From = startDate, To = endDate }) ?? 0m;

    var totalLeaveTaken = await conn.ExecuteScalarAsync<decimal?>(@"
        SELECT ISNULL(SUM(CASE WHEN IsHalfDay = 1 THEN 0.5 ELSE 1.0 END), 0)
        FROM [Leave]
        WHERE EmployeeId = @Emp AND LeaveDate >= @From AND LeaveDate <= @To
          AND Status = 'Approved'",
        new { Emp = employeeId, From = startDate, To = endDate }) ?? 0m;

    var totalPayCutHours = await conn.ExecuteScalarAsync<decimal?>(@"
        SELECT ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0), 0)
        FROM TimeSheet
        WHERE EmployeeId = @Emp AND FirstInDate >= @From AND FirstInDate <= @To
          AND LateHours IS NOT NULL",
        new { Emp = employeeId, From = startDate, To = endDate }) ?? 0m;

    var totalWorkingHours = await conn.ExecuteScalarAsync<decimal?>(@"
        SELECT ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), WorkHours) AS DECIMAL(18,4)) / 60.0), 0)
        FROM TimeSheet
        WHERE EmployeeId = @Emp AND FirstInDate >= @From AND FirstInDate <= @To
          AND WorkHours IS NOT NULL",
        new { Emp = employeeId, From = startDate, To = endDate }) ?? 0m;

    var ot = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
        SELECT
          ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursNormal)          AS DECIMAL(18,4))) / 60.0, 0) AS Normal,
          ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyDayOff)    AS DECIMAL(18,4))) / 60.0, 0) AS WeeklyDayOff,
          ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyHalfDayOff) AS DECIMAL(18,4))) / 60.0, 0) AS WeeklyHalfDayOff,
          ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursPoya)            AS DECIMAL(18,4))) / 60.0, 0) AS Poya,
          ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursBankHoliday)     AS DECIMAL(18,4))) / 60.0, 0) AS BankHoliday
        FROM TimeSheet
        WHERE EmployeeId = @Emp AND FirstInDate >= @From AND FirstInDate <= @To",
        new { Emp = employeeId, From = startDate, To = endDate });

    var totalFullNight = await conn.ExecuteScalarAsync<int>(@"
        SELECT COUNT(*) FROM NightDates
        WHERE EmployeeId = @Emp AND [Date] >= @From AND [Date] <= @To AND IsFullNight = 1",
        new { Emp = employeeId, From = startDate, To = endDate });

    return new
    {
        periodKey              = $"{year}-{GetMonthNameFromNumber(month)}",
        startDate, endDate,
        totalWorkingDays,
        totalNoPayCount        = Math.Round(totalNoPayCount, 2),
        totalLeaveTaken        = Math.Round(totalLeaveTaken, 2),
        totalPayCutHours       = Math.Round(totalPayCutHours, 2),
        totalWorkingHours      = Math.Round(totalWorkingHours, 2),
        totalOtNormal           = Math.Round((decimal)(ot?.Normal          ?? 0m), 2),
        totalOtWeeklyDayOff     = Math.Round((decimal)(ot?.WeeklyDayOff    ?? 0m), 2),
        totalOtWeeklyHalfDayOff = Math.Round((decimal)(ot?.WeeklyHalfDayOff ?? 0m), 2),
        totalOtPoya             = Math.Round((decimal)(ot?.Poya            ?? 0m), 2),
        totalOtMercantile       = 0m,
        totalOtBankHoliday      = Math.Round((decimal)(ot?.BankHoliday     ?? 0m), 2),
        totalFullNight
    };
}

// ============================================================
// Shared: daily rows for one employee in one period.
// Mirrors GetTimesheet — but usable inline from other actions.
// ============================================================
private async Task<object> BuildTimesheetDaysAsync(int employeeId, int year, int month)
{
    var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
    if (employee == null) return null;

    var settings   = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
    var department = await _context.Company_Departments.AsNoTracking()
        .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);

    int startDay = (department != null &&
        department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
        ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

    var (startDate, endDate) = GetSalaryPeriodRange(year, month, startDay);

    var rows = await _context.TimeSheets
        .Where(t => t.EmployeeId == employeeId &&
                    ((t.FirstInDate  >= startDate && t.FirstInDate  <= endDate) ||
                     (t.SecondInDate >= startDate && t.SecondInDate <= endDate)))
        .ToListAsync();

    var days = new List<object>();
    var cur = startDate;
    while (cur <= endDate)
    {
        var row = rows.FirstOrDefault(t =>
            (t.FirstInDate.HasValue  && t.FirstInDate.Value.Date  == cur.Date) ||
            (t.SecondInDate.HasValue && t.SecondInDate.Value.Date == cur.Date));

        days.Add(new
        {
            date       = cur,
            dayName    = cur.DayOfWeek.ToString(),
            workHours  = row?.WorkHours?.ToString(@"hh\:mm")     ?? "00:00",
            lateHours  = row?.LateHours?.ToString(@"hh\:mm")     ?? "00:00",
            otNormal   = row?.OtHoursNormal?.ToString(@"hh\:mm") ?? "00:00",
            otDouble   = "00:00",
            timesheetRecord = row == null ? null : new
            {
                dayType   = row.DayType ?? "Normal",
                firstInTime  = row.FirstInTime,
                firstOutTime = row.FirstOutTime,
                firstOutDate = row.FirstOutDate,
                firstShiftBlockId = row.FirstShiftBlockId
            }
        });
        cur = cur.AddDays(1);
    }
    return days;
}

// ============================================================
// Employee row DTO used by all report endpoints
// ============================================================
private class EmployeeRow
{
    public int Id { get; set; }
    public int EPFNo { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? DepartmentName { get; set; }
}
        // ============================================================
        // SAVE / UPDATE SHIFT BLOCK for a single day
        // ============================================================
        [HttpPost("shift-block")]
        public async Task<IActionResult> SaveShiftBlock([FromBody] SaveShiftBlockRequest request)
        {
            if (request == null || request.EmployeeId <= 0 || string.IsNullOrWhiteSpace(request.Date))
                return BadRequest(new { success = false, message = "employeeId and date are required" });

            if (!DateTime.TryParse(request.Date, out var date))
                return BadRequest(new { success = false, message = "Invalid date format (expected YYYY-MM-DD)" });

            if (request.ShiftBlockIdFirstShift <= 0 && (request.ShiftBlockIdSecondShift ?? 0) <= 0)
                return BadRequest(new { success = false, message = "At least one shift block id is required" });

            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sql = @"
                    MERGE ShiftsTimesheet AS target
                    USING (SELECT @EmployeeId AS EmployeeId, @Date AS [Date]) AS src
                        ON target.EmployeeId = src.EmployeeId AND target.[Date] = src.[Date]
                    WHEN MATCHED THEN
                        UPDATE SET
                            ShiftBlockIdFirstShift  = @FirstShift,
                            ShiftBlockIdSecondShift = @SecondShift
                    WHEN NOT MATCHED THEN
                        INSERT (EmployeeId, [Date], ShiftBlockIdFirstShift, ShiftBlockIdSecondShift)
                        VALUES (@EmployeeId, @Date, @FirstShift, @SecondShift);";

                var rows = await connection.ExecuteAsync(sql, new
                {
                    EmployeeId = request.EmployeeId,
                    Date = date.Date,
                    FirstShift = request.ShiftBlockIdFirstShift > 0 ? (int?)request.ShiftBlockIdFirstShift : null,
                    SecondShift = request.ShiftBlockIdSecondShift.HasValue && request.ShiftBlockIdSecondShift > 0
                        ? (int?)request.ShiftBlockIdSecondShift
                        : null
                });

                _logger.LogInformation(
                    "ShiftTimesheet saved — employee {EmployeeId}, date {Date}, rows {Rows}",
                    request.EmployeeId, date.Date, rows);

                if (!string.IsNullOrWhiteSpace(request.PeriodKey))
                {
                    var ipAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
                    await TriggerProcessQueue(request.EmployeeId, request.PeriodKey, ipAddress);
                }

                return Ok(new
                {
                    success = true,
                    message = "Shift block saved",
                    employeeId = request.EmployeeId,
                    date = date.Date.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving shift block for employee {EmployeeId}, date {Date}",
                    request.EmployeeId, request.Date);

                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ============================================================
        // ============================================================
// GET /api/attendance/summary
//   Either year+monthName  OR  fromDate+toDate (overrides)
// ============================================================
// ============================================================
// GET /api/attendance/summary
//   Mode 1: year+monthName
//   Mode 2: fromYear+fromMonth+toYear+toMonth (period range)
// ============================================================
[HttpGet("summary")]
public async Task<IActionResult> GetTimesheetSummary(
    [FromQuery] int employeeId,
    [FromQuery] string? year,
    [FromQuery] string? monthName,
    [FromQuery] int? fromYear,
    [FromQuery] string? fromMonth,
    [FromQuery] int? toYear,
    [FromQuery] string? toMonth)
{
    try
    {
        if (employeeId <= 0)
            return BadRequest(new { success = false, message = "employeeId is required" });

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId);

        if (employee == null)
            return NotFound(new { success = false, message = "Employee not found" });

        var companySettings = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
        if (companySettings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        var department = await _context.Company_Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);

        int salaryStartDay = department != null &&
            department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase)
            ? companySettings.SalaryStartDate_Office
            : companySettings.SalaryStartDate_Wages;

        int targetYear  = string.IsNullOrEmpty(year) ? DateTime.Now.Year : int.Parse(year);
        int targetMonth = GetMonthNumberFromName(monthName) ?? DateTime.Now.Month;
        if (targetMonth == 0)
            return BadRequest(new { success = false, message = "Invalid month name" });

        // ── Base range from single period ──
        DateTime startDate, endDate;
        if (salaryStartDay == 1)
        {
            startDate = new DateTime(targetYear, targetMonth, 1);
            endDate   = new DateTime(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
        }
        else
        {
            startDate = new DateTime(targetYear, targetMonth, salaryStartDay);
            var nextMonth = startDate.AddMonths(1);
            endDate = new DateTime(nextMonth.Year, nextMonth.Month, salaryStartDay).AddDays(-1);
        }

        // ── Period-range override ──
        if (fromYear.HasValue && toYear.HasValue
            && !string.IsNullOrWhiteSpace(fromMonth) && !string.IsNullOrWhiteSpace(toMonth))
        {
            int fromMonthNum = GetMonthNumber(fromMonth);
            int toMonthNum   = GetMonthNumber(toMonth);

            if (fromMonthNum > 0 && toMonthNum > 0)
            {
                if (salaryStartDay == 1)
                    startDate = new DateTime(fromYear.Value, fromMonthNum, 1);
                else
                    startDate = new DateTime(fromYear.Value, fromMonthNum, salaryStartDay);

                if (salaryStartDay == 1)
                    endDate = new DateTime(toYear.Value, toMonthNum,
                        DateTime.DaysInMonth(toYear.Value, toMonthNum));
                else
                {
                    var toPeriodStart = new DateTime(toYear.Value, toMonthNum, salaryStartDay);
                    var afterToMonth  = toPeriodStart.AddMonths(1);
                    endDate = new DateTime(afterToMonth.Year, afterToMonth.Month, salaryStartDay).AddDays(-1);
                }
            }
        }

        using var connection = new SqlConnection(_connectionString);

        // 1) Working days
        const string workingDaysSql = @"
            SELECT COUNT(*)
            FROM TimeSheet
            WHERE EmployeeId = @EmployeeId
              AND FirstInDate  >= @StartDate
              AND FirstInDate  <= @EndDate
              AND FirstInTime  !='00:00'
              AND (DayType IS NULL
                   OR DayType NOT IN ('No Pay', 'No Pay - Half'))";

        var totalWorkingDays = await connection.ExecuteScalarAsync<int>(
            workingDaysSql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate });

        // 2) No-Pay count
        const string noPaySql = @"
            SELECT ISNULL(SUM(
                CASE WHEN DayType = 'No Pay'      THEN 1.0
                     WHEN DayType = 'No Pay - Half' THEN 0.5
                     ELSE 0.0 END), 0)
            FROM TimeSheet
            WHERE EmployeeId = @EmployeeId
              AND FirstInDate >= @StartDate
              AND FirstInDate <= @EndDate
              AND DayType IN ('No Pay', 'No Pay - Half')";

        var totalNoPayCount = await connection.ExecuteScalarAsync<decimal?>(
            noPaySql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate }) ?? 0m;

        // 3) Leave taken
        const string leaveTakenSql = @"
            SELECT ISNULL(SUM(CASE WHEN IsHalfDay = 1 THEN 0.5 ELSE 1.0 END), 0)
            FROM [Leave]
            WHERE EmployeeId = @EmployeeId
              AND LeaveDate >= @StartDate
              AND LeaveDate <= @EndDate
              AND Status = 'Approved'";

        var totalLeaveTaken = await connection.ExecuteScalarAsync<decimal?>(
            leaveTakenSql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate }) ?? 0m;

        // 4) Pay-cut hours
        const string payCutSql = @"
            SELECT ISNULL(SUM(
                CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
            ), 0)
            FROM TimeSheet
            WHERE EmployeeId = @EmployeeId
              AND FirstInDate >= @StartDate
              AND FirstInDate <= @EndDate
              AND LateHours IS NOT NULL";

        var totalPayCutHours = await connection.ExecuteScalarAsync<decimal?>(
            payCutSql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate }) ?? 0m;

        // 5) Working hours
        const string workingHoursSql = @"
            SELECT ISNULL(SUM(
                CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), WorkHours) AS DECIMAL(18,4)) / 60.0
            ), 0)
            FROM TimeSheet
            WHERE EmployeeId = @EmployeeId
              AND FirstInDate >= @StartDate
              AND FirstInDate <= @EndDate
              AND WorkHours IS NOT NULL";

        var totalWorkingHours = await connection.ExecuteScalarAsync<decimal?>(
            workingHoursSql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate }) ?? 0m;

        // 6) OT summary
        const string otSql = @"
            SELECT
              ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursNormal)          AS DECIMAL(18,4))) / 60.0, 0) AS TotalOtNormal,
              ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyDayOff)    AS DECIMAL(18,4))) / 60.0, 0) AS TotalOtWeeklyDayOff,
              ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyHalfDayOff) AS DECIMAL(18,4))) / 60.0, 0) AS TotalOtWeeklyHalfDayOff,
              ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursPoya)            AS DECIMAL(18,4))) / 60.0, 0) AS TotalOtPoya,
              ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursBankHoliday)     AS DECIMAL(18,4))) / 60.0, 0) AS TotalOtBankHoliday,
              0 AS TotalOtMercantile
            FROM TimeSheet
            WHERE EmployeeId = @EmployeeId
              AND FirstInDate >= @StartDate
              AND FirstInDate <= @EndDate";

        var otRow = await connection.QuerySingleOrDefaultAsync<dynamic>(
            otSql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate });

        decimal totalOtNormal           = otRow?.TotalOtNormal           ?? 0m;
        decimal totalOtWeeklyDayOff     = otRow?.TotalOtWeeklyDayOff     ?? 0m;
        decimal totalOtWeeklyHalfDayOff = otRow?.TotalOtWeeklyHalfDayOff ?? 0m;
        decimal totalOtPoya             = otRow?.TotalOtPoya             ?? 0m;
        decimal totalOtBankHoliday      = otRow?.TotalOtBankHoliday      ?? 0m;
        decimal totalOtMercantile       = otRow?.TotalOtMercantile       ?? 0m;

        // 7) Full Night
        const string fullNightSql = @"
            SELECT COUNT(*)
            FROM NightDates
            WHERE EmployeeId = @EmployeeId
              AND [Date] >= @StartDate
              AND [Date] <= @EndDate
              AND IsFullNight = 1";

        var totalFullNight = await connection.ExecuteScalarAsync<int>(
            fullNightSql, new { EmployeeId = employeeId, StartDate = startDate, EndDate = endDate });

        return Ok(new
        {
            success = true,
            data = new
            {
                PeriodKey = $"{targetYear}-{GetMonthNameFromNumber(targetMonth)}",
                StartDate = startDate,
                EndDate   = endDate,

                TotalWorkingDays   = totalWorkingDays,
                TotalNoPayCount    = Math.Round(totalNoPayCount, 2),
                TotalLeaveTaken    = Math.Round(totalLeaveTaken, 2),
                TotalPayCutHours   = Math.Round(totalPayCutHours, 2),
                TotalWorkingHours  = Math.Round(totalWorkingHours, 2),

                TotalOtNormal           = Math.Round(totalOtNormal, 2),
                TotalOtWeeklyDayOff     = Math.Round(totalOtWeeklyDayOff, 2),
                TotalOtWeeklyHalfDayOff = Math.Round(totalOtWeeklyHalfDayOff, 2),
                TotalOtPoya             = Math.Round(totalOtPoya, 2),
                TotalOtMercantile       = Math.Round(totalOtMercantile, 2),
                TotalOtBankHoliday      = Math.Round(totalOtBankHoliday, 2),

                TotalFullNight = totalFullNight
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error building timesheet summary for employee {EmployeeId}", employeeId);
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// GET /api/attendance/payroll-period-status
//   returns { exists, processing, locked } for a given period
// ============================================================
[HttpGet("payroll-period-status")]
public async Task<IActionResult> GetPayrollPeriodStatus(
    [FromQuery] int companyId,
    [FromQuery] int year,
    [FromQuery] string monthName)
{
    try
    {
        if (companyId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName))
            return BadRequest(new { success = false, message = "companyId, year, monthName required" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0)
            return BadRequest(new { success = false, message = "Invalid month name" });

        using var connection = new SqlConnection(_connectionString);

        const string sql = @"
            SELECT TOP 1
                [processing] AS Processing,
                [locked]     AS Locked
            FROM Payroll_Process
            WHERE company_id = @CompanyId
              AND year       = @Year
              AND month      = @Month";

        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(
            sql,
            new { CompanyId = companyId, Year = year, Month = GetMonthName(monthNum) });

        if (row == null)
        {
            return Ok(new
            {
                success = true,
                data = new { exists = false, processing = false, locked = false }
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                exists     = true,
                processing = (bool)(row.Processing ?? false),
                locked     = (bool)(row.Locked     ?? false)
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading payroll period status");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// GET /api/attendance/row-attendance
//   Raw rows from Attendance table for a date range
// ============================================================
[HttpGet("row-attendance")]
public async Task<IActionResult> GetRowAttendance(
    [FromQuery] int employeeId,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
{
    try
    {
        if (employeeId <= 0 || string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            return BadRequest(new { success = false, message = "employeeId, fromDate and toDate are required" });

        if (!DateTime.TryParse(fromDate, out var from) || !DateTime.TryParse(toDate, out var to))
            return BadRequest(new { success = false, message = "Invalid date format" });

        if (from > to)
            return BadRequest(new { success = false, message = "From date must be before To date" });

        // Include the full day of `to`
        var toEnd = to.Date.AddDays(1).AddSeconds(-1);

        using var connection = new SqlConnection(_connectionString);

        var sql = @"
            SELECT
                a.id                AS Id,
                a.employee_id       AS EmployeeId,
                a.datetime          AS DateTime,
                a.input_type        AS InputType,
                a.input_datetime    AS InputDateTime
            FROM Attendance a
            WHERE a.employee_id = @EmployeeId
              AND a.datetime >= @FromDate
              AND a.datetime <= @ToDate
            ORDER BY a.datetime";

        var rows = await connection.QueryAsync<dynamic>(sql, new
        {
            EmployeeId = employeeId,
            FromDate = from.Date,
            ToDate = toEnd
        });

        // Return list of { dateTime, inputType, inputDateTime }
        var data = rows.Select(r => new
        {
            id          = (int)r.Id,
            employeeId  = (int)r.EmployeeId,
            dateTime    = (DateTime)r.DateTime,
            inputType   = (string?)r.InputType ?? "Manual",
            inputDateTime = (DateTime?)r.InputDateTime
        });

        return Ok(new { success = true, data });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading row attendance");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// LEAVE REPORT — rows (date range)
// ============================================================
[HttpGet("report/leave/rows")]
public async Task<IActionResult> GetLeaveRows(
    [FromQuery] int employeeId,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
{
    try
    {
        if (employeeId <= 0 || !DateTime.TryParse(fromDate, out var from) || !DateTime.TryParse(toDate, out var to))
            return BadRequest(new { success = false, message = "Invalid params" });

        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            SELECT LeaveDate, LeaveType, IsHalfDay
            FROM [Leave]
            WHERE EmployeeId = @Emp
              AND LeaveDate >= @From
              AND LeaveDate <= @To
              AND Status = 'Approved'
            ORDER BY LeaveDate";

        var rows = await conn.QueryAsync<dynamic>(sql,
            new { Emp = employeeId, From = from.Date, To = to.Date.AddDays(1).AddSeconds(-1) });

        return Ok(new
        {
            success = true,
            data = rows.Select(r => new
            {
                leaveDate = (DateTime)r.LeaveDate,
                leaveType = (string?)r.LeaveType ?? "",
                isHalfDay = (bool?)r.IsHalfDay ?? false
            })
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Leave rows failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
private (DateTime Start, DateTime End) GetSalaryPeriodRange(int year, int monthNum, int startDay)
{
    if (startDay <= 1)
    {
        return (new DateTime(year, monthNum, 1),
                new DateTime(year, monthNum, DateTime.DaysInMonth(year, monthNum)));
    }
    var start = new DateTime(year, monthNum, startDay);
    var nxt   = start.AddMonths(1);
    var end   = new DateTime(nxt.Year, nxt.Month, startDay).AddDays(-1);
    return (start, end);
}

// ============================================================
// NO PAY REPORT — rows
// ============================================================
[HttpGet("report/nopay/rows")]
public async Task<IActionResult> GetNoPayRows(
    [FromQuery] int employeeId,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
{
    try
    {
        if (employeeId <= 0 || !DateTime.TryParse(fromDate, out var from) || !DateTime.TryParse(toDate, out var to))
            return BadRequest(new { success = false, message = "Invalid params" });

        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            SELECT FirstInDate AS [Date], DayType
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND DayType IN ('No Pay', 'No Pay - Half')
            ORDER BY FirstInDate";

        var rows = await conn.QueryAsync<dynamic>(sql,
            new { Emp = employeeId, From = from.Date, To = to.Date.AddDays(1).AddSeconds(-1) });

        return Ok(new
        {
            success = true,
            data = rows.Select(r => new
            {
                date    = (DateTime)r.Date,
                dayType = (string?)r.DayType ?? ""
            })
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "NoPay rows failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}



// ============================================================
// LATE HOURS REPORT — rows
// ============================================================
[HttpGet("report/late/rows")]
public async Task<IActionResult> GetLateRows(
    [FromQuery] int employeeId,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
{
    try
    {
        if (employeeId <= 0 || !DateTime.TryParse(fromDate, out var from) || !DateTime.TryParse(toDate, out var to))
            return BadRequest(new { success = false, message = "Invalid params" });

        using var conn = new SqlConnection(_connectionString);
        var sql = @"
            SELECT FirstInDate AS [Date], LateHours
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND LateHours IS NOT NULL
              AND LateHours > '00:00:00'
            ORDER BY FirstInDate";

        var rows = await conn.QueryAsync<dynamic>(sql,
            new { Emp = employeeId, From = from.Date, To = to.Date.AddDays(1).AddSeconds(-1) });

        var data = rows.Select(r => {
            var ts = (TimeSpan?)r.LateHours ?? TimeSpan.Zero;
            return new
            {
                date         = (DateTime)r.Date,
                lateHours    = ts.ToString(@"hh\:mm"),
                lateHoursDecimal = Math.Round(ts.TotalHours, 4)
            };
        });

        return Ok(new { success = true, data });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Late rows failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}



// ============================================================
// OT HOURS REPORT — rows
// ============================================================
[HttpGet("report/ot/rows")]
public async Task<IActionResult> GetOtRows(
    [FromQuery] int employeeId,
    [FromQuery] string fromDate,
    [FromQuery] string toDate)
{
    try
    {
        if (employeeId <= 0 || !DateTime.TryParse(fromDate, out var from) || !DateTime.TryParse(toDate, out var to))
            return BadRequest(new { success = false, message = "Invalid params" });

        using var conn = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT FirstInDate AS [Date],
                   OtHoursNormal,
                   OtHoursWeeklyDayOff,
                   OtHoursWeeklyHalfDayOff,
                   OtHoursPoya,
                   OtHoursBankHoliday
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To
            ORDER BY FirstInDate";

        var rows = await conn.QueryAsync<dynamic>(sql,
            new { Emp = employeeId, From = from.Date, To = to.Date.AddDays(1).AddSeconds(-1) });

        decimal H(TimeSpan? t) => (decimal)(t?.TotalHours ?? 0);

        var data = rows
            .Select(r => new
            {
                date             = (DateTime)r.Date,
                normal           = Math.Round(H((TimeSpan?)r.OtHoursNormal), 4),
                weeklyDayOff     = Math.Round(H((TimeSpan?)r.OtHoursWeeklyDayOff), 4),
                weeklyHalfDayOff = Math.Round(H((TimeSpan?)r.OtHoursWeeklyHalfDayOff), 4),
                poya             = Math.Round(H((TimeSpan?)r.OtHoursPoya), 4),
                mercantile       = 0m,
                bankHoliday      = Math.Round(H((TimeSpan?)r.OtHoursBankHoliday), 4)
            })
            .Where(x => x.normal + x.weeklyDayOff + x.weeklyHalfDayOff + x.poya + x.bankHoliday > 0)
            .ToList();

        return Ok(new { success = true, data });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "OT rows failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}

// ============================================================
// Helper — check if a period exists in Payroll_Process
// ============================================================
private async Task<(bool exists, bool locked, bool processing)> GetPayrollPeriodStateAsync(
    SqlConnection conn, int companyId, int year, string monthName)
{
    const string sql = @"
        SELECT TOP 1 [processing] AS Processing, [locked] AS Locked
        FROM Payroll_Process
        WHERE company_id = @CompanyId
          AND year       = @Year
          AND month      = @Month";

    var row = await conn.QueryFirstOrDefaultAsync<dynamic>(sql,
        new { CompanyId = companyId, Year = year, Month = monthName });

    if (row == null)
        return (false, false, false);

    return (
        true,
        (bool)(row.Locked     ?? false),
        (bool)(row.Processing ?? false));
}

// ============================================================
// LEAVE REPORT — summary (single month, category by leave type)
// ============================================================
[HttpGet("report/leave/summary")]
public async Task<IActionResult> GetLeaveSummary(
    [FromQuery] int employeeId,
    [FromQuery] int year,
    [FromQuery] string monthName)
{
    try
    {
        if (employeeId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName))
            return BadRequest(new { success = false, message = "Invalid params" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0) return BadRequest(new { success = false, message = "Invalid month" });

        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        var settings   = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
        var department = await _context.Company_Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);
        int startDay = (department != null &&
            department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

        using var conn = new SqlConnection(_connectionString);

        // 1. Does the payroll period exist?
        var state = await GetPayrollPeriodStateAsync(conn, employee.CompanyId, year, monthName);
        if (!state.exists)
            return Ok(new { success = true, data = new { exists = false } });

        // 2. Compute the salary-period date range
        var (startDate, endDate) = GetSalaryPeriodRange(year, monthNum, startDay);

        // 3. Total + category breakdown
        const string sql = @"
            SELECT 
                ISNULL(LeaveType, 'Unknown') AS LeaveType,
                SUM(CASE WHEN IsHalfDay = 1 THEN 0.5 ELSE 1.0 END) AS Days
            FROM [Leave]
            WHERE EmployeeId = @Emp
              AND LeaveDate >= @From
              AND LeaveDate <= @To
              AND Status = 'Approved'
            GROUP BY LeaveType
            ORDER BY LeaveType";

        var rows = (await conn.QueryAsync<dynamic>(sql,
            new { Emp = employeeId, From = startDate, To = endDate })).ToList();

        decimal total = rows.Sum(r => (decimal)(r.Days ?? 0m));

        var byCategory = rows.Select(r => new
        {
            category = (string)r.LeaveType,
            days     = Math.Round((decimal)(r.Days ?? 0m), 2)
        }).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                exists     = true,
                locked     = state.locked,
                processing = state.processing,
                total      = Math.Round(total, 2),
                byCategory
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Leave summary failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}

// ============================================================
// NO PAY REPORT — summary (category by day type)
// ============================================================
[HttpGet("report/nopay/summary")]
public async Task<IActionResult> GetNoPaySummary(
    [FromQuery] int employeeId,
    [FromQuery] int year,
    [FromQuery] string monthName)
{
    try
    {
        if (employeeId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName))
            return BadRequest(new { success = false, message = "Invalid params" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0) return BadRequest(new { success = false, message = "Invalid month" });

        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        var settings   = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
        var department = await _context.Company_Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);
        int startDay = (department != null &&
            department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

        using var conn = new SqlConnection(_connectionString);

        var state = await GetPayrollPeriodStateAsync(conn, employee.CompanyId, year, monthName);
        if (!state.exists)
            return Ok(new { success = true, data = new { exists = false } });

        var (startDate, endDate) = GetSalaryPeriodRange(year, monthNum, startDay);

        const string sql = @"
            SELECT 
                DayType,
                SUM(CASE WHEN DayType = 'No Pay - Half' THEN 0.5 ELSE 1.0 END) AS Days
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND DayType IN ('No Pay', 'No Pay - Half')
            GROUP BY DayType
            ORDER BY DayType";

        var rows = (await conn.QueryAsync<dynamic>(sql,
            new { Emp = employeeId, From = startDate, To = endDate })).ToList();

        decimal total = rows.Sum(r => (decimal)(r.Days ?? 0m));

        var byCategory = rows.Select(r => new
        {
            category = (string)(r.DayType ?? "No Pay"),
            days     = Math.Round((decimal)(r.Days ?? 0m), 2)
        }).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                exists     = true,
                locked     = state.locked,
                processing = state.processing,
                total      = Math.Round(total, 2),
                byCategory
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "NoPay summary failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}

// ============================================================
// LATE HOURS — summary (add payroll existence check + flags)
// ============================================================
[HttpGet("report/late/summary")]
public async Task<IActionResult> GetLateSummary(
    [FromQuery] int employeeId,
    [FromQuery] int year,
    [FromQuery] string monthName)
{
    try
    {
        if (employeeId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName))
            return BadRequest(new { success = false, message = "Invalid params" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0) return BadRequest(new { success = false, message = "Invalid month" });

        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        var settings   = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
        var department = await _context.Company_Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);
        int startDay = (department != null &&
            department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

        using var conn = new SqlConnection(_connectionString);

        var state = await GetPayrollPeriodStateAsync(conn, employee.CompanyId, year, monthName);
        if (!state.exists)
            return Ok(new { success = true, data = new { exists = false } });

        var (startDate, endDate) = GetSalaryPeriodRange(year, monthNum, startDay);

        const string sql = @"
            SELECT ISNULL(SUM(
                CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), LateHours) AS DECIMAL(18,4)) / 60.0
            ), 0)
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND LateHours IS NOT NULL";

        var total = await conn.ExecuteScalarAsync<decimal>(sql,
            new { Emp = employeeId, From = startDate, To = endDate });

        return Ok(new
        {
            success = true,
            data = new
            {
                exists     = true,
                locked     = state.locked,
                processing = state.processing,
                totalHours = Math.Round(total, 2)
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Late summary failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}

// ============================================================
// OT HOURS — summary (add payroll existence check + flags)
// ============================================================
[HttpGet("report/ot/summary")]
public async Task<IActionResult> GetOtSummary(
    [FromQuery] int employeeId,
    [FromQuery] int year,
    [FromQuery] string monthName)
{
    try
    {
        if (employeeId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName))
            return BadRequest(new { success = false, message = "Invalid params" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0) return BadRequest(new { success = false, message = "Invalid month" });

        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        var settings   = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
        var department = await _context.Company_Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);
        int startDay = (department != null &&
            department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

        using var conn = new SqlConnection(_connectionString);

        var state = await GetPayrollPeriodStateAsync(conn, employee.CompanyId, year, monthName);
        if (!state.exists)
            return Ok(new { success = true, data = new { exists = false } });

        var (startDate, endDate) = GetSalaryPeriodRange(year, monthNum, startDay);

        const string sql = @"
            SELECT
                ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursNormal)          AS DECIMAL(18,4))), 0) / 60.0 AS Normal,
                ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyDayOff)    AS DECIMAL(18,4))), 0) / 60.0 AS WeeklyDayOff,
                ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursWeeklyHalfDayOff) AS DECIMAL(18,4))), 0) / 60.0 AS WeeklyHalfDayOff,
                ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursPoya)            AS DECIMAL(18,4))), 0) / 60.0 AS Poya,
                ISNULL(SUM(CAST(DATEDIFF(MINUTE, CAST('00:00:00' AS TIME), OtHoursBankHoliday)     AS DECIMAL(18,4))), 0) / 60.0 AS BankHoliday
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To";

        var r = await conn.QuerySingleAsync<dynamic>(sql, new { Emp = employeeId, From = startDate, To = endDate });

        return Ok(new
        {
            success = true,
            data = new
            {
                exists           = true,
                locked           = state.locked,
                processing       = state.processing,
                normal           = Math.Round((decimal)(r.Normal ?? 0m), 2),
                weeklyDayOff     = Math.Round((decimal)(r.WeeklyDayOff ?? 0m), 2),
                weeklyHalfDayOff = Math.Round((decimal)(r.WeeklyHalfDayOff ?? 0m), 2),
                poya             = Math.Round((decimal)(r.Poya ?? 0m), 2),
                mercantile       = 0m,
                bankHoliday      = Math.Round((decimal)(r.BankHoliday ?? 0m), 2)
            }
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "OT summary failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// BULK LEAVE SUMMARY
//   GET /api/attendance/report/leave/summary-bulk
//     ?companyId=1&year=2025&monthName=April&employeeIds=1,2,3,...
//   Returns one row per employee — avoids 1048 HTTP round-trips.
// ============================================================
[HttpGet("report/leave/summary-bulk")]
public async Task<IActionResult> GetLeaveSummaryBulk(
    [FromQuery] int companyId,
    [FromQuery] int year,
    [FromQuery] string monthName,
    [FromQuery] string employeeIds)
{
    try
    {
        if (companyId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName)
            || string.IsNullOrWhiteSpace(employeeIds))
            return BadRequest(new { success = false, message = "Invalid params" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0)
            return BadRequest(new { success = false, message = "Invalid month" });

        // ── Parse employee IDs (cap at, say, 5000 to prevent abuse) ──
        var ids = employeeIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var v) ? v : -1)
            .Where(v => v > 0)
            .Distinct()
            .Take(5000)
            .ToList();

        if (!ids.Any())
            return Ok(new { success = true, data = new List<object>() });

        // ── Payroll_Process existence check (same as single endpoint) ──
        using var conn = new SqlConnection(_connectionString);

        const string statusSql = @"
            SELECT TOP 1 [processing] AS Processing, [locked] AS Locked
            FROM Payroll_Process
            WHERE company_id = @CompanyId AND year = @Year AND month = @Month";

        var statusRow = await conn.QueryFirstOrDefaultAsync<dynamic>(
            statusSql,
            new { CompanyId = companyId, Year = year, Month = GetMonthName(monthNum) });

        if (statusRow == null)
        {
            // No payroll record for this period — return empty list.
            return Ok(new { success = true, data = new List<object>() });
        }

        bool locked     = (bool)(statusRow.Locked     ?? false);
        bool processing = (bool)(statusRow.Processing ?? false);

        // ── Get company settings + department salary-start rules for ALL employees at once ──
        var settings = await _companySettingsService.GetCompanySettingsAsync(companyId);
        if (settings == null)
            return BadRequest(new { success = false, message = "Company settings not found" });

        // Fetch employees in one shot
        var employees = await _context.Employees
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id) && e.CompanyId == companyId)
            .Join(_context.Company_Departments,
                  e => e.DepartmentId,
                  d => d.Id,
                  (e, d) => new { Employee = e, DepartmentName = d.DepartmentName })
            .ToListAsync();

        if (!employees.Any())
            return Ok(new { success = true, data = new List<object>() });

        // ── Salary-period start day = Office or Wages based on dept ──
        // Group employees by their effective start day so we can run 1-2 aggregate queries.
        var groups = employees.GroupBy(x =>
            !string.IsNullOrEmpty(x.DepartmentName) &&
            x.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase)
                ? settings.SalaryStartDate_Office
                : settings.SalaryStartDate_Wages);

        // ── Aggregate SQL: total + byCategory for all employees in one query per start-day group ──
        const string aggregateSql = @"
            SELECT
                EmployeeId,
                ISNULL(LeaveType, 'Unknown') AS LeaveType,
                SUM(CASE WHEN IsHalfDay = 1 THEN 0.5 ELSE 1.0 END) AS Days
            FROM [Leave]
            WHERE EmployeeId IN @Ids
              AND LeaveDate >= @From
              AND LeaveDate <= @To
              AND Status = 'Approved'
            GROUP BY EmployeeId, LeaveType";

        // Result: employeeId → list of { LeaveType, Days }
        var buckets = new Dictionary<int, Dictionary<string, decimal>>();

        foreach (var g in groups)
        {
            int startDay = g.Key;
            var (from, to) = GetSalaryPeriodRange(year, monthNum, startDay);
            var groupIds   = g.Select(x => x.Employee.Id).ToList();

            var rows = await conn.QueryAsync<dynamic>(aggregateSql, new
            {
                Ids  = groupIds,
                From = from,
                To   = to
            });

            foreach (var r in rows)
            {
                int empId = (int)r.EmployeeId;
                string type = (string)r.LeaveType;
                decimal days = (decimal)r.Days;

                if (!buckets.TryGetValue(empId, out var dict))
                {
                    dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                    buckets[empId] = dict;
                }

                if (dict.ContainsKey(type)) dict[type] += days;
                else                        dict[type]  = days;
            }
        }

        // ── Build response ──
        var result = new List<object>();

        foreach (var emp in employees)
        {
            var dict = buckets.TryGetValue(emp.Employee.Id, out var d)
                ? d
                : new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            decimal total = dict.Values.Sum();

            // Skip employees with no leave (keeps payload small)
            if (total <= 0) continue;

            var byCategory = dict
                .Where(kv => kv.Value > 0)
                .Select(kv => new
                {
                    category = kv.Key,
                    days     = Math.Round(kv.Value, 2)
                })
                .ToList();

            result.Add(new
            {
                employeeId  = emp.Employee.Id,
                exists      = true,
                locked      = locked,
                processing  = processing,
                total       = Math.Round(total, 2),
                byCategory  = byCategory
            });
        }

        return Ok(new { success = true, data = result });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Bulk leave summary failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
// ============================================================
// WORKING DAYS — single month
// ============================================================
[HttpGet("report/workingdays")]
public async Task<IActionResult> GetWorkingDays(
    [FromQuery] int employeeId,
    [FromQuery] int year,
    [FromQuery] string monthName)
{
    try
    {
        if (employeeId <= 0 || year <= 0 || string.IsNullOrWhiteSpace(monthName))
            return BadRequest(new { success = false, message = "Invalid params" });

        int monthNum = GetMonthNumber(monthName);
        if (monthNum == 0) return BadRequest(new { success = false, message = "Invalid month" });

        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId);
        if (employee == null) return NotFound(new { success = false, message = "Employee not found" });

        var settings = await _companySettingsService.GetCompanySettingsAsync(employee.CompanyId);
        var department = await _context.Company_Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == employee.DepartmentId);
        int startDay = (department != null &&
            department.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
            ? settings.SalaryStartDate_Office : settings.SalaryStartDate_Wages;

        var (startDate, endDate) = GetSalaryPeriodRange(year, monthNum, startDay);

        using var conn = new SqlConnection(_connectionString);
        const string sql = @"
            SELECT COUNT(*)
            FROM TimeSheet
            WHERE EmployeeId = @Emp
              AND FirstInDate >= @From
              AND FirstInDate <= @To
              AND FirstInTime  !='00:00:00'
              AND (DayType IS NULL
                   OR DayType NOT IN ('No Pay', 'No Pay - Half'))";

        var total = await conn.ExecuteScalarAsync<int>(sql,
            new { Emp = employeeId, From = startDate, To = endDate });

        return Ok(new { success = true, data = new { totalWorkingDays = total } });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Working days failed");
        return StatusCode(500, new { success = false, message = ex.Message });
    }
}
        [HttpGet("shift-block")]
        public async Task<IActionResult> GetShiftBlock([FromQuery] int employeeId, [FromQuery] string date)
        {
            if (employeeId <= 0 || string.IsNullOrWhiteSpace(date))
                return BadRequest(new { success = false, message = "employeeId and date are required" });

            if (!DateTime.TryParse(date, out var d))
                return BadRequest(new { success = false, message = "Invalid date format" });

            try
            {
                using var connection = new SqlConnection(_connectionString);

                var row = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT
                        EmployeeId              AS EmployeeId,
                        [Date]                  AS [Date],
                        ShiftBlockIdFirstShift  AS ShiftBlockIdFirstShift,
                        ShiftBlockIdSecondShift AS ShiftBlockIdSecondShift
                    FROM ShiftsTimesheet
                    WHERE EmployeeId = @EmployeeId AND [Date] = @Date",
                    new { EmployeeId = employeeId, Date = d.Date });

                if (row == null)
                {
                    return Ok(new { success = true, data = (object)null, hasData = false });
                }

                return Ok(new
                {
                    success = true,
                    hasData = true,
                    data = new
                    {
                        employeeId = (int)row.EmployeeId,
                        date = ((DateTime)row.Date).ToString("yyyy-MM-dd"),
                        shiftBlockIdFirstShift = (int?)row.ShiftBlockIdFirstShift,
                        shiftBlockIdSecondShift = (int?)row.ShiftBlockIdSecondShift
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching shift block");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ============================================================
        // TOGGLE Night Date
        // ============================================================
        [HttpPost("night-date")]
        public async Task<IActionResult> SaveNightDate([FromBody] NightDateRequest request)
        {
            if (request == null || request.EmployeeId <= 0 || string.IsNullOrWhiteSpace(request.Date))
                return BadRequest(new { success = false, message = "employeeId and date are required" });

            if (!DateTime.TryParse(request.Date, out var date))
                return BadRequest(new { success = false, message = "Invalid date format (expected YYYY-MM-DD)" });

            try
            {
                using var connection = new SqlConnection(_connectionString);

                int userId = 0;
                var userIdClaim = User?.FindFirst("UserId")?.Value ?? User?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out userId);

                if (request.IsFullNight)
                {
                    var sql = @"
                        MERGE NightDates AS target
                        USING (SELECT @EmployeeId AS EmployeeId, @Date AS [Date]) AS src
                            ON target.EmployeeId = src.EmployeeId AND target.[Date] = src.[Date]
                        WHEN MATCHED THEN
                            UPDATE SET IsFullNight = 1, Remarks = @Remarks
                        WHEN NOT MATCHED THEN
                            INSERT (EmployeeId, [Date], IsFullNight, Remarks, CreatedDate, CreatedBy)
                            VALUES (@EmployeeId, @Date, 1, @Remarks, GETDATE(), @UserId);";

                    await connection.ExecuteAsync(sql, new
                    {
                        EmployeeId = request.EmployeeId,
                        Date = date.Date,
                        Remarks = request.Remarks,
                        UserId = userId > 0 ? (int?)userId : null
                    });
                }
                else
                {
                    await connection.ExecuteAsync(
                        "DELETE FROM NightDates WHERE EmployeeId = @EmployeeId AND [Date] = @Date",
                        new { EmployeeId = request.EmployeeId, Date = date.Date });
                }

                if (!string.IsNullOrWhiteSpace(request.PeriodKey))
                {
                    var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
                    await TriggerProcessQueue(request.EmployeeId, request.PeriodKey, ip);
                }

                return Ok(new
                {
                    success = true,
                    message = request.IsFullNight ? "Night date saved" : "Night date removed",
                    employeeId = request.EmployeeId,
                    date = date.Date.ToString("yyyy-MM-dd"),
                    isFullNight = request.IsFullNight
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving night date");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods

        private async Task LogAttendanceAction(object entry, string action, int userId, string ipAddress)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sql = @"
                    INSERT INTO AttendanceManualLog (
                        EmployeeId, Action, RequestData, ResponseStatus,
                        Timestamp, IpAddress, UserId
                    ) VALUES (
                        @EmployeeId, @Action, @RequestData, 'Success',
                        GETDATE(), @IpAddress, @UserId)";

                string requestData = System.Text.Json.JsonSerializer.Serialize(entry);
                int employeeId = 0;

                if (entry is AttendanceDto dto) employeeId = dto.EmployeeId;
                else if (entry is ManualAttendanceEntryDto manual) employeeId = manual.EmployeeId;

                await connection.ExecuteAsync(sql, new
                {
                    EmployeeId = employeeId,
                    Action = action,
                    RequestData = requestData,
                    IpAddress = ipAddress ?? "Unknown",
                    UserId = userId > 0 ? userId : (int?)null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging attendance action to AttendanceManualLog");
            }
        }
                // ===== Validation Helper: Weekly Day Off / Half Day Off =====
        private (bool IsValid, string? ErrorMessage) ValidateGeneralSettings(ShiftBlock shiftBlock)
        {
            if (!string.IsNullOrWhiteSpace(shiftBlock.FirstWeeklyDayOffDay) &&
                !string.IsNullOrWhiteSpace(shiftBlock.FirstWeeklyHalfDayOffDay) &&
                string.Equals(shiftBlock.FirstWeeklyDayOffDay.Trim(),
                              shiftBlock.FirstWeeklyHalfDayOffDay.Trim(),
                              StringComparison.OrdinalIgnoreCase))
            {
                return (false, "First Weekly Day Off and First Weekly Half Day Off cannot be on the same day.");
            }

            // NEW: Late Deduction Half Day requires a Half Day Off Day to be set
            if (shiftBlock.IsLateDeductionHalfDay &&
                string.IsNullOrWhiteSpace(shiftBlock.FirstWeeklyHalfDayOffDay))
            {
                return (false, "Late Deduction for General Half Day requires a Weekly Half Day Off Day to be selected.");
            }

            return (true, null);
        }  
        private async Task TriggerProcessQueue(int employeeId, string periodKey, string ipAddress = "Unknown")
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                // ⚠️ Stale timeout: if a row has been in "processing = 1, process_end = 0"
                //    for more than 10 minutes, consider the worker crashed and release it.
                const int staleMinutes = 10;

                // 1) Clear stale processing rows
                var clearStaleSql = @"
            UPDATE process_queue
            SET 
                processing = 1,
                process_end = 1,
                processEnd_stageTwo_date = GETDATE()
            WHERE emp_systemID = @EmployeeId
              AND period_ = @PeriodKey
              AND processing = 1
              AND process_end = 0
              AND processStart_date IS NOT NULL
              AND DATEDIFF(MINUTE, processStart_date, GETDATE()) > @StaleMinutes";

                var staleCleared = await connection.ExecuteAsync(clearStaleSql, new
                {
                    EmployeeId = employeeId,
                    PeriodKey = periodKey,
                    StaleMinutes = staleMinutes
                });

                if (staleCleared > 0)
                {
                    _logger.LogWarning(
                        "Cleared {Count} stale processing row(s) for employee {EmployeeId}, period {PeriodKey} (older than {Minutes} min)",
                        staleCleared, employeeId, periodKey, staleMinutes);
                }

                // 2) Skip if there's still an actively pending row (processing=0, end=0)
                var pendingCount = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM process_queue
            WHERE emp_systemID = @EmployeeId
              AND period_ = @PeriodKey
              AND processing = 0
              AND process_end = 0",
                    new { EmployeeId = employeeId, PeriodKey = periodKey });

                if (pendingCount > 0)
                {
                    _logger.LogInformation(
                        "Pending queue row already exists for employee {EmployeeId}, period {PeriodKey}. Skipping insert.",
                        employeeId, periodKey);
                    return;
                }

                // 3) Skip if there's still an ACTIVE (non-stale) processing row (processing=1, end=0)
                var processingCount = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM process_queue
            WHERE emp_systemID = @EmployeeId
              AND period_ = @PeriodKey
              AND processing = 1
              AND process_end = 0",
                    new { EmployeeId = employeeId, PeriodKey = periodKey });

                if (processingCount > 0)
                {
                    _logger.LogInformation(
                        "Active processing row exists for employee {EmployeeId}, period {PeriodKey}. Skipping insert.",
                        employeeId, periodKey);
                    return;
                }

                // 4) Nothing pending, nothing processing → insert new row
                var empEmpId = await connection.ExecuteScalarAsync<string>(
                    "SELECT EmployeeNo FROM Employees WHERE Id = @EmployeeId",
                    new { EmployeeId = employeeId }) ?? employeeId.ToString();

                int userId = 0;
                var userIdClaim = User?.FindFirst("UserId")?.Value ?? User?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim)) int.TryParse(userIdClaim, out userId);

                var insertSql = @"
            INSERT INTO process_queue (
                emp_systemID, emp_empID, period_, process_status,
                processing, process_end, request_date, request_ip,
                requset_from, processStart_date, processEnd_stageOne_date,
                processEnd_stageTwo_date, request_by)
            VALUES (
                @EmployeeId, @EmpEmpId, @PeriodKey, 0,
                0, 0, GETDATE(), @IpAddress,
                'Attendance Module', GETDATE(), GETDATE(),
                GETDATE(), @UserId)";

                await connection.ExecuteAsync(insertSql, new
                {
                    EmployeeId = employeeId,
                    EmpEmpId = empEmpId,
                    PeriodKey = periodKey,
                    IpAddress = ipAddress,
                    UserId = userId > 0 ? userId.ToString() : "System"
                });

                _logger.LogInformation(
                    "Inserted new process queue row for employee {EmployeeId} (emp_empID={EmpEmpId}), period {PeriodKey}",
                    employeeId, empEmpId, periodKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error triggering process queue for employee {EmployeeId}, period {PeriodKey}",
                    employeeId, periodKey);
            }
        }
        private async Task<bool> IsSalaryPeriodLocked(string periodKey)
        {
            try
            {
                if (string.IsNullOrEmpty(periodKey)) return false;

                var parts = periodKey.Split('-');
                if (parts.Length < 2) return false;

                if (!int.TryParse(parts[0], out int year)) return false;

                int month = 0;
                if (!int.TryParse(parts[1], out month))
                {
                    var monthMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                    {
                        {"january", 1}, {"jan", 1}, {"february", 2}, {"feb", 2},
                        {"march", 3}, {"mar", 3}, {"april", 4}, {"apr", 4},
                        {"may", 5}, {"june", 6}, {"jun", 6}, {"july", 7},
                        {"jul", 7}, {"august", 8}, {"aug", 8}, {"september", 9},
                        {"sep", 9}, {"october", 10}, {"oct", 10}, {"november", 11},
                        {"nov", 11}, {"december", 12}, {"dec", 12}
                    };

                    if (!monthMap.TryGetValue(parts[1], out month))
                    {
                        return false;
                    }
                }

                using var connection = new SqlConnection(_connectionString);
                var sql = @"
                    SELECT locked 
                    FROM Payroll_Process 
                    WHERE year = @Year AND month = @Month";

                var locked = await connection.QueryFirstOrDefaultAsync<bool?>(
                    sql, new { Year = year, Month = GetMonthName(month) });

                return locked ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking salary period lock for {PeriodKey}", periodKey);
                return false;
            }
        }

        private string GetMonthName(int month)
        {
            return new DateTime(2020, month, 1).ToString("MMMM");
        }

        private string GetPeriodKeyFromDate(DateTime date)
        {
            return $"{date.Year}-{date:MMMM}";
        }

        private int GetMonthNumber(string monthName)
        {
            if (string.IsNullOrEmpty(monthName)) return 0;

            var monthMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {"january", 1}, {"jan", 1},
                {"february", 2}, {"feb", 2},
                {"march", 3}, {"mar", 3},
                {"april", 4}, {"apr", 4},
                {"may", 5},
                {"june", 6}, {"jun", 6},
                {"july", 7}, {"jul", 7},
                {"august", 8}, {"aug", 8},
                {"september", 9}, {"sep", 9},
                {"october", 10}, {"oct", 10},
                {"november", 11}, {"nov", 11},
                {"december", 12}, {"dec", 12}
            };

            if (monthMap.TryGetValue(monthName.Trim(), out int monthNumber))
                return monthNumber;

            if (int.TryParse(monthName, out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
                return parsedMonth;

            return 0;
        }

        private int? GetMonthNumberFromName(string monthName)
        {
            if (string.IsNullOrEmpty(monthName)) return null;

            var months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                {"january", 1}, {"jan", 1},
                {"february", 2}, {"feb", 2},
                {"march", 3}, {"mar", 3},
                {"april", 4}, {"apr", 4},
                {"may", 5},
                {"june", 6}, {"jun", 6},
                {"july", 7}, {"jul", 7},
                {"august", 8}, {"aug", 8},
                {"september", 9}, {"sep", 9},
                {"october", 10}, {"oct", 10},
                {"november", 11}, {"nov", 11},
                {"december", 12}, {"dec", 12}
            };

            if (months.TryGetValue(monthName.Trim(), out int monthNumber))
                return monthNumber;

            if (int.TryParse(monthName, out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
                return parsedMonth;

            return null;
        }

        private string GetMonthNameFromNumber(int monthNumber)
        {
            if (monthNumber < 1 || monthNumber > 12)
                return "Invalid Month";

            return new DateTime(2020, monthNumber, 1).ToString("MMMM");
        }

        #endregion
    }
}