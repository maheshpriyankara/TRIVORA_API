using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Services
{
    public class PaySheetService : IPaySheetService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public PaySheetService(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAsync(int employeeId)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName as EmployeeName,
                    e.employeeNo as EmployeeNo
                FROM paySheet ps
                INNER JOIN Employees e ON ps.empID_1 = e.Id
                WHERE ps.empID_1 = @EmployeeId
                ORDER BY ps.month_2 DESC";

            var results = await connection.QueryAsync<PaySheetDto>(sql, new { EmployeeId = employeeId });
            return results;
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByMonthAsync(string month)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName as EmployeeName,
                    e.employeeNo as EmployeeNo
                FROM paySheet ps
                INNER JOIN Employees e ON ps.empID_1 = e.Id
                WHERE ps.month_2 = @Month
                ORDER BY e.employeeNo";

            var results = await connection.QueryAsync<PaySheetDto>(sql, new { Month = month });
            return results;
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAndMonthAsync(int employeeId, string month)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName as EmployeeName,
                    e.employeeNo as EmployeeNo
                FROM paySheet ps
                INNER JOIN Employees e ON ps.empID_1 = e.Id
                WHERE ps.empID_1 = @EmployeeId AND ps.month_2 = @Month";

            var results = await connection.QueryAsync<PaySheetDto>(sql, new { EmployeeId = employeeId, Month = month });
            return results;
        }

        public async Task<PaySheetDto?> GetPaySheetByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName as EmployeeName,
                    e.employeeNo as EmployeeNo
                FROM paySheet ps
                INNER JOIN Employees e ON ps.empID_1 = e.Id
                WHERE ps.id_0 = @Id";

            var result = await connection.QueryFirstOrDefaultAsync<PaySheetDto>(sql, new { Id = id });
            return result;
        }

        public async Task<PaySheetDto> CreatePaySheetAsync(PaySheetRequestDto request)
        {
            // Check if paysheet already exists for this employee and month
            var existing = await _context.PaySheet
                .FirstOrDefaultAsync(p => p.EmployeeId == request.EmployeeId && p.Month == request.Month);

            if (existing != null)
            {
                throw new InvalidOperationException($"PaySheet already exists for Employee {request.EmployeeId} in month {request.Month}");
            }

            var paySheet = new PaySheet
            {
                EmployeeId = request.EmployeeId,
                Month = request.Month,
                Basic = request.Basic,
                Br = request.Br,
                TotalBasic = request.TotalBasic,
                SalaryForEpf = request.SalaryForEpf,
                WorkingDays = request.WorkingDays,
                NoPayDays = request.NoPayDays,
                OtNormalHours = request.OtNormalHours,
                OtSundayHours = request.OtSundayHours,
                OtNormal = request.OtNormal,
                OtSunday = request.OtSunday,
                ExtraOtHours = request.ExtraOtHours,
                ExtraOt = request.ExtraOt,
                AddDayCount = request.AddDayCount,
                AddDay = request.AddDay,
                FixedAllowGradeAll = request.FixedAllowGradeAll,
                FixedAllowPosition = request.FixedAllowPosition,
                AttendanceAllowance = request.AttendanceAllowance,
                OtherAllowance = request.OtherAllowance,
                TotalEarnings = request.TotalEarnings,
                GrossSalary = request.GrossSalary,
                LateHours = request.LateHours,
                Late = request.Late,
                NoPay = request.NoPay,
                Advanced = request.Advanced,
                OtherDeduct = request.OtherDeduct,
                OtherLoan = request.OtherLoan,
                Epf8 = request.Epf8,
                WelfareMember = request.WelfareMember,
                WelfareDead = request.WelfareDead,
                PayeeStampDuty = request.PayeeStampDuty,
                EasyPay1 = request.EasyPay1,
                EasyPay2 = request.EasyPay2,
                EasyPay3 = request.EasyPay3,
                WelfareLoan = request.WelfareLoan,
                Donation = request.Donation,
                TotalDeduction = request.TotalDeduction,
                NetSalary = request.NetSalary,
                CoinsBF = request.CoinsBF,
                TotalPayable = request.TotalPayable,
                Pay = request.Pay,
                Epf12 = request.Epf12,
                Epf3 = request.Epf3,
                CoinCF = request.CoinCF,
                PayeeNew = request.PayeeNew,
                Allow01 = request.Allow01,
                Allow02 = request.Allow02,
                Allow03 = request.Allow03,
                Allow04 = request.Allow04
            };

            _context.PaySheet.Add(paySheet);
            await _context.SaveChangesAsync();

            return await GetPaySheetByIdAsync(paySheet.Id) ?? throw new Exception("Failed to retrieve created paysheet");
        }

        public async Task<PaySheetDto> UpdatePaySheetAsync(int id, PaySheetRequestDto request)
        {
            var paySheet = await _context.PaySheet.FindAsync(id);
            if (paySheet == null)
            {
                throw new KeyNotFoundException($"PaySheet with ID {id} not found");
            }

            // Update fields
            paySheet.Basic = request.Basic;
            paySheet.Br = request.Br;
            paySheet.TotalBasic = request.TotalBasic;
            paySheet.SalaryForEpf = request.SalaryForEpf;
            paySheet.WorkingDays = request.WorkingDays;
            paySheet.NoPayDays = request.NoPayDays;
            paySheet.OtNormalHours = request.OtNormalHours;
            paySheet.OtSundayHours = request.OtSundayHours;
            paySheet.OtNormal = request.OtNormal;
            paySheet.OtSunday = request.OtSunday;
            paySheet.ExtraOtHours = request.ExtraOtHours;
            paySheet.ExtraOt = request.ExtraOt;
            paySheet.AddDayCount = request.AddDayCount;
            paySheet.AddDay = request.AddDay;
            paySheet.FixedAllowGradeAll = request.FixedAllowGradeAll;
            paySheet.FixedAllowPosition = request.FixedAllowPosition;
            paySheet.AttendanceAllowance = request.AttendanceAllowance;
            paySheet.OtherAllowance = request.OtherAllowance;
            paySheet.TotalEarnings = request.TotalEarnings;
            paySheet.GrossSalary = request.GrossSalary;
            paySheet.LateHours = request.LateHours;
            paySheet.Late = request.Late;
            paySheet.NoPay = request.NoPay;
            paySheet.Advanced = request.Advanced;
            paySheet.OtherDeduct = request.OtherDeduct;
            paySheet.OtherLoan = request.OtherLoan;
            paySheet.Epf8 = request.Epf8;
            paySheet.WelfareMember = request.WelfareMember;
            paySheet.WelfareDead = request.WelfareDead;
            paySheet.PayeeStampDuty = request.PayeeStampDuty;
            paySheet.EasyPay1 = request.EasyPay1;
            paySheet.EasyPay2 = request.EasyPay2;
            paySheet.EasyPay3 = request.EasyPay3;
            paySheet.WelfareLoan = request.WelfareLoan;
            paySheet.Donation = request.Donation;
            paySheet.TotalDeduction = request.TotalDeduction;
            paySheet.NetSalary = request.NetSalary;
            paySheet.CoinsBF = request.CoinsBF;
            paySheet.TotalPayable = request.TotalPayable;
            paySheet.Pay = request.Pay;
            paySheet.Epf12 = request.Epf12;
            paySheet.Epf3 = request.Epf3;
            paySheet.CoinCF = request.CoinCF;
            paySheet.PayeeNew = request.PayeeNew;
            paySheet.Allow01 = request.Allow01;
            paySheet.Allow02 = request.Allow02;
            paySheet.Allow03 = request.Allow03;
            paySheet.Allow04 = request.Allow04;

            await _context.SaveChangesAsync();

            return await GetPaySheetByIdAsync(id) ?? throw new Exception("Failed to retrieve updated paysheet");
        }

        public async Task<bool> DeletePaySheetAsync(int id)
        {
            var paySheet = await _context.PaySheet.FindAsync(id);
            if (paySheet == null)
            {
                return false;
            }

            _context.PaySheet.Remove(paySheet);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PaySheetDto> CalculatePaySheetAsync(int employeeId, string month)
        {
            // Get employee details
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {employeeId} not found");
            }

            // Get attendance data for the month using direct SQL
            var attendanceData = await GetEmployeeAttendanceForMonthAsync(employeeId, month);

            // Calculate basic pay components - convert decimal to double for calculations
            double basicSalary = (double)employee.BasicSalary;
            double dailyRate = basicSalary / 30; // Assuming 30 days

            // Get working days and calculate pay
            int workingDays = attendanceData.WorkingDays;
            int noPayDays = attendanceData.NoPayDays;
            double totalBasic = dailyRate * workingDays;

            // OT calculations
            double otNormalHours = attendanceData.OtNormalHours;
            double otSundayHours = attendanceData.OtSundayHours;
            double otNormal = otNormalHours * (dailyRate / 8 * 1.5); // 1.5x for normal OT
            double otSunday = otSundayHours * (dailyRate / 8 * 2); // 2x for Sunday OT

            // Calculate deductions
            double lateHours = attendanceData.LateHours;
            double late = lateHours * (dailyRate / 8); // Deduct pay for late hours
            double noPay = noPayDays * dailyRate;

            // Calculate total earnings
            double totalEarnings = totalBasic + otNormal + otSunday;
            
            // Calculate EPF (8% employee contribution)
            double epf8 = totalEarnings * 0.08;
            
            // Calculate total deductions
            double totalDeductions = late + noPay + epf8;
            
            // Calculate net salary
            double netSalary = totalEarnings - totalDeductions;

            // Get employee name
            string employeeName = $"{employee.FirstName} {employee.LastName}";

            // Create paysheet DTO - all monetary values as double? to match the DTO
            var paySheetDto = new PaySheetDto
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                EmployeeNo = employee.EmployeeNo ?? string.Empty,
                Month = month,
                Basic = basicSalary,
                Br = 0,
                TotalBasic = totalBasic,
                SalaryForEpf = totalEarnings,
                WorkingDays = workingDays,
                NoPayDays = noPayDays,
                OtNormalHours = otNormalHours,
                OtSundayHours = otSundayHours,
                OtNormal = otNormal,
                OtSunday = otSunday,
                ExtraOtHours = 0,
                ExtraOt = 0,
                AddDayCount = 0,
                AddDay = 0,
                FixedAllowGradeAll = 0,
                FixedAllowPosition = 0,
                AttendanceAllowance = 0,
                OtherAllowance = 0,
                TotalEarnings = totalEarnings,
                GrossSalary = totalEarnings,
                LateHours = lateHours,
                Late = late,
                NoPay = noPay,
                Advanced = 0,
                OtherDeduct = 0,
                OtherLoan = 0,
                Epf8 = epf8,
                WelfareMember = 0,
                WelfareDead = 0,
                PayeeStampDuty = 0,
                EasyPay1 = 0,
                EasyPay2 = 0,
                EasyPay3 = 0,
                WelfareLoan = 0,
                Donation = 0,
                TotalDeduction = totalDeductions,
                NetSalary = netSalary,
                CoinsBF = 0,
                TotalPayable = netSalary,
                Pay = 0,
                Epf12 = 0,
                Epf3 = 0,
                CoinCF = 0,
                PayeeNew = 0,
                Allow01 = 0,
                Allow02 = 0,
                Allow03 = 0,
                Allow04 = 0
            };

            return paySheetDto;
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByCompanyAndMonthAsync(int companyId, string month)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName as EmployeeName,
                    e.employeeNo as EmployeeNo
                FROM paySheet ps
                INNER JOIN Employees e ON ps.empID_1 = e.Id
                WHERE e.CompanyId = @CompanyId AND ps.month_2 = @Month
                ORDER BY e.employeeNo";

            var results = await connection.QueryAsync<PaySheetDto>(sql, new { CompanyId = companyId, Month = month });
            return results;
        }

        public async Task<bool> ProcessPayrollAsync(int companyId, string month)
        {
            try
            {
                // Get all active employees for the company
                var employees = await _context.Employees
                    .Where(e => e.CompanyId == companyId && e.IsActive)
                    .ToListAsync();

                if (!employees.Any())
                {
                    return false;
                }

                // Process payroll for each employee
                foreach (var employee in employees)
                {
                    try
                    {
                        // Check if paysheet already exists
                        var existing = await _context.PaySheet
                            .FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.Month == month);

                        // Calculate paysheet
                        var calculatedPaysheet = await CalculatePaySheetAsync(employee.Id, month);

                        // Create or update paysheet
                        if (existing != null)
                        {
                            // Update existing
                            var updateRequest = MapDtoToRequest(calculatedPaysheet);
                            await UpdatePaySheetAsync(existing.Id, updateRequest);
                        }
                        else
                        {
                            // Create new
                            var createRequest = MapDtoToRequest(calculatedPaysheet);
                            await CreatePaySheetAsync(createRequest);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other employees
                        Console.WriteLine($"Error processing payroll for employee {employee.Id}: {ex.Message}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing payroll: {ex.Message}");
                return false;
            }
        }

        private async Task<AttendanceSummaryDto> GetEmployeeAttendanceForMonthAsync(int employeeId, string month)
        {
            // Parse month to get date range
            var monthNumber = GetMonthNumber(month);
            if (monthNumber == 0)
            {
                return new AttendanceSummaryDto
                {
                    WorkingDays = 0,
                    NoPayDays = 0,
                    OtNormalHours = 0,
                    OtSundayHours = 0,
                    LateHours = 0
                };
            }

            var year = DateTime.Now.Year;
            var startDate = new DateTime(year, monthNumber, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Query timesheet data for the month - using TimeSheet table
            var sql = @"
                SELECT 
                    ISNULL(COUNT(DISTINCT CAST(FirstInDate AS DATE)), 0) as WorkingDays,
                    ISNULL(SUM(OT_NormalHours), 0) as OtNormalHours,
                    ISNULL(SUM(OT_SundayHours), 0) as OtSundayHours,
                    ISNULL(SUM(LateHours), 0) as LateHours
                FROM TimeSheets
                WHERE EmployeeId = @EmployeeId
                    AND FirstInDate >= @StartDate
                    AND FirstInDate <= @EndDate";

            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryFirstOrDefaultAsync<AttendanceSummaryDto>(sql, new
            {
                EmployeeId = employeeId,
                StartDate = startDate,
                EndDate = endDate
            });

            // If no data found, return default values
            if (result == null)
            {
                return new AttendanceSummaryDto
                {
                    WorkingDays = 0,
                    NoPayDays = 0,
                    OtNormalHours = 0,
                    OtSundayHours = 0,
                    LateHours = 0
                };
            }

            return result;
        }

        private int GetMonthNumber(string monthName)
        {
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

            return 0;
        }

        private PaySheetRequestDto MapDtoToRequest(PaySheetDto dto)
        {
            return new PaySheetRequestDto
            {
                EmployeeId = dto.EmployeeId,
                Month = dto.Month,
                Basic = dto.Basic,
                Br = dto.Br,
                TotalBasic = dto.TotalBasic,
                SalaryForEpf = dto.SalaryForEpf,
                WorkingDays = dto.WorkingDays,
                NoPayDays = dto.NoPayDays,
                OtNormalHours = dto.OtNormalHours,
                OtSundayHours = dto.OtSundayHours,
                OtNormal = dto.OtNormal,
                OtSunday = dto.OtSunday,
                ExtraOtHours = dto.ExtraOtHours,
                ExtraOt = dto.ExtraOt,
                AddDayCount = dto.AddDayCount,
                AddDay = dto.AddDay,
                FixedAllowGradeAll = dto.FixedAllowGradeAll,
                FixedAllowPosition = dto.FixedAllowPosition,
                AttendanceAllowance = dto.AttendanceAllowance,
                OtherAllowance = dto.OtherAllowance,
                TotalEarnings = dto.TotalEarnings,
                GrossSalary = dto.GrossSalary,
                LateHours = dto.LateHours,
                Late = dto.Late,
                NoPay = dto.NoPay,
                Advanced = dto.Advanced,
                OtherDeduct = dto.OtherDeduct,
                OtherLoan = dto.OtherLoan,
                Epf8 = dto.Epf8,
                WelfareMember = dto.WelfareMember,
                WelfareDead = dto.WelfareDead,
                PayeeStampDuty = dto.PayeeStampDuty,
                EasyPay1 = dto.EasyPay1,
                EasyPay2 = dto.EasyPay2,
                EasyPay3 = dto.EasyPay3,
                WelfareLoan = dto.WelfareLoan,
                Donation = dto.Donation,
                TotalDeduction = dto.TotalDeduction,
                NetSalary = dto.NetSalary,
                CoinsBF = dto.CoinsBF,
                TotalPayable = dto.TotalPayable,
                Pay = dto.Pay,
                Epf12 = dto.Epf12,
                Epf3 = dto.Epf3,
                CoinCF = dto.CoinCF,
                PayeeNew = dto.PayeeNew,
                Allow01 = dto.Allow01,
                Allow02 = dto.Allow02,
                Allow03 = dto.Allow03,
                Allow04 = dto.Allow04
            };
        }
    }

    // DTO for attendance summary
    public class AttendanceSummaryDto
    {
        public int WorkingDays { get; set; }
        public int NoPayDays { get; set; }
        public double OtNormalHours { get; set; }
        public double OtSundayHours { get; set; }
        public double LateHours { get; set; }
        public double NoPay { get; set; }
    }
}