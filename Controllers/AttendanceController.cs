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

namespace TRIVORA_API.Controllers
{
    // ========== DTO CLASSES ==========
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
                {
                    return BadRequest(new { success = false, message = "EmployeeId and PeriodKey are required" });
                }

                using var connection = new SqlConnection(_connectionString);

                var periodParts = periodKey.Split('-');
                if (periodParts.Length < 2)
                {
                    return BadRequest(new { success = false, message = "Invalid PeriodKey format" });
                }

                if (!int.TryParse(periodParts[0], out int year))
                {
                    return BadRequest(new { success = false, message = "Invalid year in PeriodKey" });
                }

                int month = GetMonthNumber(periodParts[1]);
                if (month == 0)
                {
                    return BadRequest(new { success = false, message = "Invalid month in PeriodKey" });
                }

                var employee = await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    return NotFound(new { success = false, message = "Employee not found" });
                }

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
                _logger.LogInformation($"📥 Received manual entry: {System.Text.Json.JsonSerializer.Serialize(entry)}");

                string ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                if (entry == null || entry.EmployeeId <= 0)
                    return BadRequest(new { success = false, message = "Invalid data" });

                if (entry.DateTime == DateTime.MinValue)
                {
                    return BadRequest(new { success = false, message = "DateTime is required" });
                }

                if (string.IsNullOrEmpty(entry.PeriodKey))
                {
                    entry.PeriodKey = $"{entry.DateTime.Year}-{entry.DateTime:MMMM}";
                }

                // Get employee
                var employee = await _context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == entry.EmployeeId);

                if (employee == null)
                {
                    return NotFound(new { success = false, message = $"Employee with ID {entry.EmployeeId} not found" });
                }

                using var connection = new SqlConnection(_connectionString);

                // Check if record exists for this datetime
                var checkSql = "SELECT COUNT(*) FROM Attendance WHERE datetime = @DateTime AND employee_id = @EmployeeId";
                var exists = await connection.ExecuteScalarAsync<int>(checkSql, new
                {
                    DateTime = entry.DateTime,
                    EmployeeId = entry.EmployeeId
                });

                int rowsAffected;
                if (exists > 0)
                {
                    // UPDATE existing record - update the datetime and input_datetime
                    var updateSql = @"
                UPDATE Attendance 
                SET 
                    datetime = @DateTime,
                    input_datetime = GETDATE()
                WHERE datetime = @DateTime AND employee_id = @EmployeeId";

                    rowsAffected = await connection.ExecuteAsync(updateSql, new
                    {
                        EmployeeId = entry.EmployeeId,
                        DateTime = entry.DateTime
                    });
                    _logger.LogInformation($"✅ Updated {rowsAffected} record(s)");
                }
                else
                {
                    // INSERT new record
                    var insertSql = @"
                INSERT INTO Attendance (
                    employee_id,
                    company_id,
                    datetime,
                    input_type,
                    input_datetime
                ) VALUES (
                    @EmployeeId,
                    @CompanyId,
                    @DateTime,
                    'Manual',
                    GETDATE()
                )";

                    rowsAffected = await connection.ExecuteAsync(insertSql, new
                    {
                        EmployeeId = entry.EmployeeId,
                        CompanyId = employee.CompanyId,
                        DateTime = entry.DateTime
                    });
                    _logger.LogInformation($"✅ Inserted {rowsAffected} record(s)");
                }

                if (rowsAffected > 0)
                {
                    await TriggerProcessQueue(entry.EmployeeId, entry.PeriodKey, ipAddress);
                    return Ok(new { success = true, message = "Attendance saved successfully" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to save attendance" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving manual attendance");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // DELETE: api/attendance/manual-entry/{id}
        [HttpDelete("manual-entry/{id}")]
        public async Task<IActionResult> DeleteManualEntry(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var getEntrySql = @"
                    SELECT 
                        id as Id,
                        employee_id as EmployeeId,
                        company_id as CompanyId,
                        datetime as DateTime,
                        input_type as InputType
                    FROM Attendance
                    WHERE id = @Id";

                var entry = await connection.QueryFirstOrDefaultAsync<AttendanceDto>(getEntrySql, new { Id = id });

                if (entry == null)
                {
                    return NotFound(new { success = false, message = "Attendance entry not found" });
                }

                string periodKey = GetPeriodKeyFromDate(entry.DateTime);

                var isLocked = await IsSalaryPeriodLocked(periodKey);
                if (isLocked)
                {
                    return BadRequest(new { success = false, message = "Cannot delete attendance for locked salary period" });
                }

                int userId = 0;
                var userIdClaim = User.FindFirst("UserId")?.Value
                    ?? User.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }

                string ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                await LogAttendanceAction(entry, "DELETE", userId, ipAddress);

                var deleteSql = "DELETE FROM Attendance WHERE id = @Id";
                var rowsAffected = await connection.ExecuteAsync(deleteSql, new { Id = id });

                if (rowsAffected > 0)
                {
                    await TriggerProcessQueue(entry.EmployeeId, periodKey, ipAddress);
                    return Ok(new { success = true, message = "Attendance deleted successfully and recalculation queued" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to delete attendance entry" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting manual attendance entry {Id}", id);
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/attendance/check-period-lock
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

        // GET: api/attendance/employee-shift/{employeeId}
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
                {
                    return NotFound(new { success = false, message = "Employee not found" });
                }

                return Ok(new { success = true, data = employeeShift });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/attendance/shift-blocks
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

        // Get current processing salary period
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

        // GET: api/attendance/employee-salary-start-date/{employeeId}
        [HttpGet("employee-salary-start-date/{employeeId}")]
        public async Task<IActionResult> GetEmployeeSalaryStartDate(int employeeId)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    return NotFound(new { success = false, message = "Employee not found" });
                }

                int salaryStartDay = 1;

                return Ok(new { success = true, data = salaryStartDay });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("timesheet")]
        public async Task<IActionResult> GetTimesheet(
    [FromQuery] int employeeId,
    [FromQuery] string year,
    [FromQuery] string monthName)
        {
            try
            {
                var employee = await _context.Employees
                    .Join(_context.Company_Departments,
                          e => e.DepartmentId,
                          d => d.Id,
                          (e, d) => new { Employee = e, Department = d })
                    .Where(x => x.Employee.Id == employeeId)
                    .Select(x => new
                    {
                        Employee = x.Employee,
                        DepartmentName = x.Department.DepartmentName,
                        DepartmentId = x.Department.Id
                    })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return NotFound(new { success = false, message = "Employee not found" });
                }

                var companySettings = await _companySettingsService.GetCompanySettingsAsync(employee.Employee.CompanyId);

                if (companySettings == null)
                {
                    return BadRequest(new { success = false, message = "Company settings not found" });
                }

                int salaryStartDay;
                if (!string.IsNullOrEmpty(employee.DepartmentName) &&
                    employee.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase))
                {
                    salaryStartDay = companySettings.SalaryStartDate_Office;
                }
                else
                {
                    salaryStartDay = companySettings.SalaryStartDate_Wages;
                }

                int targetYear = string.IsNullOrEmpty(year) ? DateTime.Now.Year : int.Parse(year);
                int targetMonth = GetMonthNumberFromName(monthName) ?? DateTime.Now.Month;

                if (targetMonth == 0)
                {
                    return BadRequest(new { success = false, message = "Invalid month name provided" });
                }

                DateTime startDate, endDate;

                if (salaryStartDay == 1)
                {
                    startDate = new DateTime(targetYear, targetMonth, 1);
                    endDate = new DateTime(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
                }
                else
                {
                    startDate = new DateTime(targetYear, targetMonth, salaryStartDay);
                    var nextMonth = startDate.AddMonths(1);
                    endDate = new DateTime(nextMonth.Year, nextMonth.Month, salaryStartDay).AddDays(-1);

                    if (endDate < startDate)
                    {
                        endDate = new DateTime(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
                    }
                }

                var timesheetData = new List<object>();
                var currentDate = startDate;

                // ⭐ Load timesheets
                var existingTimesheets = await _context.TimeSheets
                    .Where(t => t.EmployeeId == employeeId &&
                        ((t.FirstInDate >= startDate && t.FirstInDate <= endDate) ||
                         (t.SecondInDate >= startDate && t.SecondInDate <= endDate)))
                    .ToListAsync();

                // ⭐ Load shift blocks (pre-saved via ShiftsTimesheet)
                var savedShiftBlocks = await _context.ShiftsTimesheet
                    .Where(s => s.EmployeeId == employeeId &&
                                s.Date >= startDate && s.Date <= endDate)
                    .ToListAsync();

                var shiftBlockDict = await _context.ShiftBlocks
    .Where(sb => sb.CompanyId == employee.Employee.CompanyId)
    .ToDictionaryAsync(sb => sb.Id, sb => sb.DayType2);

                var leaveRecords = await _context.Leave
                    .Where(l => l.EmployeeId == employeeId &&
                            l.LeaveDate >= startDate && l.LeaveDate <= endDate &&
                            l.Status == "Approved")
                    .Select(l => new
                    {
                        l.LeaveDate,
                        l.LeaveType,
                        l.IsHalfDay,
                        l.Status
                    })
                    .ToListAsync();

                var otherLeaves = await _context.OtherLeaves
                    .Where(ol => ol.EmployeeId == employeeId &&
                                ol.FromDate <= endDate && ol.ToDate >= startDate &&
                                (ol.Status == "Approved" || ol.Status == "Pending"))
                    .ToListAsync();

                var employeeShift = await (from e in _context.Employees
                                           join sb in _context.ShiftBlocks on e.ShiftBlockId equals sb.Id into shiftJoin
                                           from shift in shiftJoin.DefaultIfEmpty()
                                           where e.Id == employeeId
                                           select new
                                           {
                                               ShiftBlockId = e.ShiftBlockId,
                                               ShiftType = shift != null ? shift.ShiftType : "Single"
                                           }).FirstOrDefaultAsync();

                string employeeShiftType = employeeShift?.ShiftType ?? "Single";

                while (currentDate <= endDate)
                {
                    var existingTimesheet = existingTimesheets
                        .FirstOrDefault(t =>
                            (t.FirstInDate.HasValue && t.FirstInDate.Value.Date == currentDate.Date) ||
                            (t.SecondInDate.HasValue && t.SecondInDate.Value.Date == currentDate.Date));

                    var savedShiftBlock = existingTimesheet == null
                        ? savedShiftBlocks.FirstOrDefault(s => s.Date.Date == currentDate.Date)
                        : null;

                    // Determine which shift block applies to this day
int? activeShiftBlockId =
    existingTimesheet?.FirstShiftBlockId
    ?? savedShiftBlock?.ShiftBlockIdFirstShift
    ?? employeeShift?.ShiftBlockId;

// Night = DayType2 says "Next day"
string activeDayType2 = activeShiftBlockId.HasValue && shiftBlockDict.TryGetValue(activeShiftBlockId.Value, out var dt)
    ? dt
    : null;

var hasFullNight = string.Equals(activeDayType2, "Next day", StringComparison.OrdinalIgnoreCase);

                    
                    // ⭐ Default OutDate: next day ONLY if Full Night, otherwise same day
                    var defaultOutDate = hasFullNight ? currentDate.AddDays(1) : currentDate;

                    var dayLeaves = leaveRecords
                        .Where(l => l.LeaveDate.Date == currentDate.Date)
                        .ToList();

                    var isBlockedByOtherLeave = otherLeaves
                        .Any(ol => currentDate >= ol.FromDate && currentDate <= ol.ToDate);

                    var hasAnnualLeave = dayLeaves.Any(l => l.LeaveType == "Annual" && !l.IsHalfDay);
                    var hasAnnualHalfLeave = dayLeaves.Any(l => l.LeaveType == "Annual" && l.IsHalfDay);
                    var hasCasualLeave = dayLeaves.Any(l => l.LeaveType == "Casual" && !l.IsHalfDay);
                    var hasCasualHalfLeave = dayLeaves.Any(l => l.LeaveType == "Casual" && l.IsHalfDay);
                    var hasSickLeave = dayLeaves.Any(l => l.LeaveType == "Sick" && !l.IsHalfDay);
                    var hasSickHalfLeave = dayLeaves.Any(l => l.LeaveType == "Sick" && l.IsHalfDay);

                    var otherLeaveType = "-";
                    if (hasFullNight)
{
    hasAnnualHalfLeave = false;
    hasCasualHalfLeave = false;
    hasSickHalfLeave = false;
}
                    if (isBlockedByOtherLeave)
                    {
                        var blockingLeave = otherLeaves.FirstOrDefault(ol =>
                            currentDate >= ol.FromDate && currentDate <= ol.ToDate);
                        otherLeaveType = blockingLeave?.LeaveType ?? "Blocked";
                    }

                    var dayData = new
                    {
                        Date = currentDate,
                        DayName = currentDate.DayOfWeek.ToString(),

                        TimesheetRecord = existingTimesheet != null
                            ? new
                            {
                                ShiftBlockId = existingTimesheet.FirstShiftBlockId,
                                InTime = existingTimesheet.FirstInTime,
                                OutDate = (DateTime?)(existingTimesheet.FirstOutDate ?? defaultOutDate),
                                OutTime = existingTimesheet.FirstOutTime,
                                DayType = existingTimesheet.DayType,
                                WorkHours = existingTimesheet.WorkHours,

                                FirstShiftBlockId = existingTimesheet.FirstShiftBlockId,
                                FirstInTime = existingTimesheet.FirstInTime,
                                FirstOutDate = (DateTime?)(existingTimesheet.FirstOutDate ?? defaultOutDate),
                                FirstOutTime = existingTimesheet.FirstOutTime,
                                SecondShiftBlockId = existingTimesheet.SecondShiftBlockId,
                                SecondInTime = existingTimesheet.SecondInTime,
                                SecondOutDate = (DateTime?)(existingTimesheet.SecondOutDate ?? defaultOutDate),
                                SecondOutTime = existingTimesheet.SecondOutTime,

                                LateHours = existingTimesheet.LateHours,
                                OtHoursNormal = existingTimesheet.OtHoursNormal,
                                OtHoursDouble = existingTimesheet.OtHoursPoya,
                                PayType = (string?)existingTimesheet.PayType,
                                FromShiftBlock = false
                            }
                            : savedShiftBlock != null
                                ? new
                                {
                                    ShiftBlockId = savedShiftBlock.ShiftBlockIdFirstShift,
                                    InTime = (TimeSpan?)null,
                                    OutDate = (DateTime?)defaultOutDate,
                                    OutTime = (TimeSpan?)null,
                                    DayType = "Normal",
                                    WorkHours = (TimeSpan?)null,

                                    FirstShiftBlockId = savedShiftBlock.ShiftBlockIdFirstShift,
                                    FirstInTime = (TimeSpan?)null,
                                    FirstOutDate = (DateTime?)defaultOutDate,
                                    FirstOutTime = (TimeSpan?)null,
                                    SecondShiftBlockId = savedShiftBlock.ShiftBlockIdSecondShift,
                                    SecondInTime = (TimeSpan?)null,
                                    SecondOutDate = (DateTime?)defaultOutDate,
                                    SecondOutTime = (TimeSpan?)null,

                                    LateHours = (TimeSpan?)null,
                                    OtHoursNormal = (TimeSpan?)null,
                                    OtHoursDouble = (TimeSpan?)null,
                                    PayType = (string?)null,
                                    FromShiftBlock = true
                                }
                                : null,

                        ActiveLeave = dayLeaves,
                        IsBlocked = isBlockedByOtherLeave,
                        OtherLeaveType = otherLeaveType,
                        HasFullNight = hasFullNight,
                        HasAnnualLeave = hasAnnualLeave,
                        HasAnnualHalfLeave = hasAnnualHalfLeave,
                        HasCasualLeave = hasCasualLeave,
                        HasCasualHalfLeave = hasCasualHalfLeave,
                        HasSickLeave = hasSickLeave,
                        HasSickHalfLeave = hasSickHalfLeave,
                        WorkHours = existingTimesheet?.WorkHours?.ToString(@"hh\:mm") ?? "00:00",
                        LateHours = existingTimesheet?.LateHours?.ToString(@"hh\:mm") ?? "00:00",
                        OtNormal = existingTimesheet?.OtHoursNormal?.ToString(@"hh\:mm") ?? "00:00",
                        OtDouble = existingTimesheet?.OtHoursPoya?.ToString(@"hh\:mm") ?? "00:00",
                        IsWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday,
                        IsHoliday = false,
                        SalaryStartDayUsed = salaryStartDay
                    };

                    timesheetData.Add(dayData);
                    currentDate = currentDate.AddDays(1);
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        Days = timesheetData,
                        StartDate = startDate,
                        EndDate = endDate,
                        EmployeeShiftType = employeeShiftType,
                        PeriodInfo = new
                        {
                            Year = targetYear,
                            Month = targetMonth,
                            MonthName = GetMonthNameFromNumber(targetMonth),
                            SalaryStartDay = salaryStartDay,
                            DepartmentId = employee.DepartmentId,
                            DepartmentName = employee.DepartmentName ?? "No Department",
                            DepartmentType = !string.IsNullOrEmpty(employee.DepartmentName) &&
                                employee.DepartmentName.Equals("Office", StringComparison.OrdinalIgnoreCase)
                                ? "Office" : "Wages",
                            SalaryStartDate_Office = companySettings.SalaryStartDate_Office,
                            SalaryStartDate_Wages = companySettings.SalaryStartDate_Wages
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
        // SAVE / UPDATE SHIFT BLOCK for a single day
        // POST /api/attendance/shift-block
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

                // ---------- UPSERT ----------
                // If a row exists for (EmployeeId, Date) → UPDATE the shift columns.
                // Else → INSERT a new row.
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

                // ---------- Trigger process queue ----------
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
        // GET shift block for a single day
        // GET /api/attendance/shift-block?employeeId=1&date=2025-04-15
        // ============================================================
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
                    // ⭐ Return a structured "no data" response the frontend can handle
                    return Ok(new
                    {
                        success = true,
                        data = (object)null,
                        hasData = false
                    });
                }

                // ⭐ Project to camelCase explicitly
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
        // TOGGLE Night Date for an employee + day
        // POST /api/attendance/night-date
        // ============================================================
        public class NightDateRequest
        {
            public int EmployeeId { get; set; }
            public string Date { get; set; }         // 'YYYY-MM-DD'
            public bool IsFullNight { get; set; }
            public string? PeriodKey { get; set; }   // '2025-April' → triggers queue
            public string? Remarks { get; set; }
        }

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
                    // UPSERT: insert or update the row
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
                    // Unchecked → delete the row
                    await connection.ExecuteAsync(
                        "DELETE FROM NightDates WHERE EmployeeId = @EmployeeId AND [Date] = @Date",
                        new { EmployeeId = request.EmployeeId, Date = date.Date });
                }

                // Trigger process queue
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

        // ============================================================
        // REQUEST MODEL
        // ============================================================
        public class SaveShiftBlockRequest
        {
            public int EmployeeId { get; set; }
            public string Date { get; set; }                          // 'YYYY-MM-DD'
            public int ShiftBlockIdFirstShift { get; set; }           // Single-shift employees → this
            public int? ShiftBlockIdSecondShift { get; set; }         // Multi-shift employees → this
            public string PeriodKey { get; set; }                     // '2025-April' → for TriggerProcessQueue
        }


        #region Helper Methods

        private async Task LogAttendanceAction(object entry, string action, int userId, string ipAddress)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sql = @"
                    INSERT INTO AttendanceManualLog (
                        EmployeeId,
                        Action,
                        RequestData,
                        ResponseStatus,
                        Timestamp,
                        IpAddress,
                        UserId
                    ) VALUES (
                        @EmployeeId,
                        @Action,
                        @RequestData,
                        'Success',
                        GETDATE(),
                        @IpAddress,
                        @UserId
                    )";

                string requestData = System.Text.Json.JsonSerializer.Serialize(entry);
                int employeeId = 0;

                if (entry is AttendanceDto dto)
                {
                    employeeId = dto.EmployeeId;
                }
                else if (entry is ManualAttendanceEntryDto manual)
                {
                    employeeId = manual.EmployeeId;
                }

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

        private async Task TriggerProcessQueue(int employeeId, string periodKey, string ipAddress = "Unknown")
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                // Check if there's already a PENDING record (processing = 0 AND process_end = 0)
                var pendingCheckSql = @"
            SELECT COUNT(*) 
            FROM process_queue 
            WHERE emp_systemID = @EmployeeId 
                AND period_ = @PeriodKey 
                AND processing = 0 
                AND process_end = 0";

                var pendingCount = await connection.ExecuteScalarAsync<int>(pendingCheckSql, new { EmployeeId = employeeId, PeriodKey = periodKey });

                if (pendingCount > 0)
                {
                    _logger.LogInformation($"Pending process queue record already exists for employee {employeeId}, period {periodKey}. Skipping new insert.");
                    return;
                }

                // Check if there's already a PROCESSING record (processing = 1 AND process_end = 0)
                var processingCheckSql = @"
            SELECT COUNT(*) 
            FROM process_queue 
            WHERE emp_systemID = @EmployeeId 
                AND period_ = @PeriodKey 
                AND processing = 1 
                AND process_end = 0";

                var processingCount = await connection.ExecuteScalarAsync<int>(processingCheckSql, new { EmployeeId = employeeId, PeriodKey = periodKey });

                if (processingCount > 0)
                {
                    _logger.LogInformation($"Processing record already exists for employee {employeeId}, period {periodKey}. Skipping new insert.");
                    return;
                }

                // Get the employee's emp_empID (EmployeeNo) from the Employees table
                var getEmpIdSql = "SELECT EmployeeNo FROM Employees WHERE Id = @EmployeeId";
                var empEmpId = await connection.ExecuteScalarAsync<string>(getEmpIdSql, new { EmployeeId = employeeId }) ?? employeeId.ToString();

                int userId = 0;
                var userIdClaim = User?.FindFirst("UserId")?.Value
                    ?? User?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    int.TryParse(userIdClaim, out userId);
                }

                var sql = @"
            INSERT INTO process_queue (
                emp_systemID,
                emp_empID,
                period_,
                process_status,
                processing,
                process_end,
                request_date,
                request_ip,
                requset_from,
                processStart_date,
                processEnd_stageOne_date,
                processEnd_stageTwo_date,
                request_by
            ) VALUES (
                @EmployeeId,
                @EmpEmpId,
                @PeriodKey,
                0,  -- 0 = Pending
                0,  -- processing = 0 (not processing yet)
                0,  -- process_end = 0 (not completed)
                GETDATE(),
                @IpAddress,
                'Attendance Module',
                GETDATE(),
                GETDATE(),
                GETDATE(),
                @UserId
            )";

                var parameters = new
                {
                    EmployeeId = employeeId,
                    EmpEmpId = empEmpId,
                    PeriodKey = periodKey,
                    IpAddress = ipAddress,
                    UserId = userId > 0 ? userId.ToString() : "System"
                };

                int rowsAffected = await connection.ExecuteAsync(sql, parameters);
                _logger.LogInformation($"Inserted new process queue record for employee {employeeId}, period {periodKey}, rows affected: {rowsAffected}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering process queue for employee {EmployeeId}, period {PeriodKey}",
                    employeeId, periodKey);
            }
        }
        // GET: api/processqueue/status
        [HttpGet("status")]
        public async Task<IActionResult> GetProcessStatus([FromQuery] int employeeId, [FromQuery] string period)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                // Simple check - just count if there are any active processing records
                var sql = @"
            SELECT COUNT(1) 
            FROM process_queue 
            WHERE emp_systemID = @EmployeeId 
                AND period_ = @Period
                AND (process_end = 0 OR processing = 1)";

                var activeCount = await connection.ExecuteScalarAsync<int>(sql, new { EmployeeId = employeeId, Period = period });

                // Return simple response - just the status
                return Ok(new
                {
                    success = true,
                    isProcessing = activeCount > 0  // true if any record is still processing
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking process status for employee {EmployeeId}, period {Period}", employeeId, period);
                return StatusCode(500, new { success = false, message = ex.Message });
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

                var locked = await connection.QueryFirstOrDefaultAsync<bool?>(sql, new { Year = year, Month = GetMonthName(month) });

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
            {
                return monthNumber;
            }

            if (int.TryParse(monthName, out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
            {
                return parsedMonth;
            }

            return 0;
        }

        private int? GetMonthNumberFromName(string monthName)
        {
            if (string.IsNullOrEmpty(monthName))
                return null;

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
            {
                return monthNumber;
            }

            if (int.TryParse(monthName, out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
            {
                return parsedMonth;
            }

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