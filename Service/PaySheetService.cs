// Services/PaySheetService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TRIVORA_API.Models;

namespace TRIVORA_API.Service
{
    public class PaySheetService : IPaySheetService
    {
        private readonly string _connectionString;

        public PaySheetService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new Exception("Connection string not found");
        }

        // ============================================================
        // GET PAYSHEET BY EMPLOYEE AND MONTH
        // ============================================================
        // Services/PaySheetService.cs

        public async Task<PaySheetDto?> GetPaySheetByEmployeeAndMonthAsync(int employeeId, string month)
        {
            Console.WriteLine($"🔍 GetPaySheetByEmployeeAndMonthAsync called: EmployeeId={employeeId}, Month={month}");

            using (var con = new SqlConnection(_connectionString))
            {
                // ✅ FIXED: Use correct column names from Employees table
                string query = @"
    SELECT 
        p.*,
        e.FirstName + ' ' + e.LastName AS EmployeeName,
        e.EmployeeNo,
        d.DepartmentName AS Department,
        des.Designation AS Designation  -- Add this line
    FROM paySheet p
    INNER JOIN Employees e ON p.empID_1 = e.Id
    LEFT JOIN Company_Departments d ON e.DepartmentId = d.Id
    LEFT JOIN Company_Designations des ON e.DesignationId = des.Id  -- Add this join
    WHERE p.empID_1 = @EmployeeId 
    AND p.month_2 = @Month";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                    cmd.Parameters.AddWithValue("@Month", month);

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapToDto(reader);
                        }
                    }
                }
            }
            return null;
        }
        // ============================================================
        // GET ALL PAYSHEETS FOR AN EMPLOYEE
        // ============================================================

        // ============================================================
        // GET ALL PAYSHEETS FOR A MONTH
        // ============================================================

        // ============================================================
        // GET PAYSHEET BY ID
        // ============================================================

        // ============================================================
        // CREATE PAYSHEET
        // ============================================================
        public async Task<PaySheetDto?> CreatePaySheetAsync(PaySheetRequestDto request)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    INSERT INTO paySheet (
                        empID_1, month_2, basic_3, br_4, totalBasic_5, 
                        salaryForEpf_6, workingDays_7, noPayDays_8,
                        ot_NormalHours_9, otSundaHours_10, otNormal_11, otSunday_12,
                        extrOtHours_13, extraOt_14, addDayCount_15, addDay_16,
                        fixedAllowGradeAll_17, fixedAllowPosition_18, 
                        attendnceAllownce_19, otherAllowance_20,
                        totaEarnings_21, grossSalary_22,
                        lateHours_23, late_24, noPay_25,
                        advanced_26, otherDeduct_27, Otherloan_28,
                        epf8_29, welfareMember_30, welfareDead_31,
                        payeeStampDuty_32, easyPay1_33, easyPay2_34, easyPay3_35,
                        welfareLoan_36, donation_37,
                        totalDeduction_38, netSalary_39,
                        coinsBF_40, totalPayble_41, pay_42,
                        epf12_43, epf3_44, coinCF_45,
                        payeeNew_46, allow01_47, allow02_48, allow03_49, allow04_50
                    )
                    OUTPUT INSERTED.id_0
                    VALUES (
                        @EmployeeId, @Month, @Basic, @Br, @TotalBasic,
                        @SalaryForEpf, @WorkingDays, @NoPayDays,
                        @OtNormalHours, @OtSundayHours, @OtNormal, @OtSunday,
                        @ExtraOtHours, @ExtraOt, @AddDayCount, @AddDay,
                        @FixedAllowGradeAll, @FixedAllowPosition,
                        @AttendanceAllowance, @OtherAllowance,
                        @TotalEarnings, @GrossSalary,
                        @LateHours, @Late, @NoPay,
                        @Advanced, @OtherDeduct, @OtherLoan,
                        @Epf8, @WelfareMember, @WelfareDead,
                        @PayeeStampDuty, @EasyPay1, @EasyPay2, @EasyPay3,
                        @WelfareLoan, @Donation,
                        @TotalDeduction, @NetSalary,
                        @CoinsBF, @TotalPayable, @Pay,
                        @Epf12, @Epf3, @CoinCF,
                        @PayeeNew, @Allow01, @Allow02, @Allow03, @Allow04
                    )";

                using (var cmd = new SqlCommand(query, con))
                {
                    AddParameters(cmd, request);

                    await con.OpenAsync();
                    var id = await cmd.ExecuteScalarAsync();

                    if (id != null)
                    {
                        return await GetPaySheetByIdAsync(Convert.ToInt32(id));
                    }
                }
            }
            return null;
        }

        // ============================================================
        // UPDATE PAYSHEET
        // ============================================================
        public async Task<PaySheetDto?> UpdatePaySheetAsync(int id, PaySheetRequestDto request)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    UPDATE paySheet SET
                        month_2 = @Month,
                        basic_3 = @Basic,
                        br_4 = @Br,
                        totalBasic_5 = @TotalBasic,
                        salaryForEpf_6 = @SalaryForEpf,
                        workingDays_7 = @WorkingDays,
                        noPayDays_8 = @NoPayDays,
                        ot_NormalHours_9 = @OtNormalHours,
                        otSundaHours_10 = @OtSundayHours,
                        otNormal_11 = @OtNormal,
                        otSunday_12 = @OtSunday,
                        extrOtHours_13 = @ExtraOtHours,
                        extraOt_14 = @ExtraOt,
                        addDayCount_15 = @AddDayCount,
                        addDay_16 = @AddDay,
                        fixedAllowGradeAll_17 = @FixedAllowGradeAll,
                        fixedAllowPosition_18 = @FixedAllowPosition,
                        attendnceAllownce_19 = @AttendanceAllowance,
                        otherAllowance_20 = @OtherAllowance,
                        totaEarnings_21 = @TotalEarnings,
                        grossSalary_22 = @GrossSalary,
                        lateHours_23 = @LateHours,
                        late_24 = @Late,
                        noPay_25 = @NoPay,
                        advanced_26 = @Advanced,
                        otherDeduct_27 = @OtherDeduct,
                        Otherloan_28 = @OtherLoan,
                        epf8_29 = @Epf8,
                        welfareMember_30 = @WelfareMember,
                        welfareDead_31 = @WelfareDead,
                        payeeStampDuty_32 = @PayeeStampDuty,
                        easyPay1_33 = @EasyPay1,
                        easyPay2_34 = @EasyPay2,
                        easyPay3_35 = @EasyPay3,
                        welfareLoan_36 = @WelfareLoan,
                        donation_37 = @Donation,
                        totalDeduction_38 = @TotalDeduction,
                        netSalary_39 = @NetSalary,
                        coinsBF_40 = @CoinsBF,
                        totalPayble_41 = @TotalPayable,
                        pay_42 = @Pay,
                        epf12_43 = @Epf12,
                        epf3_44 = @Epf3,
                        coinCF_45 = @CoinCF,
                        payeeNew_46 = @PayeeNew,
                        allow01_47 = @Allow01,
                        allow02_48 = @Allow02,
                        allow03_49 = @Allow03,
                        allow04_50 = @Allow04
                    WHERE id_0 = @Id";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    AddParameters(cmd, request);

                    await con.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        return await GetPaySheetByIdAsync(id);
                    }
                }
            }
            return null;
        }

        // ============================================================
        // DELETE PAYSHEET
        // ============================================================
        public async Task<bool> DeletePaySheetAsync(int id)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = "DELETE FROM paySheet WHERE id_0 = @Id";
                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    await con.OpenAsync();
                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        // ============================================================
        // CALCULATE PAYSHEET
        // ============================================================
        public async Task<PaySheetDto?> CalculatePaySheetAsync(int employeeId, string month)
        {
            // Check if paysheet already exists
            var existing = await GetPaySheetByEmployeeAndMonthAsync(employeeId, month);
            if (existing != null)
            {
                return existing;
            }

            // TODO: Implement actual payroll calculation logic here
            // This would:
            // 1. Get employee details (basic salary, allowances, etc.)
            // 2. Get attendance/leave data for the month
            // 3. Calculate earnings, deductions, and net pay
            // 4. Create and return the paysheet

            throw new NotImplementedException("Payroll calculation not implemented yet");
        }

        // ============================================================
        // GET PAYSHEETS BY COMPANY AND MONTH
        // ============================================================
        // ============================================================
        // GET ALL PAYSHEETS FOR AN EMPLOYEE
        // ============================================================
        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAsync(int employeeId)
        {
            Console.WriteLine("222222");
            var result = new List<PaySheetDto>();

            using (var con = new SqlConnection(_connectionString))
            {
                // ✅ FIXED: Use correct column names
                string query = @"
            SELECT 
                p.*,
                e.FirstName + ' ' + e.LastName AS EmployeeName,
                e.EmployeeNo,
                d.DepartmentName AS Department
            FROM paySheet p
            INNER JOIN Employees e ON p.empID_1 = e.Id
            LEFT JOIN Company_Departments d ON e.DepartmentId = d.Id
            WHERE p.empID_1 = @EmployeeId
            ORDER BY p.month_2 DESC";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@EmployeeId", employeeId);

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(MapToDto(reader));
                        }
                    }
                }
            }
            return result;
        }

        // ============================================================
        // GET ALL PAYSHEETS FOR A MONTH
        // ============================================================
        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByMonthAsync(string month)
        {
            Console.WriteLine("3333333");
            var result = new List<PaySheetDto>();

            using (var con = new SqlConnection(_connectionString))
            {
                // ✅ FIXED: Use correct column names
                string query = @"
            SELECT 
                p.*,
                e.FirstName + ' ' + e.LastName AS EmployeeName,
                e.EmployeeNo,
                d.DepartmentName AS Department
            FROM paySheet p
            INNER JOIN Employees e ON p.empID_1 = e.Id
            LEFT JOIN Company_Departments d ON e.DepartmentId = d.Id
            WHERE p.month_2 = @Month
            ORDER BY e.FirstName";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Month", month);

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(MapToDto(reader));
                        }
                    }
                }
            }
            return result;
        }

        // ============================================================
        // GET PAYSHEET BY ID
        // ============================================================
        public async Task<PaySheetDto?> GetPaySheetByIdAsync(int id)
        {
            Console.WriteLine("444444");
            using (var con = new SqlConnection(_connectionString))
            {
                // ✅ FIXED: Use correct column names
                string query = @"
            SELECT 
                p.*,
                e.FirstName + ' ' + e.LastName AS EmployeeName,
                e.EmployeeNo,
                d.DepartmentName AS Department
            FROM paySheet p
            INNER JOIN Employees e ON p.empID_1 = e.Id
            LEFT JOIN Company_Departments d ON e.DepartmentId = d.Id
            WHERE p.id_0 = @Id";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Id", id);

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapToDto(reader);
                        }
                    }
                }
            }
            return null;
        }

        // ============================================================
        // GET PAYSHEETS BY COMPANY AND MONTH
        // ============================================================
        public async Task<IEnumerable<PaySheetDto>> GetPaySheetsByCompanyAndMonthAsync(int companyId, string month)
        {
            Console.WriteLine("5555555");
            var result = new List<PaySheetDto>();

            using (var con = new SqlConnection(_connectionString))
            {
                // ✅ FIXED: Use correct column names
                string query = @"
            SELECT 
                p.*,
                e.FirstName + ' ' + e.LastName AS EmployeeName,
                e.EmployeeNo,
                d.DepartmentName AS Department
            FROM paySheet p
            INNER JOIN Employees e ON p.empID_1 = e.Id
            LEFT JOIN Company_Departments d ON e.DepartmentId = d.Id
            WHERE e.CompanyId = @CompanyId AND p.month_2 = @Month
            ORDER BY e.FirstName";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyId);
                    cmd.Parameters.AddWithValue("@Month", month);

                    await con.OpenAsync();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(MapToDto(reader));
                        }
                    }
                }
            }
            return result;
        }
        // ============================================================
        // PROCESS PAYROLL
        // ============================================================
        public async Task<bool> ProcessPayrollAsync(int companyId, string month)
        {
            // TODO: Implement payroll processing logic here
            // This would:
            // 1. Get all active employees for the company
            // 2. Calculate paysheet for each employee
            // 3. Save all paysheets
            // 4. Return success/failure

            throw new NotImplementedException("Payroll processing not implemented yet");
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private PaySheetDto MapToDto(SqlDataReader reader)
        {
            return new PaySheetDto
            {
                Id = reader.GetInt32(reader.GetOrdinal("id_0")),
                EmployeeId = reader.GetInt32(reader.GetOrdinal("empID_1")),
                EmployeeName = reader["EmployeeName"]?.ToString() ?? "",
                EmployeeNo = reader["EmployeeNo"]?.ToString() ?? "",
                Department = reader["Department"]?.ToString() ?? "",
                Month = reader["month_2"]?.ToString() ?? "",

                // Earnings
                Basic = GetDouble(reader, "basic_3"),
                Br = GetDouble(reader, "br_4"),
                TotalBasic = GetDouble(reader, "totalBasic_5"),
                SalaryForEpf = GetDouble(reader, "salaryForEpf_6"),
                WorkingDays = GetDouble(reader, "workingDays_7"),
                NoPayDays = GetDouble(reader, "noPayDays_8"),

                // Overtime
                OtNormalHours = GetDouble(reader, "ot_NormalHours_9"),
                OtSundayHours = GetDouble(reader, "otSundaHours_10"),
                OtNormal = GetDouble(reader, "otNormal_11"),
                OtSunday = GetDouble(reader, "otSunday_12"),
                ExtraOtHours = GetDouble(reader, "extrOtHours_13"),
                ExtraOt = GetDouble(reader, "extraOt_14"),
                AddDayCount = GetDouble(reader, "addDayCount_15"),
                AddDay = GetDouble(reader, "addDay_16"),

                // Allowances
                FixedAllowGradeAll = GetDouble(reader, "fixedAllowGradeAll_17"),
                FixedAllowPosition = GetDouble(reader, "fixedAllowPosition_18"),
                AttendanceAllowance = GetDouble(reader, "attendnceAllownce_19"),
                OtherAllowance = GetDouble(reader, "otherAllowance_20"),
                TotalEarnings = GetDouble(reader, "totaEarnings_21"),
                GrossSalary = GetDouble(reader, "grossSalary_22"),

                // Deductions
                LateHours = GetDouble(reader, "lateHours_23"),
                Late = GetDouble(reader, "late_24"),
                NoPay = GetDouble(reader, "noPay_25"),
                Advanced = GetDouble(reader, "advanced_26"),
                OtherDeduct = GetDouble(reader, "otherDeduct_27"),
                OtherLoan = GetDouble(reader, "Otherloan_28"),
                Epf8 = GetDouble(reader, "epf8_29"),
                WelfareMember = GetDouble(reader, "welfareMember_30"),
                WelfareDead = GetDouble(reader, "welfareDead_31"),
                PayeeStampDuty = GetDouble(reader, "payeeStampDuty_32"),
                EasyPay1 = GetDouble(reader, "easyPay1_33"),
                EasyPay2 = GetDouble(reader, "easyPay2_34"),
                EasyPay3 = GetDouble(reader, "easyPay3_35"),
                WelfareLoan = GetDouble(reader, "welfareLoan_36"),
                Donation = GetDouble(reader, "donation_37"),
                TotalDeduction = GetDouble(reader, "totalDeduction_38"),

                // Summary
                NetSalary = GetDouble(reader, "netSalary_39"),
                CoinsBF = GetDouble(reader, "coinsBF_40"),
                TotalPayable = GetDouble(reader, "totalPayble_41"),
                Pay = GetDouble(reader, "pay_42"),
                Epf12 = GetDouble(reader, "epf12_43"),
                Epf3 = GetDouble(reader, "epf3_44"),
                CoinCF = GetDouble(reader, "coinCF_45"),
                PayeeNew = GetDouble(reader, "payeeNew_46"),
                Allow01 = GetDouble(reader, "allow01_47"),
                Allow02 = GetDouble(reader, "allow02_48"),
                Allow03 = GetDouble(reader, "allow03_49"),
                Allow04 = GetDouble(reader, "allow04_50")
            };
        }

        private double? GetDouble(SqlDataReader reader, string columnName)
        {
            try
            {
                var value = reader[columnName];
                if (value == DBNull.Value)
                    return null;
                return Convert.ToDouble(value);
            }
            catch
            {
                return null;
            }
        }

        private void AddParameters(SqlCommand cmd, PaySheetRequestDto request)
        {
            cmd.Parameters.AddWithValue("@EmployeeId", request.EmployeeId);
            cmd.Parameters.AddWithValue("@Month", request.Month ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Basic", request.Basic ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Br", request.Br ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalBasic", request.TotalBasic ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@SalaryForEpf", request.SalaryForEpf ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@WorkingDays", request.WorkingDays ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NoPayDays", request.NoPayDays ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtNormalHours", request.OtNormalHours ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtSundayHours", request.OtSundayHours ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtNormal", request.OtNormal ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtSunday", request.OtSunday ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExtraOtHours", request.ExtraOtHours ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ExtraOt", request.ExtraOt ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AddDayCount", request.AddDayCount ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AddDay", request.AddDay ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FixedAllowGradeAll", request.FixedAllowGradeAll ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@FixedAllowPosition", request.FixedAllowPosition ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AttendanceAllowance", request.AttendanceAllowance ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtherAllowance", request.OtherAllowance ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalEarnings", request.TotalEarnings ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GrossSalary", request.GrossSalary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@LateHours", request.LateHours ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Late", request.Late ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NoPay", request.NoPay ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Advanced", request.Advanced ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtherDeduct", request.OtherDeduct ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@OtherLoan", request.OtherLoan ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Epf8", request.Epf8 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@WelfareMember", request.WelfareMember ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@WelfareDead", request.WelfareDead ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PayeeStampDuty", request.PayeeStampDuty ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EasyPay1", request.EasyPay1 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EasyPay2", request.EasyPay2 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@EasyPay3", request.EasyPay3 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@WelfareLoan", request.WelfareLoan ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Donation", request.Donation ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalDeduction", request.TotalDeduction ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@NetSalary", request.NetSalary ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CoinsBF", request.CoinsBF ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@TotalPayable", request.TotalPayable ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Pay", request.Pay ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Epf12", request.Epf12 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Epf3", request.Epf3 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CoinCF", request.CoinCF ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@PayeeNew", request.PayeeNew ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Allow01", request.Allow01 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Allow02", request.Allow02 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Allow03", request.Allow03 ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Allow04", request.Allow04 ?? (object)DBNull.Value);
        }
    }
}