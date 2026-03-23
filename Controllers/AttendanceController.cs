using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public AttendanceController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection");
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
                        sb.SecondShiftOutTime
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
                using var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                
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

                // Default to 1 if not specified
                int salaryStartDay = 1;

                return Ok(new { success = true, data = salaryStartDay });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

       
        // GET: api/attendance/timesheet
        [HttpGet("timesheet")]
        public async Task<IActionResult> GetTimesheet([FromQuery] int employeeId, [FromQuery] string year, [FromQuery] string monthName, [FromQuery] int salaryStartDay = 1)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    return NotFound(new { success = false, message = "Employee not found" });
                }
                Console.WriteLine($"MonthName1: {monthName }");
                // Convert month name to month number
                int targetYear = string.IsNullOrEmpty(year) ? DateTime.Now.Year : int.Parse(year);
                int targetMonth = GetMonthNumberFromName(monthName) ?? DateTime.Now.Month;
                 Console.WriteLine($"MonthName2: {GetMonthNumberFromName(monthName) }");
                if (targetMonth == 0)
                {
                    return BadRequest(new { success = false, message = "Invalid month name provided" });
                }

                // Calculate period dates based on salary start day
                DateTime startDate,  endDate;
                
                if (salaryStartDay == 1)
                {
                    // Standard month (1st to end of month)
                    startDate = new DateTime(targetYear, targetMonth, 1);
                    endDate = new DateTime(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
                }
                else
                {
                    // Custom salary period (e.g., 16th to 15th of next month)
                    startDate = new DateTime(targetYear, targetMonth, salaryStartDay);
                    
                    // Calculate end date (day before same day next month)
                    var nextMonth = startDate.AddMonths(1);
                    endDate = new DateTime(nextMonth.Year, nextMonth.Month, salaryStartDay).AddDays(-1);
                    
                    // Handle year rollover for December
                    if (endDate.Month != nextMonth.Month)
                    {
                        endDate = new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    }
                }

                var timesheetData = new List<object>();
                var currentDate = startDate;

                // Get existing timesheet records for this period
                var existingTimesheets = await _context.TimeSheets
                    .Where(t => t.EmployeeId == employeeId &&
                            t.FirstInDate >= startDate && t.FirstInDate <= endDate)
                    .ToListAsync();

                // Get leave records for this period
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

                // Get other leaves for this period
                var otherLeaves = await _context.OtherLeaves
                    .Where(ol => ol.EmployeeId == employeeId &&
                                ol.FromDate <= endDate && ol.ToDate >= startDate &&
                                (ol.Status == "Approved" || ol.Status == "Pending"))
                    .ToListAsync();

                // Get employee's shift information by joining with ShiftBlocks
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
                        .FirstOrDefault(t => t.FirstInDate == currentDate.Date);

                    var dayLeaves = leaveRecords
                        .Where(l => l.LeaveDate.Date == currentDate.Date)
                        .ToList();

                    var isBlockedByOtherLeave = otherLeaves
                        .Any(ol => currentDate >= ol.FromDate && currentDate <= ol.ToDate);

                    // Determine leave types for the day
                    var hasAnnualLeave = dayLeaves.Any(l => l.LeaveType == "Annual" && !l.IsHalfDay);
                    var hasAnnualHalfLeave = dayLeaves.Any(l => l.LeaveType == "Annual" && l.IsHalfDay);
                    var hasCasualLeave = dayLeaves.Any(l => l.LeaveType == "Casual" && !l.IsHalfDay);
                    var hasCasualHalfLeave = dayLeaves.Any(l => l.LeaveType == "Casual" && l.IsHalfDay);
                    var hasSickLeave = dayLeaves.Any(l => l.LeaveType == "Sick" && !l.IsHalfDay);
                    var hasSickHalfLeave = dayLeaves.Any(l => l.LeaveType == "Sick" && l.IsHalfDay);

                    // Get other leave type if blocked
                    var otherLeaveType = "-";
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
                        TimesheetRecord = existingTimesheet != null ? new
                        {
                            FirstShiftBlockId = existingTimesheet.FirstShiftBlockId,
                            FirstInTime = existingTimesheet.FirstInTime,
                            FirstOutDate = existingTimesheet.FirstOutDate,
                            FirstOutTime = existingTimesheet.FirstOutTime,
                            SecondShiftBlockId = existingTimesheet.SecondShiftBlockId,
                            SecondInTime = existingTimesheet.SecondInTime,
                            SecondOutDate = existingTimesheet.SecondOutDate,
                            SecondOutTime = existingTimesheet.SecondOutTime,
                            WorkHours = existingTimesheet.WorkHours,
                            LateHours = existingTimesheet.LateHours,
                            OtHoursNormal = existingTimesheet.OtHoursNormal,
                            OtHoursDouble = existingTimesheet.OtHoursDouble,
                            DayType = existingTimesheet.DayType,
                            PayType = existingTimesheet.PayType
                        } : null,
                        ActiveLeave = dayLeaves,
                        IsBlocked = isBlockedByOtherLeave,
                        OtherLeaveType = otherLeaveType,
                        // Leave flags for UI
                        HasAnnualLeave = hasAnnualLeave,
                        HasAnnualHalfLeave = hasAnnualHalfLeave,
                        HasCasualLeave = hasCasualLeave,
                        HasCasualHalfLeave = hasCasualHalfLeave,
                        HasSickLeave = hasSickLeave,
                        HasSickHalfLeave = hasSickHalfLeave,
                        // Calculated fields with safe formatting
                        WorkHours = existingTimesheet?.WorkHours?.ToString(@"hh\:mm") ?? "00:00",
                        LateHours = existingTimesheet?.LateHours?.ToString(@"hh\:mm") ?? "00:00",
                        OtNormal = existingTimesheet?.OtHoursNormal?.ToString(@"hh\:mm") ?? "00:00",
                        OtDouble = existingTimesheet?.OtHoursDouble?.ToString(@"hh\:mm") ?? "00:00",
                        // Additional info
                        IsWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday,
                        IsHoliday = false // You can implement holiday checking here
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
                            SalaryStartDay = salaryStartDay
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        
        // Helper method to convert month name to month number
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

            // Try to parse as number if provided as string number
            if (int.TryParse(monthName, out int parsedMonth) && parsedMonth >= 1 && parsedMonth <= 12)
            {
                return parsedMonth;
            }

            return null;
        }

        // Helper method to convert month number to month name
        private string GetMonthNameFromNumber(int monthNumber)
        {
            if (monthNumber < 1 || monthNumber > 12)
                return "Invalid Month";

            return new DateTime(2020, monthNumber, 1).ToString("MMMM");
        }

        
        
        private async Task ProcessLeaveApplications(int employeeId, int companyId, string userId, 
            DateTime date, List<LeaveApplicationData> leaveApplications)
        {
            if (leaveApplications == null || !leaveApplications.Any())
                return;

            // For now, we'll just handle one leave application per day
            var leaveApplication = leaveApplications.First();

            // Check if leave already exists for this date
            var existingLeave = await _context.Leave
                .FirstOrDefaultAsync(l => l.EmployeeId == employeeId && 
                                         l.LeaveDate.Date == date.Date);

            if (existingLeave != null)
            {
                // Update existing leave
                existingLeave.LeaveType = leaveApplication.LeaveType;
                existingLeave.IsHalfDay = leaveApplication.IsHalfDay;
                existingLeave.CreatedDate = DateTime.Now;
                existingLeave.CreatedBy = int.Parse(userId);
            }
            else
            {
                // Create new leave
                var newLeave = new Leave
                {
                    CompanyId = companyId,
                    EmployeeId = employeeId,
                    LeaveDate = date,
                    LeaveType = leaveApplication.LeaveType,
                    IsHalfDay = leaveApplication.IsHalfDay,
                    Status = "Approved", // Or "Pending" based on your workflow
                    Remarks = "Applied via timesheet",
                    CreatedBy = int.Parse(userId),
                    CreatedDate = DateTime.Now
                };

                _context.Leave.Add(newLeave);
            }
        }
    }

    // Models
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

    // Request models
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
}