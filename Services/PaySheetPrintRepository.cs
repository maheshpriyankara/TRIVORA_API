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
    public class PaySheetPrintRepository
    {
        private readonly string _conn;

        public PaySheetPrintRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new Exception("DefaultConnection not found");
        }

        // ============================================================
        // SHARED SELECT — used by single + batch
        // ============================================================
        private const string BaseSelect = @"
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

                s.OverTimePay,
                s.OverTimeHours,
                s.OverTimePoyaPay,
                s.OverTimePoyaHours,
                s.OverTimeBankHolidayPay,
                s.OverTimeBankHolidayHours,
                s.OverTimeSundayPay,
                s.OverTimeSundayHours,

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
                s.NoPayCount                AS NoPayDays
            FROM payroll p
            INNER JOIN Employees e             ON e.Id  = p.EmployeeId
            INNER JOIN Company c               ON c.Id  = p.CompanyId
            INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
            INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
            INNER JOIN Paysheet s              ON s.EmployeeID = p.EmployeeId
                                              AND s.Period     = p.Period";

        // ============================================================
        // SINGLE
        // ============================================================
        public async Task<PaySheetPrintDto?> GetSingleAsync(int employeeId, string month, int year)
        {
            var period = $"{year}/{month}";

            var sql = BaseSelect + @"
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
        // BATCH
        // ============================================================
        public async Task<List<PaySheetPrintDto>> GetBatchAsync(
            int companyId, int? departmentId, string month, int year)
        {
            var period = $"{year}/{month}";

            var sql = BaseSelect + @"
                WHERE p.CompanyId = @CompanyId
                  AND p.Period    = @Period
                  AND (@DepartmentId IS NULL OR p.DepartmentId = @DepartmentId)
                ORDER BY e.EpfNo, e.FirstName";

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
        // EMPLOYEE EMAIL
        // ============================================================
        public async Task<string?> GetEmployeeEmailAsync(int employeeId)
        {
            const string sql = @"SELECT TOP 1 Mobile FROM Employees WHERE Id = @EmployeeId";

            using var conn = new SqlConnection(_conn);
            var mobile = await conn.QueryFirstOrDefaultAsync<string?>(sql, new { EmployeeId = employeeId });
            return mobile?.Trim();
        }

        // ============================================================
        // PERIOD LOCK CHECK — uses Payroll_Process
        //   Ready = locked = 1 AND processing = 0
        // ============================================================
        public async Task<bool> IsPeriodLockedAsync(int companyId, string month, int year)
        {
            const string sql = @"
                SELECT TOP 1
                    ISNULL(locked, 0)     AS locked,
                    ISNULL(processing, 0) AS processing
                FROM Payroll_Process
                WHERE company_id = @CompanyId
                  AND year       = @Year
                  AND month      = @Month";

            using var conn = new SqlConnection(_conn);
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                CompanyId = companyId,
                Year      = year,
                Month     = month
            });

            if (row == null) return false;
            return (bool)row.locked == true && (bool)row.processing == false;
        }

        // ============================================================
        // PERIOD STATUS — exists / locked / processing
        // ============================================================
        public async Task<(bool exists, bool locked, bool processing)> GetPeriodStatusAsync(
            int companyId, string month, int year)
        {
            const string sql = @"
                SELECT TOP 1
                    ISNULL(locked, 0)     AS locked,
                    ISNULL(processing, 0) AS processing
                FROM Payroll_Process
                WHERE company_id = @CompanyId
                  AND year       = @Year
                  AND month      = @Month";

            using var conn = new SqlConnection(_conn);
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new
            {
                CompanyId = companyId,
                Year      = year,
                Month     = month
            });

            if (row == null) return (false, false, false);
            return (true, (bool)row.locked, (bool)row.processing);
        }

        // ============================================================
        // LOCKED PERIODS LIST
        // ============================================================
        public async Task<List<string>> GetLockedPeriodsAsync(int companyId)
        {
            const string sql = @"
                SELECT year, month
                FROM Payroll_Process
                WHERE company_id  = @CompanyId
                  AND ISNULL(locked, 0)     = 1
                  AND ISNULL(processing, 0) = 0";

            using var conn = new SqlConnection(_conn);
            var rows = await conn.QueryAsync<(int year, string month)>(sql, new { CompanyId = companyId });

            return rows
                .Select(r => $"{r.year}/{r.month}")
                .ToList();
        }

        // ============================================================
        // EMPLOYEE PROCESSING CHECK (process_queue)
        //   TRUE if any row exists with processing = 1 OR process_end = 0
        // ============================================================
        public async Task<bool> IsEmployeePeriodProcessingAsync(int employeeId, string period)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM process_queue
                WHERE emp_systemID = @EmployeeId
                  AND period_      = @Period
                  AND (processing = 1 OR process_end = 0)";

            using var conn = new SqlConnection(_conn);
            var count = await conn.ExecuteScalarAsync<int>(sql, new
            {
                EmployeeId = employeeId,
                Period     = period
            });

            return count > 0;
        }

        // ============================================================
        // EPF/ETF HISTORY — for a period range
        // ============================================================
        public async Task<List<PaySheetPrintDto>> GetEpfEtfHistoryAsync(
            int employeeId, string monthFrom, int yearFrom, string monthTo, int yearTo)
        {
            // Uses the same BaseSelect but only the EPF/ETF-relevant columns are read.
            var sql = @"
                SELECT
                    p.Id AS PaySheetId, p.EmployeeId, p.Period, p.CompanyId,
                    p.DepartmentId, p.DesignationId,
                    p.SalaryPayType, p.AccountNumber,
                    p.BankName, p.BankCode, p.BranchName, p.BranchCode,
                    e.FirstName + ' ' + e.LastName AS EmployeeName,
                    e.EmployeeNo, e.EpfNo,
                    c.CompanyName AS CompanyName,
                    c.CompanyAddress AS CompanyAddress,
                    d.DepartmentName AS Department,
                    ds.Designation AS Designation,
                    s.BasicSalary, s.BudgetaryAllowance, s.BudgetaryAllowance2,
                    s.SalaryForEPF, s.Epf8, s.Epf12, s.Etf3,
                    s.TotalEarning, s.TotalDeduction, s.GrossSalary, s.NetPay,
                    s.WorkingDays, s.NoPayCount AS NoPayDays
                FROM payroll p
                INNER JOIN Employees e             ON e.Id  = p.EmployeeId
                INNER JOIN Company c               ON c.Id  = p.CompanyId
                INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
                INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
                INNER JOIN Paysheet s              ON s.EmployeeID = p.EmployeeId
                                                  AND s.Period     = p.Period
                WHERE p.EmployeeId = @EmployeeId";

            using var conn = new SqlConnection(_conn);
            var all = (await conn.QueryAsync<PaySheetPrintDto>(sql, new { EmployeeId = employeeId })).ToList();

            var months = new[]
            {
                "January","February","March","April","May","June",
                "July","August","September","October","November","December"
            };

            int Key(string period)
            {
                var parts = (period ?? "").Split('/');
                if (parts.Length != 2 || !int.TryParse(parts[0], out var y)) return 0;
                var m = Array.IndexOf(months, parts[1]) + 1;
                return m <= 0 ? 0 : y * 100 + m;
            }

            var fromKey = yearFrom * 100 + (Array.IndexOf(months, monthFrom) + 1);
            var toKey   = yearTo   * 100 + (Array.IndexOf(months, monthTo)   + 1);

            return all
                .Where(x => { var k = Key(x.Period); return k >= fromKey && k <= toKey; })
                .OrderBy(x => Key(x.Period))
                .ToList();
        }
    }
}