using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Repositories
{
    public class PaySheetRepository
    {
        private readonly string _conn;

        public PaySheetRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new Exception("DefaultConnection not found");
        }

        // ============================================================
        // SINGLE — one paysheet row for EmployeeId + Period
        // Period format: "2025/April"
        // ============================================================
        public async Task<PaySheetPrintDto?> GetSingleAsync(int employeeId, string month, int year)
        {
            var period = $"{year}/{month}";

            const string sql = @"
                SELECT
                    p.Id                        AS PaySheetId,
                    p.EmployeeId,
                    p.Period,
                    p.CompanyId,
                    p.DepartmentId,
                    p.DesignationId,
                    p.ShiftBlockId,
                    p.EpfPay,
                    p.SalaryPayType,
                    p.AccountNumber,
                    p.BankName,
                    p.BankCode,
                    p.BranchName,
                    p.BranchCode,
                    p.Nic,

                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    e.EmployeeNo,
                    e.EpfNo,
                    e.ProfileImage,
                    c.CompanyName               AS CompanyName,
                    c.CompanyAddress            AS CompanyAddress,

                    d.DepartmentName            AS Department,
                    ds.Designation              AS Designation,

                    s.BasicSalary,
                    s.BudgetaryAllowance,
                    s.BudgetaryAllowance2,
                    s.AttendanceAllowance,
                    s.FixedAllowance,
                    s.MealAllowance,
                    s.SpecialAllowance,
                    s.TransportAllowance,
                    s.AccommodationAllowance,
                    s.FuelAllowance,
                    s.CostOfLivingAllowance,
                    s.PerformanceAllowance,
                    s.HealthAllowance,
                    s.OtherAllowances,
                    s.SalaryForEPF,

                   s.NoramlOverTimePay              AS NoramlOverTimePay,
s.NoramlOverTimePoyaHours        AS NoramlOverTimePoyaHours,
s.WeeklyDayOffOverTimePay        AS WeeklyDayOffOverTimePay,
s.WeeklyDayOffOverTimeHours      AS WeeklyDayOffOverTimeHours,
s.WeeklyHalfDayOverTimePay       AS WeeklyHalfDayOverTimePay,
s.WeeklyHalfDayOverTimeHours     AS WeeklyHalfDayOverTimeHours,
s.PoyaDayOverTimePay             AS PoyaDayOverTimePay,
s.PoyaDayOverTimeHours           AS PoyaDayOverTimeHours,
s.BankHolidayOverTimePay         AS BankHolidayOverTimePay,
s.BankHolidayOverTimeHours       AS BankHolidayOverTimeHours,
s.MercantileHolidayOverTimePay   AS MercantileHolidayOverTimePay,
s.MercantileHolidayOverTimeHours AS MercantileHolidayOverTimeHours,
s.ExtraOverTimePay               AS ExtraOverTimePay,
s.ExtraOverTimeHours             AS ExtraOverTimeHours,
                    s.PayCutDeduction,
                    s.PayCutHours,
                    s.NoPayDeduction,
                    s.Epf8,
                    s.PayeeDeduction,
                    s.CashShortDeduction,
                    s.WelfareDeduction,
                    s.Loan,
                    s.SalaryAdvance,
                    s.OtherDeduction,

                    s.TotalEarning,
                    s.TotalDeduction,
                    s.GrossSalary,
                    s.NetPay,
                    s.Epf12,
                    s.Etf3,
                    s.WorkingDays,
                    s.WorkingHours,
                    s.LeaveDays,
                    s.NoPayCount                AS NoPayDays
                FROM payroll p
                INNER JOIN Employees e             ON e.Id  = p.EmployeeId
                INNER JOIN Company c               ON c.Id  = p.CompanyId
                INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
                INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
                INNER JOIN Paysheet s              ON s.EmployeeID = p.EmployeeId
                                                  AND s.Period     = p.Period
                WHERE p.EmployeeId = @EmployeeId
                  AND p.Period     = @Period
                ORDER BY p.Id DESC";

            using var conn = new SqlConnection(_conn);
            return await conn.QueryFirstOrDefaultAsync<PaySheetPrintDto>(sql, new
            {
                EmployeeId = employeeId,
                Period     = period
            });
        }

        // ============================================================
        // EPF / ETF FULL-HISTORY TOTALS FOR ONE EMPLOYEE
        // Returns sum of Epf12, Epf8, Etf3 and the period range
        // ============================================================
        public async Task<PaySheetEpfEtfTotalsDto?> GetEpfEtfTotalsAsync(int employeeId)
        {
            const string sql = @"
                SELECT
                    MIN(
                        CAST(LEFT(p.Period, 4) AS INT) * 100
                        + CASE SUBSTRING(p.Period, 6, LEN(p.Period) - 5)
                              WHEN 'January'   THEN 1
                              WHEN 'February'  THEN 2
                              WHEN 'March'     THEN 3
                              WHEN 'April'     THEN 4
                              WHEN 'May'       THEN 5
                              WHEN 'June'      THEN 6
                              WHEN 'July'      THEN 7
                              WHEN 'August'    THEN 8
                              WHEN 'September' THEN 9
                              WHEN 'October'   THEN 10
                              WHEN 'November'  THEN 11
                              WHEN 'December'  THEN 12
                              ELSE 0
                          END
                    )                       AS FirstPeriodKey,

                    MAX(
                        CAST(LEFT(p.Period, 4) AS INT) * 100
                        + CASE SUBSTRING(p.Period, 6, LEN(p.Period) - 5)
                              WHEN 'January'   THEN 1
                              WHEN 'February'  THEN 2
                              WHEN 'March'     THEN 3
                              WHEN 'April'     THEN 4
                              WHEN 'May'       THEN 5
                              WHEN 'June'      THEN 6
                              WHEN 'July'      THEN 7
                              WHEN 'August'    THEN 8
                              WHEN 'September' THEN 9
                              WHEN 'October'   THEN 10
                              WHEN 'November'  THEN 11
                              WHEN 'December'  THEN 12
                              ELSE 0
                          END
                    )                       AS LastPeriodKey,

                    COUNT(*)                AS MonthCount,
                    ISNULL(SUM(s.Epf12), 0) AS TotalEpf12,
                    ISNULL(SUM(s.Epf8),  0) AS TotalEpf8,
                    ISNULL(SUM(s.Etf3),  0) AS TotalEtf3
                FROM payroll p
                INNER JOIN Paysheet s
                    ON s.EmployeeID = p.EmployeeId
                   AND s.Period     = p.Period
                WHERE p.EmployeeId = @EmployeeId
                GROUP BY p.EmployeeId";

            using var conn = new SqlConnection(_conn);
            return await conn.QueryFirstOrDefaultAsync<PaySheetEpfEtfTotalsDto>(
                sql, new { EmployeeId = employeeId });
        }

        // ============================================================
        // BATCH — all paysheet rows for company + period
        // ============================================================
        public async Task<List<PaySheetPrintDto>> GetBatchAsync(
            int companyId, int? departmentId, string month, int year)
        {
            var period = $"{year}/{month}";

            const string sql = @"
                SELECT
                    p.Id                        AS PaySheetId,
                    p.EmployeeId,
                    p.Period,
                    p.CompanyId,
                    p.DepartmentId,
                    p.DesignationId,
                    p.ShiftBlockId,
                    p.EpfPay,
                    p.SalaryPayType,
                    p.AccountNumber,
                    p.BankName,
                    p.BankCode,
                    p.BranchName,
                    p.BranchCode,
                    p.Nic,

                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    e.EmployeeNo,
                    e.EpfNo,
                    e.ProfileImage,
                    c.CompanyName               AS CompanyName,
                    c.CompanyAddress            AS CompanyAddress,

                    d.DepartmentName            AS Department,
                    ds.Designation              AS Designation,

                    s.BasicSalary,
                    s.BudgetaryAllowance,
                    s.BudgetaryAllowance2,
                    s.AttendanceAllowance,
                    s.FixedAllowance,
                    s.MealAllowance,
                    s.SpecialAllowance,
                    s.TransportAllowance,
                    s.AccommodationAllowance,
                    s.FuelAllowance,
                    s.CostOfLivingAllowance,
                    s.PerformanceAllowance,
                    s.HealthAllowance,
                    s.OtherAllowances,
                    s.SalaryForEPF,

                   s.NoramlOverTimePay              AS NoramlOverTimePay,
s.NoramlOverTimePoyaHours        AS NoramlOverTimePoyaHours,
s.WeeklyDayOffOverTimePay        AS WeeklyDayOffOverTimePay,
s.WeeklyDayOffOverTimeHours      AS WeeklyDayOffOverTimeHours,
s.WeeklyHalfDayOverTimePay       AS WeeklyHalfDayOverTimePay,
s.WeeklyHalfDayOverTimeHours     AS WeeklyHalfDayOverTimeHours,
s.PoyaDayOverTimePay             AS PoyaDayOverTimePay,
s.PoyaDayOverTimeHours           AS PoyaDayOverTimeHours,
s.BankHolidayOverTimePay         AS BankHolidayOverTimePay,
s.BankHolidayOverTimeHours       AS BankHolidayOverTimeHours,
s.MercantileHolidayOverTimePay   AS MercantileHolidayOverTimePay,
s.MercantileHolidayOverTimeHours AS MercantileHolidayOverTimeHours,
s.ExtraOverTimePay               AS ExtraOverTimePay,
s.ExtraOverTimeHours             AS ExtraOverTimeHours,

                    s.PayCutDeduction,
                    s.PayCutHours,
                    s.NoPayDeduction,
                    s.Epf8,
                    s.PayeeDeduction,
                    s.CashShortDeduction,
                    s.WelfareDeduction,
                    s.Loan,
                    s.SalaryAdvance,
                    s.OtherDeduction,

                    s.TotalEarning,
                    s.TotalDeduction,
                    s.GrossSalary,
                    s.NetPay,
                    s.Epf12,
                    s.Etf3,
                    s.WorkingDays,
                    s.WorkingHours,
                    s.LeaveDays,
                    s.NoPayCount                AS NoPayDays
                FROM payroll p
                INNER JOIN Employees e             ON e.Id  = p.EmployeeId
                INNER JOIN Company c               ON c.Id  = p.CompanyId
                INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
                INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
                INNER JOIN Paysheet s              ON s.EmployeeID = p.EmployeeId
                                                  AND s.Period     = p.Period
                WHERE p.CompanyId = @CompanyId
                  AND p.Period    = @Period
                  AND (@DepartmentId IS NULL OR p.DepartmentId = @DepartmentId)
                ORDER BY ISNULL(e.EmployeeNo, e.EpfNo), e.FirstName";

            using var conn = new SqlConnection(_conn);
            var rows = await conn.QueryAsync<PaySheetPrintDto>(sql, new
            {
                CompanyId    = companyId,
                DepartmentId = departmentId,
                Period       = period
            });

            return rows.ToList();
        }

        // ============================================================
        // EMPLOYEE EMAIL — from Employees.Mobile
        // ============================================================
        public async Task<string?> GetEmployeeEmailAsync(int employeeId)
        {
            const string sql = @"SELECT TOP 1 Mobile FROM Employees WHERE Id = @EmployeeId";

            using var conn = new SqlConnection(_conn);
            var mobile = await conn.QueryFirstOrDefaultAsync<string?>(sql, new { EmployeeId = employeeId });
            return mobile?.Trim();
        }
    }
}