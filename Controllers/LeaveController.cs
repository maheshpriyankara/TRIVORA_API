using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Transactions;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LeaveController : ControllerBase
    {
        private readonly string _connectionString;

        public LeaveController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // DTO Classes
        public class PayrollPeriod
        {
            public int Id { get; set; }
            public int CompanyId { get; set; }
            public int Year { get; set; }
            public string Month { get; set; }
            public DateTime PayrollStartDate { get; set; }
            public bool Processing { get; set; }
            public bool Locked { get; set; }
        }

        public class LeaveBalanceDto
        {
            public decimal AnnualLeave { get; set; }
            public decimal CasualLeave { get; set; }
            public decimal SickLeave { get; set; }
            public decimal AnnualLeaveBalance { get; set; }
            public decimal CasualLeaveBalance { get; set; }
            public decimal SickLeaveBalance { get; set; }
        }

        

        public class LeaveApplicationDto
        {
            public int EmployeeId { get; set; }
            public DateTime LeaveDate { get; set; }
            public string LeaveType { get; set; }
            public bool IsHalfDay { get; set; }
            public string Remarks { get; set; }
            public int CompanyId { get; set; } // Add this field
            public int UserId { get; set; } // Add this field
        }
        public class EmployeeInfoDto
        {
            public int Id { get; set; }
            public DateTime? DateOfAppointment { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }

        public class LeaveSummaryDto
        {
            public int Id { get; set; }
            public DateTime LeaveDate { get; set; }
            public string LeaveType { get; set; }
            public bool IsHalfDay { get; set; }
            public string Status { get; set; }
            public string Remarks { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        // OTHER LEAVE DTOs
        public class ApplyOtherLeaveRequest
        {
            public int EmployeeId { get; set; }
            public string LeaveType { get; set; } // "Maternity", "Special", "Unpaid", "Other"
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
            public string Reason { get; set; }
            public string Remarks { get; set; }
            public int CompanyId { get; set; } // Add this field
            public int UserId { get; set; } // Add this field
        }

        public class OtherLeaveDto
        {
            public int Id { get; set; }
            public int EmployeeId { get; set; }
            public string LeaveType { get; set; }
            public DateTime FromDate { get; set; }
            public DateTime ToDate { get; set; }
            public string Reason { get; set; }
            public string Remarks { get; set; }
            public string Status { get; set; }
            public DateTime AppliedDate { get; set; }
            public DateTime? ApprovedDate { get; set; }
            public int? ApprovedBy { get; set; }
            public string ApprovalRemarks { get; set; }
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

        // Get employee shift block salary start date
        [HttpGet("employee-salary-start-date/{employeeId}")]
        public async Task<IActionResult> GetEmployeeSalaryStartDate(int employeeId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                var sql = @"
                    SELECT ISNULL(sb.SalaryStartDate, 1) as SalaryStartDate
                    FROM Employees e
                    LEFT JOIN ShiftBlocks sb ON e.ShiftBlockId = sb.Id
                    WHERE e.Id = @EmployeeId";
                
                var salaryStartDate = await connection.QueryFirstOrDefaultAsync<int>(sql, new { EmployeeId = employeeId });
                
                return Ok(new { success = true, data = salaryStartDate });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get or calculate leave balance with entitlement and history consideration
        [HttpGet("leave-balance/{employeeId}")]
        public async Task<IActionResult> GetLeaveBalance(int employeeId, [FromQuery] int? year = null, [FromQuery] int? month = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // Get employee appointment date
                var employeeSql = @"
                    SELECT Id, DateOfAppointment, FirstName, LastName
                    FROM Employees 
                    WHERE Id = @EmployeeId";
                
                var employee = await connection.QueryFirstOrDefaultAsync<EmployeeInfoDto>(employeeSql, new { EmployeeId = employeeId });
                
                if (employee == null || employee.DateOfAppointment == null)
                {
                    return NotFound(new { success = false, message = "Employee not found or appointment date not set" });
                }

                // Use provided year/month or current date as fallback
                var currentDate = DateTime.Now;
                var targetYear = year ?? currentDate.Year;
                var targetMonth = month ?? currentDate.Month;
                
                // Always use the last date of the current month for calculation
                var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);
                var calculationDate = new DateTime(targetYear, targetMonth, daysInMonth);

                // STEP 1: Get eligible leave entitlement (from DB or calculate)
                decimal eligibleAnnual, eligibleCasual, eligibleSick;
                
                var entitlementSql = @"
                    SELECT 
                        AnnualLeave, 
                        CasualLeave, 
                        SickLeave
                    FROM LeaveBalance 
                    WHERE EmployeeId = @EmployeeId 
                    AND EntitlementYear = @TargetYear";
                    
                var dbEntitlement = await connection.QueryFirstOrDefaultAsync<LeaveBalanceDto>(entitlementSql, new 
                { 
                    EmployeeId = employeeId,
                    TargetYear = targetYear
                });

                if (dbEntitlement != null)
                {
                    // Use entitlement from database
                    eligibleAnnual = dbEntitlement.AnnualLeave;
                    eligibleCasual = dbEntitlement.CasualLeave;
                    eligibleSick = dbEntitlement.SickLeave;
                }
                else
                {
                    // Calculate entitlement using existing formula with calculation date (last date of current month)
                    var calculatedEntitlement = LeaveCalculator.CalculateLeaveEntitlement(
                        employee.DateOfAppointment.Value, calculationDate);

                    eligibleAnnual = calculatedEntitlement.AnnualLeave;
                    eligibleCasual = calculatedEntitlement.CasualLeave;
                    eligibleSick = calculatedEntitlement.SickLeave;
                }

                // STEP 2: Get taken leaves from history for target year
                var takenLeavesSql = @"
                    SELECT 
                        LeaveType,
                        IsHalfDay,
                        COUNT(*) as LeaveCount
                    FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                    AND YEAR(LeaveDate) = @TargetYear
                    AND Status = 'Approved'
                    GROUP BY LeaveType, IsHalfDay";
                    
                var takenLeaves = await connection.QueryAsync<(string LeaveType, bool IsHalfDay, int LeaveCount)>(takenLeavesSql, new 
                {
                    EmployeeId = employeeId,
                    TargetYear = targetYear
                });

                // Calculate total taken leaves by type
                decimal takenAnnual = 0, takenCasual = 0, takenSick = 0;

                foreach (var leave in takenLeaves)
                {
                    decimal leaveDays = leave.IsHalfDay ? 0.5m : 1.0m;
                    decimal totalDays = leave.LeaveCount * leaveDays;

                    switch (leave.LeaveType)
                    {
                        case "Annual":
                            takenAnnual += totalDays;
                            break;
                        case "Casual":
                            takenCasual += totalDays;
                            break;
                        case "Sick":
                            takenSick += totalDays;
                            break;
                    }
                }

                // STEP 3: Calculate final available balance
                decimal finalAnnualBalance = eligibleAnnual - takenAnnual;
                decimal finalCasualBalance = eligibleCasual - takenCasual;
                decimal finalSickBalance = eligibleSick - takenSick;

                // Prepare response in your existing format
                var balance = new LeaveBalanceDto 
                { 
                    AnnualLeave = eligibleAnnual,
                    CasualLeave = eligibleCasual,
                    SickLeave = eligibleSick,
                    AnnualLeaveBalance = finalAnnualBalance,
                    CasualLeaveBalance = finalCasualBalance,
                    SickLeaveBalance = finalSickBalance
                };

                return Ok(new { success = true, data = balance });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetLeaveBalance: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Apply for leave - UPDATED to calculate final balance considering entitlement and history
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyLeave([FromBody] LeaveApplicationDto request)
        {
            using var transaction = new TransactionScope(TransactionScopeOption.Required, 
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted }, 
                TransactionScopeAsyncFlowOption.Enabled);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                var currentYear = DateTime.Now.Year;
                var currentMonth = DateTime.Now.Month;

                // Check if active leave already exists for this date
                var existingLeaveSql = @"
                    SELECT Id FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                    AND LeaveDate = @LeaveDate 
                    AND CompanyId = @CompanyId
                    AND Status = 'Approved'";
                    
                var existingLeave = await connection.QueryFirstOrDefaultAsync<int?>(existingLeaveSql, new 
                {
                    request.EmployeeId,
                    request.LeaveDate,
                    request.CompanyId
                });

                if (existingLeave.HasValue)
                {
                    return BadRequest(new { success = false, message = "Active leave already exists for this date" });
                }

                // Get employee appointment date
                var employeeSql = @"
                    SELECT Id, DateOfAppointment, FirstName, LastName
                    FROM Employees 
                    WHERE Id = @EmployeeId";
                
                var employee = await connection.QueryFirstOrDefaultAsync<EmployeeInfoDto>(employeeSql, new { EmployeeId = request.EmployeeId });

                if (employee == null || employee.DateOfAppointment == null)
                {
                    return BadRequest(new { success = false, message = "Employee not found or appointment date not set" });
                }

                // STEP 1: Get eligible leave entitlement (from DB or calculate)
                // Always use the last date of the current month for calculation
                var daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
                var calculationDate = new DateTime(currentYear, currentMonth, daysInMonth);
                
                decimal eligibleAnnual, eligibleCasual, eligibleSick;
                
                var entitlementSql = @"
                    SELECT 
                        AnnualLeave, 
                        CasualLeave, 
                        SickLeave
                    FROM LeaveBalance 
                    WHERE EmployeeId = @EmployeeId 
                    AND EntitlementYear = @CurrentYear";
                    
                var dbEntitlement = await connection.QueryFirstOrDefaultAsync<LeaveBalanceDto>(entitlementSql, new 
                { 
                    request.EmployeeId,
                    CurrentYear = currentYear
                });

                if (dbEntitlement != null)
                {
                    // Use entitlement from database
                    eligibleAnnual = dbEntitlement.AnnualLeave;
                    eligibleCasual = dbEntitlement.CasualLeave;
                    eligibleSick = dbEntitlement.SickLeave;
                }
                else
                {
                    // Calculate entitlement using existing formula with calculation date (last date of current month)
                    var calculatedEntitlement = LeaveCalculator.CalculateLeaveEntitlement(
                        employee.DateOfAppointment.Value, calculationDate);

                    eligibleAnnual = calculatedEntitlement.AnnualLeave;
                    eligibleCasual = calculatedEntitlement.CasualLeave;
                    eligibleSick = calculatedEntitlement.SickLeave;
                }

                // STEP 2: Get taken leaves from history for current year
                var takenLeavesSql = @"
                    SELECT 
                        LeaveType,
                        IsHalfDay,
                        COUNT(*) as LeaveCount
                    FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                    AND YEAR(LeaveDate) = @CurrentYear
                    AND Status = 'Approved'
                    GROUP BY LeaveType, IsHalfDay";
                    
                var takenLeaves = await connection.QueryAsync<(string LeaveType, bool IsHalfDay, int LeaveCount)>(takenLeavesSql, new 
                {
                    request.EmployeeId,
                    CurrentYear = currentYear
                });

                // Calculate total taken leaves by type
                decimal takenAnnual = 0, takenCasual = 0, takenSick = 0;

                foreach (var leave in takenLeaves)
                {
                    decimal leaveDays = leave.IsHalfDay ? 0.5m : 1.0m;
                    decimal totalDays = leave.LeaveCount * leaveDays;

                    switch (leave.LeaveType)
                    {
                        case "Annual":
                            takenAnnual += totalDays;
                            break;
                        case "Casual":
                            takenCasual += totalDays;
                            break;
                        case "Sick":
                            takenSick += totalDays;
                            break;
                    }
                }

                // STEP 3: Calculate final available balance
                decimal finalAnnualBalance = eligibleAnnual - takenAnnual;
                decimal finalCasualBalance = eligibleCasual - takenCasual;
                decimal finalSickBalance = eligibleSick - takenSick;

                decimal leaveDeduction = request.IsHalfDay ? 0.5m : 1.0m;

                // Check sufficient final balance
                if (request.LeaveType == "Annual" && finalAnnualBalance < leaveDeduction)
                {
                    return BadRequest(new { success = false, message = "Insufficient annual leave balance" });
                }
                else if (request.LeaveType == "Casual" && finalCasualBalance < leaveDeduction)
                {
                    return BadRequest(new { success = false, message = "Insufficient casual leave balance" });
                }
                else if (request.LeaveType == "Sick" && finalSickBalance < leaveDeduction)
                {
                    return BadRequest(new { success = false, message = "Insufficient sick leave balance" });
                }

                // Insert leave record
                var insertLeaveSql = @"
                    INSERT INTO Leave (CompanyId, EmployeeId, LeaveDate, LeaveType, IsHalfDay, Remarks, Status, CreatedBy, CreatedDate)
                    OUTPUT INSERTED.Id
                    VALUES (@CompanyId, @EmployeeId, @LeaveDate, @LeaveType, @IsHalfDay, @Remarks, 'Approved', @UserId, GETDATE())";
                    
                var leaveId = await connection.ExecuteScalarAsync<int>(insertLeaveSql, new 
                {
                    request.CompanyId,
                    request.EmployeeId,
                    request.LeaveDate,
                    request.LeaveType,
                    request.IsHalfDay,
                    request.Remarks,
                    request.UserId
                });

                // Update leave balance table ONLY if record exists
                if (dbEntitlement != null)
                {
                    var updateBalanceSql = "";
                    if (request.LeaveType == "Annual")
                    {
                        updateBalanceSql = @"
                            UPDATE LeaveBalance 
                            SET AnnualLeaveBalance = AnnualLeaveBalance - @Deduction 
                            WHERE EmployeeId = @EmployeeId 
                            AND EntitlementYear = @CurrentYear";
                    }
                    else if (request.LeaveType == "Casual")
                    {
                        updateBalanceSql = @"
                            UPDATE LeaveBalance 
                            SET CasualLeaveBalance = CasualLeaveBalance - @Deduction 
                            WHERE EmployeeId = @EmployeeId 
                            AND EntitlementYear = @CurrentYear";
                    }
                    else if (request.LeaveType == "Sick")
                    {
                        updateBalanceSql = @"
                            UPDATE LeaveBalance 
                            SET SickLeaveBalance = SickLeaveBalance - @Deduction 
                            WHERE EmployeeId = @EmployeeId 
                            AND EntitlementYear = @CurrentYear";
                    }

                    if (!string.IsNullOrEmpty(updateBalanceSql))
                    {
                        await connection.ExecuteAsync(updateBalanceSql, new 
                        {
                            Deduction = leaveDeduction,
                            request.EmployeeId,
                            CurrentYear = currentYear
                        });
                    }
                }

                // Log the action
                var logSql = @"
                    INSERT INTO LeaveLogs (LeaveId, Action, PreviousValues, NewValues, ActionBy, ActionDate, Remarks,LeaveType)
                    VALUES (@LeaveId, 'Created', NULL, @NewValues, @ActionBy, GETDATE(), 'Leave application created','Paid Leave')";
                    
                await connection.ExecuteAsync(logSql, new 
                {
                    LeaveId = leaveId,
                    NewValues = $"LeaveType: {request.LeaveType}, Date: {request.LeaveDate:yyyy-MM-dd}, HalfDay: {request.IsHalfDay}, Status: Approved",
                    ActionBy = request.UserId
                });

                transaction.Complete();

                return Ok(new { success = true, message = "Leave applied successfully", data = new { LeaveId = leaveId } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error applying leave: {ex.Message}" });
            }
        }

        [HttpGet("leave-summary/{employeeId}")]
        public async Task<IActionResult> GetLeaveSummary(int employeeId, [FromQuery] int year)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                var sql = @"
                    SELECT 
                        l.Id,
                        l.LeaveDate,
                        l.LeaveType,
                        l.IsHalfDay,
                        l.Status,
                        l.Remarks,
                        l.CreatedDate
                    FROM Leave l
                    WHERE l.EmployeeId = @EmployeeId
                    AND YEAR(l.LeaveDate) = @Year
                    AND l.Status != 'Cancelled'
                    ORDER BY l.LeaveDate DESC";

                var leaves = await connection.QueryAsync<LeaveSummaryDto>(sql, new 
                { 
                    EmployeeId = employeeId,
                    Year = year
                });

                // Calculate summary statistics
                var annualLeaves = leaves.Where(l => l.LeaveType == "Annual").ToList();
                var casualLeaves = leaves.Where(l => l.LeaveType == "Casual").ToList();
                var sickLeaves = leaves.Where(l => l.LeaveType == "Sick").ToList();

                var summary = new
                {
                    leaves = leaves,
                    annualSummary = new 
                    {
                        totalDays = annualLeaves.Sum(l => l.IsHalfDay ? 0.5 : 1),
                        totalRecords = annualLeaves.Count
                    },
                    casualSummary = new 
                    {
                        totalDays = casualLeaves.Sum(l => l.IsHalfDay ? 0.5 : 1),
                        totalRecords = casualLeaves.Count
                    },
                    sickSummary = new 
                    {
                        totalDays = sickLeaves.Sum(l => l.IsHalfDay ? 0.5 : 1),
                        totalRecords = sickLeaves.Count
                    }
                };

                return Ok(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetLeaveSummary: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Generate leave entitlements for all employees for a specific year
        [HttpPost("generate-entitlements/{year}")]
        public async Task<IActionResult> GenerateLeaveEntitlements(int year)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // Get all active employees with appointment dates
                var employeesSql = @"
                    SELECT Id, DateOfAppointment, FirstName, LastName
                    FROM Employees 
                    WHERE DateOfAppointment IS NOT NULL 
                    AND Status = 'Active'";
                
                var employees = await connection.QueryAsync<EmployeeInfoDto>(employeesSql);

                var results = new List<object>();
                var calculationDate = new DateTime(year, 12, 31); // Calculate as of end of year

                foreach (var employee in employees)
                {
                    try
                    {
                        // Check if entitlement already exists for this year
                        var existingSql = @"
                            SELECT COUNT(1) 
                            FROM LeaveBalance 
                            WHERE EmployeeId = @EmployeeId 
                            AND EntitlementYear = @Year";
                            
                        var exists = await connection.ExecuteScalarAsync<bool>(existingSql, new 
                        {
                            EmployeeId = employee.Id,
                            Year = year
                        });

                        if (!exists)
                        {
                            // Calculate leave entitlement
                            var entitlement = LeaveCalculator.CalculateLeaveEntitlement(
                                employee.DateOfAppointment.Value, calculationDate);

                            // Insert leave balance record
                            var insertSql = @"
                                INSERT INTO LeaveBalance (CompanyId, EmployeeId, EntitlementYear, 
                                                        AnnualLeave, CasualLeave, SickLeave,
                                                        AnnualLeaveBalance, CasualLeaveBalance, SickLeaveBalance,
                                                        CreatedDate)
                                VALUES (1, @EmployeeId, @EntitlementYear, 
                                        @AnnualLeave, @CasualLeave, @SickLeave,
                                        @AnnualLeaveBalance, @CasualLeaveBalance, @SickLeaveBalance,
                                        GETDATE())";
                                
                            await connection.ExecuteAsync(insertSql, new 
                            {
                                EmployeeId = employee.Id,
                                EntitlementYear = year,
                                AnnualLeave = entitlement.AnnualLeave,
                                CasualLeave = entitlement.CasualLeave,
                                SickLeave = entitlement.SickLeave,
                                AnnualLeaveBalance = entitlement.AnnualLeave,
                                CasualLeaveBalance = entitlement.CasualLeave,
                                SickLeaveBalance = entitlement.SickLeave
                            });

                            results.Add(new 
                            {
                                EmployeeId = employee.Id,
                                Name = $"{employee.FirstName} {employee.LastName}",
                                DateOfAppointment = employee.DateOfAppointment,
                                Entitlement = entitlement,
                                Status = "Generated"
                            });
                        }
                        else
                        {
                            results.Add(new 
                            {
                                EmployeeId = employee.Id,
                                Name = $"{employee.FirstName} {employee.LastName}",
                                Status = "AlreadyExists"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new 
                        {
                            EmployeeId = employee.Id,
                            Name = $"{employee.FirstName} {employee.LastName}",
                            Status = "Error",
                            Error = ex.Message
                        });
                    }
                }

                return Ok(new { 
                    success = true, 
                    message = $"Leave entitlements generated for {year}",
                    data = results 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get leave calendar for employee - SIMPLE FIX
        [HttpGet("leave-calendar")]
        public async Task<IActionResult> GetLeaveCalendar(int employeeId, int year, int month, int salaryStartDay = 1)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // Calculate date range based on salary start day
                DateTime startDate, endDate;
                
                if (salaryStartDay == 1)
                {
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                }
                else
                {
                    startDate = new DateTime(year, month, salaryStartDay);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    
                    if (salaryStartDay > 1 && month == 1)
                    {
                        startDate = new DateTime(year - 1, 12, salaryStartDay);
                    }
                    else if (salaryStartDay > 1)
                    {
                        startDate = new DateTime(year, month - 1, salaryStartDay);
                    }
                }

                // Generate date range in C#
                var dateRange = new List<DateTime>();
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }

                // Get ALL leaves for this employee and date range (both active and cancelled)
                var sql = @"
                    SELECT 
                        LeaveDate,
                        LeaveType,
                        IsHalfDay,
                        Status,
                        Id AS LeaveId
                    FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                        AND LeaveDate BETWEEN @StartDate AND @EndDate
                        AND Status IN ('Approved')";

                var leaves = await connection.QueryAsync<dynamic>(sql, new 
                { 
                    StartDate = startDate,
                    EndDate = endDate,
                    EmployeeId = employeeId
                });

                // SIMPLE FIX: Return as list, let frontend handle duplicates
                var leaveList = leaves.ToList();

                // Build the result - include ALL leaves
                var result = dateRange.Select(date => 
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    
                    // Find ALL leaves for this date (could be multiple - active and cancelled)
                    var dateLeaves = leaveList.Where(l => (DateTime)l.LeaveDate == date).ToList();
                    
                    return new 
                    {
                        Date = dateStr,
                        DayName = date.DayOfWeek.ToString(),
                        AllLeaves = dateLeaves.Select(l => new {
                            LeaveId = l.LeaveId,
                            LeaveType = l.LeaveType,
                            IsHalfDay = l.IsHalfDay,
                            Status = l.Status
                        }).ToList()
                    };
                }).ToList();

                return Ok(new { 
                    success = true, 
                    data = result, 
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Cancel leave - UPDATED to use SOFT DELETE
        [HttpPost("cancel/{leaveId}")]
        public async Task<IActionResult> CancelLeave(int leaveId)
        {
            if (!HttpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader))
            {
                return BadRequest(new { success = false, message = "User ID header is required" });
            }

            if (!int.TryParse(userIdHeader, out int userId) || userId <= 0)
            {
                return BadRequest(new { success = false, message = "Invalid user ID" });
            }

            using var transaction = new TransactionScope(TransactionScopeOption.Required, 
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted }, 
                TransactionScopeAsyncFlowOption.Enabled);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {
                var currentYear = DateTime.Now.Year;

                // Get leave details before cancellation
                var leaveSql = @"
                    SELECT EmployeeId, LeaveDate, LeaveType, IsHalfDay, Status
                    FROM Leave 
                    WHERE Id = @LeaveId AND Status = 'Approved'"; // Only allow cancellation of active leaves
                    
                var leave = await connection.QueryFirstOrDefaultAsync<dynamic>(leaveSql, new { LeaveId = leaveId });

                if (leave == null)
                {
                    return NotFound(new { success = false, message = "Active leave not found or already cancelled" });
                }

                // Restore leave balance
                decimal leaveRestore = leave.IsHalfDay ? 0.5m : 1.0m;
                var updateBalanceSql = "";

                if (leave.LeaveType == "Annual")
                {
                    updateBalanceSql = @"
                        UPDATE LeaveBalance 
                        SET AnnualLeaveBalance = AnnualLeaveBalance + @Restore 
                        WHERE EmployeeId = @EmployeeId 
                        AND EntitlementYear = @CurrentYear";
                }
                else if (leave.LeaveType == "Casual")
                {
                    updateBalanceSql = @"
                        UPDATE LeaveBalance 
                        SET CasualLeaveBalance = CasualLeaveBalance + @Restore 
                        WHERE EmployeeId = @EmployeeId 
                        AND EntitlementYear = @CurrentYear";
                }
                else if (leave.LeaveType == "Sick")
                {
                    updateBalanceSql = @"
                        UPDATE LeaveBalance 
                        SET SickLeaveBalance = SickLeaveBalance + @Restore 
                        WHERE EmployeeId = @EmployeeId 
                        AND EntitlementYear = @CurrentYear";
                }

                if (!string.IsNullOrEmpty(updateBalanceSql))
                {
                    await connection.ExecuteAsync(updateBalanceSql, new 
                    {
                        Restore = leaveRestore,
                        EmployeeId = leave.EmployeeId,
                        CurrentYear = currentYear
                    });
                }

                // SOFT DELETE: Update leave status to 'Cancelled' instead of deleting
                var cancelSql = @"
                    UPDATE Leave 
                    SET Status = 'Cancelled', 
                        CancelledBy = @CancelledBy, 
                        CancelledDate = GETDATE()
                    WHERE Id = @LeaveId";
                    
                var rowsAffected = await connection.ExecuteAsync(cancelSql, new 
                {
                    LeaveId = leaveId,
                    CancelledBy = userId
                });

                if (rowsAffected == 0)
                {
                    return BadRequest(new { success = false, message = "Failed to cancel leave" });
                }

                // Log the cancellation action
                var logSql = @"
                    INSERT INTO LeaveLogs (LeaveId, Action, PreviousValues, NewValues, ActionBy, ActionDate, Remarks)
                    VALUES (@LeaveId, 'Cancelled', @PreviousValues, @NewValues, @ActionBy, GETDATE(), 'Leave cancelled - soft delete')";
                    
                await connection.ExecuteAsync(logSql, new 
                {
                    LeaveId = leaveId,
                    PreviousValues = $"LeaveType: {leave.LeaveType}, Date: {leave.LeaveDate:yyyy-MM-dd}, HalfDay: {leave.IsHalfDay}, Status: Approved",
                    NewValues = $"LeaveType: {leave.LeaveType}, Date: {leave.LeaveDate:yyyy-MM-dd}, HalfDay: {leave.IsHalfDay}, Status: Cancelled",
                    ActionBy = userId
                });

                transaction.Complete();

                return Ok(new { success = true, message = "Leave cancelled successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error cancelling leave: {ex.Message}" });
            }
        }

        // Get employee leave entitlement summary
        [HttpGet("entitlement-summary/{employeeId}")]
        public async Task<IActionResult> GetEntitlementSummary(int employeeId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // Get employee details
                var employeeSql = @"
                    SELECT Id, DateOfAppointment, FirstName, LastName
                    FROM Employees 
                    WHERE Id = @EmployeeId";
                
                var employee = await connection.QueryFirstOrDefaultAsync<EmployeeInfoDto>(employeeSql, new { EmployeeId = employeeId });

                if (employee == null || employee.DateOfAppointment == null)
                {
                    return NotFound(new { success = false, message = "Employee not found or appointment date not set" });
                }

                // Get current and previous years leave balances
                var currentYear = DateTime.Now.Year;
                var previousYear = currentYear - 1;

                var balanceSql = @"
                    SELECT EntitlementYear, AnnualLeave, CasualLeave, SickLeave,
                           AnnualLeaveBalance, CasualLeaveBalance, SickLeaveBalance
                    FROM LeaveBalance 
                    WHERE EmployeeId = @EmployeeId 
                    AND EntitlementYear IN (@CurrentYear, @PreviousYear)
                    ORDER BY EntitlementYear DESC";
                
                var balances = await connection.QueryAsync<dynamic>(balanceSql, new 
                { 
                    EmployeeId = employeeId,
                    CurrentYear = currentYear,
                    PreviousYear = previousYear
                });

                // Calculate projected entitlement for next year
                var nextYear = currentYear + 1;
                var nextYearDate = new DateTime(nextYear, 12, 31);
                var projectedEntitlement = LeaveCalculator.CalculateLeaveEntitlement(
                    employee.DateOfAppointment.Value, nextYearDate);

                var result = new
                {
                    Employee = new
                    {
                        employee.Id,
                        Name = $"{employee.FirstName} {employee.LastName}",
                        employee.DateOfAppointment,
                        YearsOfService = DateTime.Now.Year - employee.DateOfAppointment.Value.Year
                    },
                    CurrentYearBalance = balances.FirstOrDefault(b => b.EntitlementYear == currentYear),
                    PreviousYearBalance = balances.FirstOrDefault(b => b.EntitlementYear == previousYear),
                    ProjectedNextYearEntitlement = projectedEntitlement
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get leave history for employee - INCLUDING CANCELLED LEAVES
        [HttpGet("leave-history/{employeeId}")]
        public async Task<IActionResult> GetLeaveHistory(int employeeId, int year)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                var sql = @"
                    SELECT 
                        Id AS LeaveId,
                        LeaveDate,
                        LeaveType,
                        IsHalfDay,
                        Status,
                        Remarks,
                        CreatedDate,
                        CancelledDate,
                        CreatedBy,
                        CancelledBy
                    FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                    AND YEAR(LeaveDate) = @Year
                    AND Status IN ('Approved', 'Cancelled')
                    ORDER BY LeaveDate DESC";
                
                var leaves = await connection.QueryAsync<dynamic>(sql, new 
                {
                    EmployeeId = employeeId,
                    Year = year
                });

                return Ok(new { success = true, data = leaves });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ========== OTHER LEAVE ENDPOINTS ==========

        // Apply for Other Leave
        [HttpPost("apply-other")]
        public async Task<IActionResult> ApplyOtherLeave([FromBody] ApplyOtherLeaveRequest request)
        {
            using var transaction = new TransactionScope(TransactionScopeOption.Required, 
                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted }, 
                TransactionScopeAsyncFlowOption.Enabled);
            
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            try
            {

                // Validate dates
                if (request.FromDate > request.ToDate)
                {
                    return BadRequest(new { success = false, message = "From date cannot be after to date" });
                }

                // Check if employee exists
                var employeeSql = @"
                    SELECT COUNT(1) 
                    FROM Employees 
                    WHERE Id = @EmployeeId";
                    
                var employeeExists = await connection.ExecuteScalarAsync<bool>(employeeSql, new { request.EmployeeId });
                
                if (!employeeExists)
                {
                    return NotFound(new { success = false, message = "Employee not found" });
                }

                // Check for date conflicts with existing leaves (both regular and other)
                var conflictSql = @"
                    -- Check regular leaves
                    SELECT COUNT(1) 
                    FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                    AND Status = 'Approved'
                    AND LeaveDate BETWEEN @FromDate AND @ToDate
                    
                    UNION ALL
                    
                    -- Check other leaves
                    SELECT COUNT(1) 
                    FROM OtherLeaves 
                    WHERE EmployeeId = @EmployeeId 
                    AND Status IN ('Approved', 'Pending')
                    AND ((FromDate <= @ToDate AND ToDate >= @FromDate))";

                var conflicts = await connection.QueryAsync<int>(conflictSql, new 
                {
                    request.EmployeeId,
                    request.FromDate,
                    request.ToDate
                });

                var totalConflicts = conflicts.Sum();
                if (totalConflicts > 0)
                {
                    return BadRequest(new { success = false, message = "There is a leave conflict with existing leaves for the selected period" });
                }

                // Insert other leave record
                var insertSql = @"
                    INSERT INTO OtherLeaves (CompanyId,EmployeeId, LeaveType, FromDate, ToDate, Reason, Remarks, Status, AppliedDate,ApprovedDate,ApprovedBy,ApprovalRemarks)
                    OUTPUT INSERTED.Id
                    VALUES (@CompanyId,@EmployeeId, @LeaveType, @FromDate, @ToDate, @Reason, @Remarks, 'Approved', GETDATE(), GETDATE(),@userId,'System')";
                    
                var leaveId = await connection.ExecuteScalarAsync<int>(insertSql, new 
                {
                    request.CompanyId,
                    request.EmployeeId,
                    request.LeaveType,
                    request.FromDate,
                    request.ToDate,
                    request.Reason,
                    request.Remarks,
                    request.UserId
                });

                // Log the action
                var logSql = @"
                    INSERT INTO LeaveLogs (LeaveId, Action, PreviousValues, NewValues, ActionBy, ActionDate, Remarks, LeaveType)
                    VALUES (@LeaveId, 'OtherLeave_Created', NULL, @NewValues, @ActionBy, GETDATE(), 'Other leave application created', 'Other')";
                    
                await connection.ExecuteAsync(logSql, new 
                {
                    LeaveId = leaveId,
                    NewValues = $"LeaveType: {request.LeaveType}, FromDate: {request.FromDate:yyyy-MM-dd}, ToDate: {request.ToDate:yyyy-MM-dd}, Reason: {request.Reason}, Status: Pending",
                    ActionBy = request.UserId

                });

                transaction.Complete();

                return Ok(new { success = true, message = "Other leave applied successfully", data = new { LeaveId = leaveId } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error applying other leave: {ex.Message}" });
            }
        }

        // Get Other Leaves for employee - FIXED VERSION
        [HttpGet("other-leaves/{employeeId}")]
        public async Task<IActionResult> GetOtherLeaves(int employeeId, [FromQuery] int? year = null, [FromQuery] int? month = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                var sql = @"
                    SELECT 
                        Id,
                        EmployeeId,
                        LeaveType,
                        FromDate,
                        ToDate,
                        Reason,
                        Remarks,
                        Status,
                        AppliedDate,
                        ApprovedDate,
                        ApprovedBy,
                        ApprovalRemarks
                    FROM OtherLeaves 
                    WHERE EmployeeId = @EmployeeId";

                // Build query based on parameters
                object parameters;
                
                if (year.HasValue && month.HasValue)
                {
                    sql += " AND YEAR(FromDate) = @Year AND MONTH(FromDate) = @Month";
                    parameters = new { EmployeeId = employeeId, Year = year.Value, Month = month.Value };
                }
                else if (year.HasValue)
                {
                    sql += " AND YEAR(FromDate) = @Year";
                    parameters = new { EmployeeId = employeeId, Year = year.Value };
                }
                else
                {
                    parameters = new { EmployeeId = employeeId };
                }

                sql += " ORDER BY FromDate DESC";

                var leaves = await connection.QueryAsync<OtherLeaveDto>(sql, parameters);

                return Ok(new { success = true, data = leaves });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error retrieving other leaves: {ex.Message}" });
            }
        }

        // Delete Other Leave
        [HttpDelete("other-leaves/{leaveId}")]
            public async Task<IActionResult> DeleteOtherLeave(int leaveId)
            {
                // Validate user ID from header first
                if (!HttpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader))
                {
                    return BadRequest(new { success = false, message = "User ID header is required" });
                }

                if (!int.TryParse(userIdHeader, out int userId) || userId <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid user ID" });
                }

                using var transaction = new TransactionScope(TransactionScopeOption.Required, 
                    new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted }, 
                    TransactionScopeAsyncFlowOption.Enabled);
                
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                try
                {
                    // Get other leave details before deletion
                    var leaveSql = @"
                        SELECT EmployeeId, LeaveType, FromDate, ToDate, Reason, Status
                        FROM OtherLeaves 
                        WHERE Id = @LeaveId";
                        
                    var leave = await connection.QueryFirstOrDefaultAsync<dynamic>(leaveSql, new { LeaveId = leaveId });

                    if (leave == null)
                    {
                        return NotFound(new { success = false, message = "Other leave not found" });
                    }

                    

                    // Check if the leave date is within the current salary period
                    var currentPeriodSql = @"
                        SELECT TOP 1 payrollStartDate as PayrollStartDate
                        FROM Payroll_Process 
                        WHERE locked = 0 
                        ORDER BY payrollStartDate";
                        
                    var currentPeriod = await connection.QueryFirstOrDefaultAsync<DateTime?>(currentPeriodSql);

                    if (currentPeriod.HasValue && leave.FromDate < currentPeriod.Value)
                    {
                        return BadRequest(new { success = false, message = "Cannot delete other leaves from previous salary periods" });
                    }

                    // Delete the other leave record
                    var deleteSql = @"
                        DELETE FROM OtherLeaves 
                        WHERE Id = @LeaveId";
                        
                    var rowsAffected = await connection.ExecuteAsync(deleteSql, new { LeaveId = leaveId });

                    if (rowsAffected == 0)
                    {
                        return BadRequest(new { success = false, message = "Failed to delete other leave" });
                    }

                    // Log the deletion action
                    var logSql = @"
                        INSERT INTO LeaveLogs (LeaveId, Action, PreviousValues, NewValues, ActionBy, ActionDate, Remarks, LeaveType)
                        VALUES (@LeaveId, 'OtherLeave_Deleted', @PreviousValues, NULL, @ActionBy, GETDATE(), 'Other leave deleted', 'Other')";
                        
                    await connection.ExecuteAsync(logSql, new 
                    {
                        LeaveId = leaveId,
                        PreviousValues = $"LeaveType: {leave.LeaveType}, FromDate: {leave.FromDate:yyyy-MM-dd}, ToDate: {leave.ToDate:yyyy-MM-dd}, Reason: {leave.Reason}, Status: {leave.Status}",
                        ActionBy = userId
                    });

                    transaction.Complete();

                    return Ok(new { success = true, message = "Other leave deleted successfully" });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = $"Error deleting other leave: {ex.Message}" });
                }
            }
        // Get leave calendar with Other Leaves blocking
        [HttpGet("leave-calendar-with-other")]
        public async Task<IActionResult> GetLeaveCalendarWithOther(int employeeId, int year, int month, int salaryStartDay = 1)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                
                // Calculate date range based on salary start day (using your existing logic)
                DateTime startDate, endDate;
                
                if (salaryStartDay == 1)
                {
                    startDate = new DateTime(year, month, 1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                }
                else
                {
                    startDate = new DateTime(year, month, salaryStartDay);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    
                    if (salaryStartDay > 1 && month == 1)
                    {
                        startDate = new DateTime(year - 1, 12, salaryStartDay);
                    }
                    else if (salaryStartDay > 1)
                    {
                        startDate = new DateTime(year, month - 1, salaryStartDay);
                    }
                }

                // Generate date range in C#
                var dateRange = new List<DateTime>();
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    dateRange.Add(date);
                }

                // Get ALL leaves for this employee and date range (both regular and other)
                var regularLeavesSql = @"
                    SELECT 
                        LeaveDate,
                        LeaveType,
                        IsHalfDay,
                        Status,
                        Id AS LeaveId
                    FROM Leave 
                    WHERE EmployeeId = @EmployeeId 
                        AND LeaveDate BETWEEN @StartDate AND @EndDate
                        AND Status IN ('Approved')";

                var regularLeaves = await connection.QueryAsync<dynamic>(regularLeavesSql, new 
                { 
                    StartDate = startDate,
                    EndDate = endDate,
                    EmployeeId = employeeId
                });

                // Get Other Leaves that block dates in this period
                var otherLeavesSql = @"
                    SELECT 
                        Id,
                        LeaveType,
                        FromDate,
                        ToDate,
                        Reason,
                        Status
                    FROM OtherLeaves 
                    WHERE EmployeeId = @EmployeeId 
                        AND Status IN ('Approved', 'Pending')
                        AND ((FromDate <= @EndDate AND ToDate >= @StartDate))";

                var otherLeaves = await connection.QueryAsync<dynamic>(otherLeavesSql, new 
                { 
                    StartDate = startDate,
                    EndDate = endDate,
                    EmployeeId = employeeId
                });

                // Build the result
                var result = dateRange.Select(date => 
                {
                    var dateStr = date.ToString("yyyy-MM-dd");
                    
                    // Find ALL regular leaves for this date
                    var dateRegularLeaves = regularLeaves.Where(l => (DateTime)l.LeaveDate == date).ToList();
                    
                    // Check if this date is blocked by any Other Leave
                    var blockingLeave = otherLeaves.FirstOrDefault(ol => 
                        date >= (DateTime)ol.FromDate && date <= (DateTime)ol.ToDate);
                    
                    var isBlocked = blockingLeave != null;
                    
                    return new 
                    {
                        Date = dateStr,
                        DayName = date.DayOfWeek.ToString(),
                        AllLeaves = dateRegularLeaves.Select(l => new {
                            LeaveId = l.LeaveId,
                            LeaveType = l.LeaveType,
                            IsHalfDay = l.IsHalfDay,
                            Status = l.Status
                        }).ToList(),
                        IsBlocked = isBlocked,
                        BlockingLeaveType = isBlocked ? blockingLeave.LeaveType : null,
                        BlockingLeaveReason = isBlocked ? blockingLeave.Reason : null
                    };
                }).ToList();

                return Ok(new { 
                    success = true, 
                    data = result, 
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    endDate = endDate.ToString("yyyy-MM-dd")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class LeaveEntitlement
    {
        public int AnnualLeave { get; set; }
        public int CasualLeave { get; set; }
        public int SickLeave { get; set; }

        public int AnnualLeaveBalance { get; set; }
        public int CasualLeaveBalance { get; set; }
        public int SickLeaveBalance { get; set; }
    }

    public static class LeaveCalculator
    {
        // Sri Lankan Labour Law Leave Entitlements
        private const int ANNUAL_LEAVE_DAYS = 14;
        private const int CASUAL_LEAVE_DAYS = 7;
        private const int SICK_LEAVE_DAYS = 7;

        public static LeaveEntitlement CalculateLeaveEntitlement(DateTime dateOfAppointment, DateTime calculationDate)
        {
            Console.WriteLine(calculationDate);
            var entitlement = new LeaveEntitlement();

            int appointmentYear = dateOfAppointment.Year;
            int calculationYear = calculationDate.Year;
            int yearGap = calculationYear - appointmentYear;

            // Rule 1: DOA and calculation date years SAME
            if (yearGap == 0)
            {
                // No annual leave
                entitlement.AnnualLeave = 0;
                
                // Casual + Sick = worked months count / 2
                int workedMonths = GetWorkedMonths(dateOfAppointment, calculationDate);
                int totalCasualSick = workedMonths / 2;
                entitlement.CasualLeave = totalCasualSick;
                entitlement.SickLeave = 0;
            }
            // Rule 2: DOA and calculation year gap is 1
            else if (yearGap == 1)
            {
                // Annual based on quarter
                if (dateOfAppointment.Month >= 7 && dateOfAppointment.Month <= 9) // Jul-Sep
                {
                    entitlement.AnnualLeave = 7;
                }
                else if (dateOfAppointment.Month >= 4 && dateOfAppointment.Month <= 6) // Apr-Jun
                {
                    entitlement.AnnualLeave = 10;
                }
                else if (dateOfAppointment.Month >= 1 && dateOfAppointment.Month <= 3) // Jan-Mar
                {
                    entitlement.AnnualLeave = 14;
                }
                else // Oct-Dec
                {
                    entitlement.AnnualLeave = 4;
                }

                // Casual + Sick: if worked total months > 12 then 7, else worked months / 2
                int totalWorkedMonths = GetWorkedMonths(dateOfAppointment, calculationDate);
                if (totalWorkedMonths > 12)
                {
                    entitlement.CasualLeave = 7;
                    entitlement.SickLeave = 0;
                }
                else
                {
                    int totalCasualSick = totalWorkedMonths / 2;
                    entitlement.CasualLeave = totalCasualSick;
                    entitlement.SickLeave = 0;
                }
            }
            // Rule 3: DOA and calculation year gap > 1
            else
            {
                // Annual = 14, Casual + Sick = 7
                entitlement.AnnualLeave = 14;
                entitlement.CasualLeave = 7;
                entitlement.SickLeave = 0;
            }

            return entitlement;
        }

        private static int GetWorkedMonths(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate) return 0;

            int totalMonths = ((endDate.Year - startDate.Year) * 12) + (endDate.Month - startDate.Month);
            
            // If end day is less than start day, we haven't completed this month
            if (endDate.Day < startDate.Day)
            {
                totalMonths--;
            }
            
            return Math.Max(0, totalMonths);
        }
    }
}