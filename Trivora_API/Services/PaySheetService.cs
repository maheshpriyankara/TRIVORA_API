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

        // ────────────────────────────────────────────────────────────
        // READ
        // ────────────────────────────────────────────────────────────

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAsync(int employeeId)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName AS EmployeeName,
                    e.employeeNo AS EmployeeNo
                FROM Paysheet ps
                INNER JOIN Employees e ON ps.EmployeeID = e.Id
                WHERE ps.EmployeeID = @EmployeeId
                ORDER BY ps.Period DESC";

            return await connection.QueryAsync<PaySheetDto>(sql, new { EmployeeId = employeeId });
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByMonthAsync(string month)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName AS EmployeeName,
                    e.employeeNo AS EmployeeNo
                FROM Paysheet ps
                INNER JOIN Employees e ON ps.EmployeeID = e.Id
                WHERE ps.Period = @Month
                ORDER BY e.employeeNo";

            return await connection.QueryAsync<PaySheetDto>(sql, new { Month = month });
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAndMonthAsync(int employeeId, string month)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName AS EmployeeName,
                    e.employeeNo AS EmployeeNo
                FROM Paysheet ps
                INNER JOIN Employees e ON ps.EmployeeID = e.Id
                WHERE ps.EmployeeID = @EmployeeId AND ps.Period = @Month";

            return await connection.QueryAsync<PaySheetDto>(sql, new { EmployeeId = employeeId, Month = month });
        }

        public async Task<PaySheetDto?> GetPaySheetByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName AS EmployeeName,
                    e.employeeNo AS EmployeeNo
                FROM Paysheet ps
                INNER JOIN Employees e ON ps.EmployeeID = e.Id
                WHERE ps.id = @Id";

            return await connection.QueryFirstOrDefaultAsync<PaySheetDto>(sql, new { Id = id });
        }

        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByCompanyAndMonthAsync(int companyId, string month)
        {
            using var connection = new SqlConnection(_connectionString);
            var sql = @"
                SELECT 
                    ps.*,
                    e.firstName + ' ' + e.lastName AS EmployeeName,
                    e.employeeNo AS EmployeeNo
                FROM Paysheet ps
                INNER JOIN Employees e ON ps.EmployeeID = e.Id
                WHERE e.CompanyId = @CompanyId AND ps.Period = @Month
                ORDER BY e.employeeNo";

            return await connection.QueryAsync<PaySheetDto>(sql, new { CompanyId = companyId, Month = month });
        }

        // ────────────────────────────────────────────────────────────
        // WRITE
        // ────────────────────────────────────────────────────────────

        public async Task<PaySheetDto> CreatePaySheetAsync(PaySheetRequestDto request)
        {
            var existing = await _context.PaySheet
                .FirstOrDefaultAsync(p => p.EmployeeId == request.EmployeeId && p.Period == request.Period);

            if (existing != null)
            {
                throw new InvalidOperationException(
                    $"PaySheet already exists for Employee {request.EmployeeId} in period {request.Period}");
            }

            var paySheet = new PaySheet
            {
                EmployeeId = request.EmployeeId,
                Period     = request.Period,

                BasicSalary          = request.BasicSalary,
                BudgetaryAllowance   = request.BudgetaryAllowance,
                BudgetaryAllowance2  = request.BudgetaryAllowance2,
                SalaryForEPF         = request.SalaryForEPF,

                WorkingDays          = request.WorkingDays,
                WorkingHours         = request.WorkingHours,
                LeaveDays            = request.LeaveDays,

                NoramlOverTimePay          = request.NoramlOverTimePay,
                NoramlOverTimePoyaHours    = request.NoramlOverTimePoyaHours,
                WeeklyDayOffOverTimePay    = request.WeeklyDayOffOverTimePay,
                WeeklyDayOffOverTimeHours  = request.WeeklyDayOffOverTimeHours,
                WeeklyHalfDayOverTimePay   = request.WeeklyHalfDayOverTimePay,
                WeeklyHalfDayOverTimeHours = request.WeeklyHalfDayOverTimeHours,
                PoyaDayOverTimePay         = request.PoyaDayOverTimePay,
                PoyaDayOverTimeHours       = request.PoyaDayOverTimeHours,
                BankHolidayOverTimePay     = request.BankHolidayOverTimePay,
                BankHolidayOverTimeHours   = request.BankHolidayOverTimeHours,
                MercantileHolidayOverTimePay   = request.MercantileHolidayOverTimePay,
                MercantileHolidayOverTimeHours = request.MercantileHolidayOverTimeHours,
                ExtraOverTimePay           = request.ExtraOverTimePay,
                ExtraOverTimeHours         = request.ExtraOverTimeHours,

                FixedAllowance          = request.FixedAllowance,
                MealAllowance           = request.MealAllowance,
                SpecialAllowance        = request.SpecialAllowance,
                TransportAllowance      = request.TransportAllowance,
                AccommodationAllowance  = request.AccommodationAllowance,
                FuelAllowance           = request.FuelAllowance,
                CostOfLivingAllowance   = request.CostOfLivingAllowance,
                PerformanceAllowance    = request.PerformanceAllowance,
                HealthAllowance         = request.HealthAllowance,
                OtherAllowances         = request.OtherAllowances,
                AttendanceAllowance     = request.AttendanceAllowance,
                TotalEarning            = request.TotalEarning,
                GrossSalary             = request.GrossSalary,

                PayCutHours         = request.PayCutHours,
                PayCutDeduction     = request.PayCutDeduction,
                NoPayCount          = request.NoPayCount,
                NoPayDeduction      = request.NoPayDeduction,
                PayeeDeduction      = request.PayeeDeduction,
                Epf8                = request.Epf8,
                CashShortDeduction  = request.CashShortDeduction,
                WelfareDeduction    = request.WelfareDeduction,
                Loan                = request.Loan,
                SalaryAdvance       = request.SalaryAdvance,
                OtherDeduction      = request.OtherDeduction,
                TotalDeduction      = request.TotalDeduction,
                NetPay              = request.NetPay,

                Epf12 = request.Epf12,
                Etf3  = request.Etf3
            };

            _context.PaySheet.Add(paySheet);
            await _context.SaveChangesAsync();

            return await GetPaySheetByIdAsync(paySheet.Id)
                   ?? throw new Exception("Failed to retrieve created paysheet");
        }

        public async Task<PaySheetDto> UpdatePaySheetAsync(int id, PaySheetRequestDto request)
        {
            var paySheet = await _context.PaySheet.FindAsync(id);
            if (paySheet == null)
            {
                throw new KeyNotFoundException($"PaySheet with ID {id} not found");
            }

            paySheet.Period = request.Period;

            paySheet.BasicSalary         = request.BasicSalary;
            paySheet.BudgetaryAllowance  = request.BudgetaryAllowance;
            paySheet.BudgetaryAllowance2 = request.BudgetaryAllowance2;
            paySheet.SalaryForEPF        = request.SalaryForEPF;

            paySheet.WorkingDays  = request.WorkingDays;
            paySheet.WorkingHours = request.WorkingHours;
            paySheet.LeaveDays    = request.LeaveDays;

            paySheet.NoramlOverTimePay          = request.NoramlOverTimePay;
            paySheet.NoramlOverTimePoyaHours    = request.NoramlOverTimePoyaHours;
            paySheet.WeeklyDayOffOverTimePay    = request.WeeklyDayOffOverTimePay;
            paySheet.WeeklyDayOffOverTimeHours  = request.WeeklyDayOffOverTimeHours;
            paySheet.WeeklyHalfDayOverTimePay   = request.WeeklyHalfDayOverTimePay;
            paySheet.WeeklyHalfDayOverTimeHours = request.WeeklyHalfDayOverTimeHours;
            paySheet.PoyaDayOverTimePay         = request.PoyaDayOverTimePay;
            paySheet.PoyaDayOverTimeHours       = request.PoyaDayOverTimeHours;
            paySheet.BankHolidayOverTimePay     = request.BankHolidayOverTimePay;
            paySheet.BankHolidayOverTimeHours   = request.BankHolidayOverTimeHours;
            paySheet.MercantileHolidayOverTimePay   = request.MercantileHolidayOverTimePay;
            paySheet.MercantileHolidayOverTimeHours = request.MercantileHolidayOverTimeHours;
            paySheet.ExtraOverTimePay           = request.ExtraOverTimePay;
            paySheet.ExtraOverTimeHours         = request.ExtraOverTimeHours;

            paySheet.FixedAllowance         = request.FixedAllowance;
            paySheet.MealAllowance          = request.MealAllowance;
            paySheet.SpecialAllowance       = request.SpecialAllowance;
            paySheet.TransportAllowance     = request.TransportAllowance;
            paySheet.AccommodationAllowance = request.AccommodationAllowance;
            paySheet.FuelAllowance          = request.FuelAllowance;
            paySheet.CostOfLivingAllowance  = request.CostOfLivingAllowance;
            paySheet.PerformanceAllowance   = request.PerformanceAllowance;
            paySheet.HealthAllowance        = request.HealthAllowance;
            paySheet.OtherAllowances        = request.OtherAllowances;
            paySheet.AttendanceAllowance    = request.AttendanceAllowance;
            paySheet.TotalEarning           = request.TotalEarning;
            paySheet.GrossSalary            = request.GrossSalary;

            paySheet.PayCutHours        = request.PayCutHours;
            paySheet.PayCutDeduction    = request.PayCutDeduction;
            paySheet.NoPayCount         = request.NoPayCount;
            paySheet.NoPayDeduction     = request.NoPayDeduction;
            paySheet.PayeeDeduction     = request.PayeeDeduction;
            paySheet.Epf8               = request.Epf8;
            paySheet.CashShortDeduction = request.CashShortDeduction;
            paySheet.WelfareDeduction   = request.WelfareDeduction;
            paySheet.Loan               = request.Loan;
            paySheet.SalaryAdvance      = request.SalaryAdvance;
            paySheet.OtherDeduction     = request.OtherDeduction;
            paySheet.TotalDeduction     = request.TotalDeduction;
            paySheet.NetPay             = request.NetPay;

            paySheet.Epf12 = request.Epf12;
            paySheet.Etf3  = request.Etf3;

            await _context.SaveChangesAsync();

            return await GetPaySheetByIdAsync(id)
                   ?? throw new Exception("Failed to retrieve updated paysheet");
        }

        public async Task<bool> DeletePaySheetAsync(int id)
        {
            var paySheet = await _context.PaySheet.FindAsync(id);
            if (paySheet == null) return false;

            _context.PaySheet.Remove(paySheet);
            await _context.SaveChangesAsync();
            return true;
        }

        // ────────────────────────────────────────────────────────────
        // CALCULATE (per-employee, per-period)
        // ────────────────────────────────────────────────────────────

        public async Task<PaySheetDto> CalculatePaySheetAsync(int employeeId, string period)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {employeeId} not found");
            }

            var attendance = await GetEmployeeAttendanceForMonthAsync(employeeId, period);

            // Basic + daily rate (30-day month assumption — matches old behaviour)
            decimal basicSalary = (decimal)employee.BasicSalary;
            decimal dailyRate   = basicSalary / 30m;

            double workingDays = attendance.WorkingDays;
            double noPayDays   = attendance.NoPayDays;
            double leaveDays   = attendance.LeaveDays;

            decimal totalBasic = dailyRate * (decimal)workingDays;

            // Overtime (unchanged math — using daily rate / 8 * multiplier)
            decimal hourlyRate     = dailyRate / 8m;
            double  otNormalHours  = attendance.OtNormalHours;
            double  otSundayHours  = attendance.OtSundayHours;
            decimal otNormalPay    = (decimal)otNormalHours * hourlyRate * 1.5m;
            decimal otSundayPay    = (decimal)otSundayHours * hourlyRate * 2m;

            // Late + no-pay
            double  lateHours       = attendance.LateHours;
            decimal lateDeduction   = (decimal)lateHours * hourlyRate;
            decimal noPayDeduction  = (decimal)noPayDays * dailyRate;

            // Totals
            decimal totalEarning = totalBasic + otNormalPay + otSundayPay;
            decimal grossSalary  = totalEarning;
            decimal epf8         = totalEarning * 0.08m;
            decimal totalDeduction = lateDeduction + noPayDeduction + epf8;
            decimal netPay         = totalEarning - totalDeduction;

            string employeeName = $"{employee.FirstName} {employee.LastName}".Trim();

            return new PaySheetDto
            {
                EmployeeId      = employeeId,
                EmployeeName    = employeeName,
                EmployeeNo      = employee.EmployeeNo ?? string.Empty,
                Period          = period,

                BasicSalary         = basicSalary,
                BudgetaryAllowance  = 0,
                BudgetaryAllowance2 = 0,
                SalaryForEPF        = totalEarning,

                WorkingDays  = workingDays,
                WorkingHours = attendance.WorkingHours,
                LeaveDays    = leaveDays,

                NoramlOverTimePay          = otNormalPay,
                NoramlOverTimePoyaHours    = otNormalHours,
                WeeklyDayOffOverTimePay    = 0,
                WeeklyDayOffOverTimeHours  = 0,
                WeeklyHalfDayOverTimePay   = 0,
                WeeklyHalfDayOverTimeHours = 0,
                PoyaDayOverTimePay         = 0,
                PoyaDayOverTimeHours       = 0,
                BankHolidayOverTimePay     = 0,
                BankHolidayOverTimeHours   = 0,
                MercantileHolidayOverTimePay   = 0,
                MercantileHolidayOverTimeHours = 0,
                ExtraOverTimePay    = otSundayPay,
                ExtraOverTimeHours  = otSundayHours,

                FixedAllowance         = 0,
                MealAllowance          = 0,
                SpecialAllowance       = 0,
                TransportAllowance     = 0,
                AccommodationAllowance = 0,
                FuelAllowance          = 0,
                CostOfLivingAllowance  = 0,
                PerformanceAllowance   = 0,
                HealthAllowance        = 0,
                OtherAllowances        = 0,
                AttendanceAllowance    = 0,
                TotalEarning           = totalEarning,
                GrossSalary            = grossSalary,

                PayCutHours        = lateHours,
                PayCutDeduction    = lateDeduction,
                NoPayCount         = noPayDays,
                NoPayDeduction     = noPayDeduction,
                PayeeDeduction     = 0,
                Epf8               = epf8,
                CashShortDeduction = 0,
                WelfareDeduction   = 0,
                Loan               = 0,
                SalaryAdvance      = 0,
                OtherDeduction     = 0,
                TotalDeduction     = totalDeduction,
                NetPay             = netPay,

                Epf12 = 0,
                Etf3  = 0
            };
        }

        // ────────────────────────────────────────────────────────────
        // BATCH
        // ────────────────────────────────────────────────────────────

        public async Task<bool> ProcessPayrollAsync(int companyId, string period)
        {
            try
            {
                var employees = await _context.Employees
                    .Where(e => e.CompanyId == companyId && e.IsActive)
                    .ToListAsync();

                if (!employees.Any()) return false;

                foreach (var employee in employees)
                {
                    try
                    {
                        var existing = await _context.PaySheet
                            .FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.Period == period);

                        var calculated = await CalculatePaySheetAsync(employee.Id, period);
                        var request    = MapDtoToRequest(calculated);

                        if (existing != null)
                        {
                            await UpdatePaySheetAsync(existing.Id, request);
                        }
                        else
                        {
                            await CreatePaySheetAsync(request);
                        }
                    }
                    catch (Exception ex)
                    {
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

        // ────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────

        private async Task<AttendanceSummaryDto> GetEmployeeAttendanceForMonthAsync(int employeeId, string period)
        {
            // period format: "2025-January" or "January"
            int year  = DateTime.Now.Year;
            int month = 0;

            var parts = (period ?? "").Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int parsedYear))
            {
                year  = parsedYear;
                month = GetMonthNumber(parts[1]);
            }
            else
            {
                month = GetMonthNumber(period ?? "");
            }

            if (month == 0)
            {
                return new AttendanceSummaryDto();
            }

            var startDate = new DateTime(year, month, 1);
            var endDate   = startDate.AddMonths(1).AddDays(-1);

            var sql = @"
                SELECT 
                    ISNULL(COUNT(DISTINCT CAST(FirstInDate AS DATE)), 0) AS WorkingDays,
                    ISNULL(SUM(OT_NormalHours), 0)                        AS OtNormalHours,
                    ISNULL(SUM(OT_SundayHours), 0)                        AS OtSundayHours,
                    ISNULL(SUM(LateHours), 0)                             AS LateHours,
                    ISNULL(SUM(WorkingHours), 0)                          AS WorkingHours,
                    ISNULL(SUM(LeaveDays), 0)                             AS LeaveDays,
                    ISNULL(SUM(NoPayDays), 0)                             AS NoPayDays
                FROM TimeSheets
                WHERE EmployeeId = @EmployeeId
                    AND FirstInDate >= @StartDate
                    AND FirstInDate <= @EndDate";

            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryFirstOrDefaultAsync<AttendanceSummaryDto>(sql, new
            {
                EmployeeId = employeeId,
                StartDate  = startDate,
                EndDate    = endDate
            });

            return result ?? new AttendanceSummaryDto();
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

            if (months.TryGetValue(monthName.Trim(), out int n)) return n;
            return 0;
        }

        private PaySheetRequestDto MapDtoToRequest(PaySheetDto dto)
        {
            return new PaySheetRequestDto
            {
                EmployeeId = dto.EmployeeId,
                Period     = dto.Period,

                BasicSalary         = dto.BasicSalary,
                BudgetaryAllowance  = dto.BudgetaryAllowance,
                BudgetaryAllowance2 = dto.BudgetaryAllowance2,
                SalaryForEPF        = dto.SalaryForEPF,

                WorkingDays  = dto.WorkingDays,
                WorkingHours = dto.WorkingHours,
                LeaveDays    = dto.LeaveDays,

                NoramlOverTimePay          = dto.NoramlOverTimePay,
                NoramlOverTimePoyaHours    = dto.NoramlOverTimePoyaHours,
                WeeklyDayOffOverTimePay    = dto.WeeklyDayOffOverTimePay,
                WeeklyDayOffOverTimeHours  = dto.WeeklyDayOffOverTimeHours,
                WeeklyHalfDayOverTimePay   = dto.WeeklyHalfDayOverTimePay,
                WeeklyHalfDayOverTimeHours = dto.WeeklyHalfDayOverTimeHours,
                PoyaDayOverTimePay         = dto.PoyaDayOverTimePay,
                PoyaDayOverTimeHours       = dto.PoyaDayOverTimeHours,
                BankHolidayOverTimePay     = dto.BankHolidayOverTimePay,
                BankHolidayOverTimeHours   = dto.BankHolidayOverTimeHours,
                MercantileHolidayOverTimePay   = dto.MercantileHolidayOverTimePay,
                MercantileHolidayOverTimeHours = dto.MercantileHolidayOverTimeHours,
                ExtraOverTimePay    = dto.ExtraOverTimePay,
                ExtraOverTimeHours  = dto.ExtraOverTimeHours,

                FixedAllowance         = dto.FixedAllowance,
                MealAllowance          = dto.MealAllowance,
                SpecialAllowance       = dto.SpecialAllowance,
                TransportAllowance     = dto.TransportAllowance,
                AccommodationAllowance = dto.AccommodationAllowance,
                FuelAllowance          = dto.FuelAllowance,
                CostOfLivingAllowance  = dto.CostOfLivingAllowance,
                PerformanceAllowance   = dto.PerformanceAllowance,
                HealthAllowance        = dto.HealthAllowance,
                OtherAllowances        = dto.OtherAllowances,
                AttendanceAllowance    = dto.AttendanceAllowance,
                TotalEarning           = dto.TotalEarning,
                GrossSalary            = dto.GrossSalary,

                PayCutHours        = dto.PayCutHours,
                PayCutDeduction    = dto.PayCutDeduction,
                NoPayCount         = dto.NoPayCount,
                NoPayDeduction     = dto.NoPayDeduction,
                PayeeDeduction     = dto.PayeeDeduction,
                Epf8               = dto.Epf8,
                CashShortDeduction = dto.CashShortDeduction,
                WelfareDeduction   = dto.WelfareDeduction,
                Loan               = dto.Loan,
                SalaryAdvance      = dto.SalaryAdvance,
                OtherDeduction     = dto.OtherDeduction,
                TotalDeduction     = dto.TotalDeduction,
                NetPay             = dto.NetPay,

                Epf12 = dto.Epf12,
                Etf3  = dto.Etf3
            };
        }
    }

    public class AttendanceSummaryDto
    {
        public double WorkingDays   { get; set; }
        public double NoPayDays     { get; set; }
        public double WorkingHours  { get; set; }
        public double LeaveDays     { get; set; }
        public double OtNormalHours { get; set; }
        public double OtSundayHours { get; set; }
        public double LateHours     { get; set; }
    }
}