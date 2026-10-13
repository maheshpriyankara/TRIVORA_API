using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
            _conn = cfg.GetConnectionString("DefaultConnection");
        }

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

                c.Name                      AS CompanyName,
                c.Address                   AS CompanyAddress,

                d.DepartmentName            AS Department,
                ds.DesignationName          AS Designation,

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
                s.NoPayDays

            FROM payroll p
            INNER JOIN Employees e             ON e.Id  = p.EmployeeId
            INNER JOIN Company c               ON c.Id  = p.CompanyId
            INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
            INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
            INNER JOIN paysheet s              ON s.EmployeeId = p.EmployeeId
                                              AND s.Period     = p.Period";

        public async Task<PaySheetPrintDto?> GetSingleAsync(int employeeId, string month, int year)
        {
            var period = $"{year}/{month}";

            var sql = BaseSelect + @"
                WHERE p.EmployeeId = @EmployeeId
                  AND p.Period     = @Period
                ORDER BY p.Id DESC";

            using var conn = new SqlConnection(_conn);
            return await conn.QueryFirstOrDefaultAsync<PaySheetPrintDto>(
                sql, new { EmployeeId = employeeId, Period = period });
        }
// ============================================================
// EPF/ETF History — returns one row per month for a period range
// for a specific employee (used by the EPF/ETF History report)
// ============================================================
public async Task<List<PaySheetPrintDto>> GetEpfEtfHistoryAsync(
    int employeeId, string monthFrom, int yearFrom, string monthTo, int yearTo)
{
    var periodFrom = $"{yearFrom}/{monthFrom}";
    var periodTo   = $"{yearTo}/{monthTo}";

    const string sql = @"
        SELECT
            p.Id                        AS PaySheetId,
            p.EmployeeId,
            p.Period,
            p.CompanyId,
            p.DepartmentId,
            p.DesignationId,
            p.SalaryPayType,
            p.AccountNumber,
            p.BankName, p.BankCode, p.BranchName, p.BranchCode,

            e.FirstName + ' ' + e.LastName AS EmployeeName,
            e.EmployeeNo,
            e.EpfNo,

            c.Name                      AS CompanyName,
            c.Address                   AS CompanyAddress,

            d.DepartmentName            AS Department,
            ds.DesignationName          AS Designation,

            s.BasicSalary,
            s.BudgetaryAllowance,
            s.BudgetaryAllowance2,
            s.SalaryForEPF,
            s.Epf8,
            s.Epf12,
            s.Etf3,
            s.TotalEarning,
            s.TotalDeduction,
            s.GrossSalary,
            s.NetPay,
            s.WorkingDays,
            s.NoPayDays
        FROM payroll p
        INNER JOIN Employees e             ON e.Id  = p.EmployeeId
        INNER JOIN Company c               ON c.Id  = p.CompanyId
        INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
        INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
        INNER JOIN paysheet s              ON s.EmployeeId = p.EmployeeId
                                          AND s.Period     = p.Period
        WHERE p.EmployeeId = @EmployeeId
          AND p.Period    >= @PeriodFrom
          AND p.Period    <= @PeriodTo
        ORDER BY p.Period ASC";

    using var conn = new SqlConnection(_conn);
    var rows = await conn.QueryAsync<PaySheetPrintDto>(sql, new
    {
        EmployeeId = employeeId,
        PeriodFrom = periodFrom,
        PeriodTo   = periodTo
    });

    return rows.ToList();
}        public async Task<List<PaySheetPrintDto>> GetBatchAsync(
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

        public async Task<string?> GetEmployeeEmailAsync(int employeeId)
        {
            const string sql = @"
                SELECT TOP 1 Mobile
                FROM Employees
                WHERE Id = @EmployeeId";

            using var conn = new SqlConnection(_conn);
            var mobile = await conn.QueryFirstOrDefaultAsync<string?>(sql, new { EmployeeId = employeeId });
            return mobile?.Trim();
        }
    }
}