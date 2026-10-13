using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
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
                p.BankCode,
                p.BranchCode,
                p.Nic,

                e.FirstName + ' ' + e.LastName AS EmployeeName,
                e.EmployeeNo,
                e.EpfNo,

                c.Name                      AS CompanyName,
                c.Address                   AS CompanyAddress,

                d.DepartmentName            AS Department,
                ds.DesignationName          AS Designation,

                b.BankName                  AS BankName,
                b.BranchName                AS BankBranch,

                p.BasicSalary,
                p.BudgetaryAllowance,
                p.BudgetaryAllowance2,
                p.AttendanceAllowance,
                p.FixedAllowance,
                p.MealAllowance,
                p.SpecialAllowance,
                p.TransportAllowance,
                p.AccommodationAllowance,
                p.FuelAllowance,
                p.CostOfLivingAllowance,
                p.PerformanceAllowance,
                p.HealthAllowance
            FROM payroll p
            INNER JOIN Employees e             ON e.Id  = p.EmployeeId
            INNER JOIN Company c               ON c.Id  = p.CompanyId
            INNER JOIN Company_Departments d   ON d.Id  = p.DepartmentId
            INNER JOIN Company_Designations ds ON ds.Id = p.DesignationId
            LEFT  JOIN Banks b                 ON b.BankCode = p.BankCode";

        public async Task<PaySheetDto?> GetSingleAsync(int employeeId, string month, int year)
        {
            var period = $"{month} {year}";
            var sql = BaseSelect + @"
                WHERE p.EmployeeId = @EmployeeId
                  AND p.Period     = @Period
                ORDER BY p.Id DESC";

            using var conn = new SqlConnection(_conn);
            return await conn.QueryFirstOrDefaultAsync<PaySheetDto>(sql,
                new { EmployeeId = employeeId, Period = period });
        }

        public async Task<List<PaySheetDto>> GetBatchAsync(
            int companyId, int? departmentId, string month, int year)
        {
            var period = $"{month} {year}";
            var sql = BaseSelect + @"
                WHERE p.CompanyId = @CompanyId
                  AND p.Period    = @Period
                  AND (@DepartmentId IS NULL OR p.DepartmentId = @DepartmentId)
                ORDER BY e.EpfNo, e.FirstName";

            using var conn = new SqlConnection(_conn);
            var rows = await conn.QueryAsync<PaySheetDto>(sql, new
            {
                CompanyId    = companyId,
                DepartmentId = departmentId,
                Period       = period
            });
            return rows.ToList();
        }
    }
}