using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Threading.Tasks;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoanController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        

        public LoanController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // GET: api/loan/types
        [HttpGet("types")]
        public IActionResult GetLoanTypes()
        {
            try
            {
                var loanTypes = new List<string>
                {
                    "Personal Loan",
                    "Vehicle Loan", 
                    "Housing Loan",
                    "Education Loan",
                    "Medical Loan",
                    "Emergency Loan",
                    "Other Loan"
                };

                return Ok(new { success = true, data = loanTypes });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/loan/employee/{employeeId}/{companyId}/{periodYear}/{periodMonth}
        [HttpGet("employee/{employeeId}/{companyId}/{periodYear}/{periodMonth}")]
        public IActionResult GetEmployeeLoans(int employeeId, int companyId, string periodYear, string periodMonth)
        {
            var period = periodYear + "/" + periodMonth;
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // First check if loan_paid table exists
                    string checkTableQuery = @"
                        SELECT COUNT(*) 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_NAME = 'loan_paid'";
                        
                    using (var checkCmd = new SqlCommand(checkTableQuery, connection))
                    {
                        var tableExists = (int)checkCmd.ExecuteScalar() > 0;
                        
                        string query;
                        if (tableExists)
                        {
                            query = @"
                                SELECT 
                                    l.*,
                                    e.SystemName,
                                    e.FirstName + ' ' + e.LastName as EmployeeName,
                                    ru.FirstName + ' ' + ru.LastName as RequestedByName,
                                    au.FirstName + ' ' + au.LastName as ApprovedByName,
                                    -- Calculate monthly installment
                                    CAST(l.loan_amount AS DECIMAL(18,2)) / CASE WHEN l.Installment = 0 THEN 1 ELSE l.Installment END as MonthlyInstallment,
                                    -- Count actual paid installments from loan_paid table
                                    ISNULL((SELECT COUNT(*) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as PaidInstallments,
                                    -- Calculate pending installments: original - paid
                                    l.Installment - ISNULL((SELECT COUNT(*) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as PendingInstallments,
                                    -- Calculate settled amount from loan_paid table
                                    ISNULL((SELECT SUM(lp.paid_amount) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as CalculatedSettledAmount,
                                    -- Calculate balance amount dynamically
                                    l.loan_amount - ISNULL((SELECT SUM(lp.paid_amount) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as CalculatedBalanceAmount
                                FROM Loans l
                                LEFT JOIN Employees e ON l.employee_id = e.Id
                                LEFT JOIN Employees ru ON l.requested_by = ru.Id
                                LEFT JOIN Employees au ON l.approved_by = au.Id
                                WHERE l.employee_id = @EmployeeId and l.deleted= 0
                                AND l.company_id = @CompanyId
                                AND l.Setteled = 0
                                ORDER BY l.taken_date DESC";
                        }
                        else
                        {
                            // Fallback: Use existing amounts from Loans table
                            query = @"
                                SELECT 
                                    l.*,
                                    e.SystemName,
                                    e.FirstName + ' ' + e.LastName as EmployeeName,
                                    ru.FirstName + ' ' + ru.LastName as RequestedByName,
                                    au.FirstName + ' ' + au.LastName as ApprovedByName,
                                    -- Calculate monthly installment
                                    CAST(l.loan_amount AS DECIMAL(18,2)) / CASE WHEN l.Installment = 0 THEN 1 ELSE l.Installment END as MonthlyInstallment,
                                    -- Calculate paid installments based on settled amount
                                    CASE 
                                        WHEN l.loan_amount > 0 AND l.Installment > 0 
                                        THEN CAST(FLOOR(CAST(l.setteled_amount AS DECIMAL(18,2)) / (CAST(l.loan_amount AS DECIMAL(18,2)) / l.Installment)) AS INT)
                                        ELSE 0
                                    END as PaidInstallments,
                                    -- Calculate pending installments: original - paid
                                    l.Installment - 
                                    CASE 
                                        WHEN l.loan_amount > 0 AND l.Installment > 0 
                                        THEN CAST(FLOOR(CAST(l.setteled_amount AS DECIMAL(18,2)) / (CAST(l.loan_amount AS DECIMAL(18,2)) / l.Installment)) AS INT)
                                        ELSE 0
                                    END as PendingInstallments,
                                    -- Use existing amounts
                                    l.setteled_amount as CalculatedSettledAmount,
                                    l.balance_amount as CalculatedBalanceAmount
                                FROM Loans l
                                LEFT JOIN Employees e ON l.employee_id = e.Id
                                LEFT JOIN Employees ru ON l.requested_by = ru.Id
                                LEFT JOIN Employees au ON l.approved_by = au.Id
                                WHERE l.employee_id = @EmployeeId 
                                AND l.company_id = @CompanyId
                                AND l.Setteled = 0
                                ORDER BY l.taken_date DESC";
                        }

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@EmployeeId", employeeId);
                            command.Parameters.AddWithValue("@CompanyId", companyId);

                            using (var reader = command.ExecuteReader())
                            {
                                var loans = new List<Loan>();
                                
                                while (reader.Read())
                                {
                                    var loan = new Loan
                                    {
                                        Id = reader.GetInt32("id"),
                                        EmployeeId = reader.GetInt32("employee_id"),
                                        CompanyId = reader.GetInt32("company_id"),
                                        Period = reader.GetString("period"),
                                        LoanType = reader.GetString("loan_type"),
                                        Installment = reader.GetInt32("Installment"),
                                        LoanAmount = reader.GetDecimal("loan_amount"),
                                        SettledAmount = SafeGetDecimal(reader, "CalculatedSettledAmount"),
                                        BalanceAmount = SafeGetDecimal(reader, "CalculatedBalanceAmount"),
                                        TakenDate = reader.GetDateTime("taken_date"),
                                        RequestedDate = reader.GetDateTime("requested_date"),
                                        RequestType = reader.GetString("request_type"),
                                        RequestedBy = reader.GetInt32("requested_by"),
                                        Approved = reader.GetBoolean("approved"),
                                        Declined = reader.GetBoolean("declined"),
                                        Remark = reader.IsDBNull("remark") ? null : reader.GetString("remark"),
                                        CreatedDate = reader.GetDateTime("created_date"),
                                        Active = reader.GetBoolean("Active"),
                                        Settled = reader.GetBoolean("Setteled"),
                                        EmployeeName = reader.GetString("EmployeeName"),
                                        SystemName = reader.GetString("SystemName"),
                                        RequestedByName = reader.IsDBNull("RequestedByName") ? "System" : reader.GetString("RequestedByName"),
                                        ApprovedByName = reader.IsDBNull("ApprovedByName") ? null : reader.GetString("ApprovedByName"),
                                        MonthlyInstallment = SafeGetDecimal(reader, "MonthlyInstallment"),
                                        PaidInstallments = reader.GetInt32("PaidInstallments"),
                                        PendingInstallments = reader.GetInt32("PendingInstallments")
                                    };

                                    if (!reader.IsDBNull("approved_by"))
                                        loan.ApprovedBy = reader.GetInt32("approved_by");
                                    if (!reader.IsDBNull("approved_date"))
                                        loan.ApprovedDate = reader.GetDateTime("approved_date");
                                    if (!reader.IsDBNull("modified_date"))
                                        loan.ModifiedDate = reader.GetDateTime("modified_date");

                                    loans.Add(loan);
                                }

                                return Ok(new { success = true, data = loans });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error loading loans",
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        // POST: api/loan
        [HttpPost]
        public IActionResult CreateLoan([FromBody] LoanRequest request)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    var query = @"
                        INSERT INTO Loans (
                            employee_id, company_id, period, loan_type, Installment, 
                            loan_amount, setteled_amount, balance_amount, taken_date,
                            requested_date, request_type, requested_by, approved_by, approved, declined,
                            remark, created_date, Active, Setteled, approved_date
                        ) VALUES (
                            @EmployeeId, @CompanyId, @Period, @LoanType, @Installment,
                            @LoanAmount, 0, @LoanAmount, @TakenDate,
                            GETDATE(), @RequestType, @RequestedBy, @ApprovedBy, 1, 0,
                            @Remark, GETDATE(), 1, 0, GETDATE()
                        ); SELECT SCOPE_IDENTITY();";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@EmployeeId", request.EmployeeId);
                        command.Parameters.AddWithValue("@CompanyId", request.CompanyId);
                        command.Parameters.AddWithValue("@Period", request.Period);
                        command.Parameters.AddWithValue("@LoanType", request.LoanType);
                        command.Parameters.AddWithValue("@Installment", request.Installment);
                        command.Parameters.AddWithValue("@LoanAmount", request.Amount);
                        command.Parameters.AddWithValue("@TakenDate", request.TakenDate);
                        command.Parameters.AddWithValue("@RequestType", "System");
                        command.Parameters.AddWithValue("@RequestedBy", request.UserId);
                        command.Parameters.AddWithValue("@ApprovedBy", request.UserId);
                        command.Parameters.AddWithValue("@Remark", request.Purpose ?? string.Empty);

                        var loanId = Convert.ToInt32(command.ExecuteScalar());

                        // Add creation history
                         AddLoanHistory(
                            loanId, 
                            "Loan Created", 
                            "N/A", 
                            "Active", 
                            request.UserId, 
                           $"{request.LoanType} loan created for amount LKR {request.Amount:N2}"
                        );

                        return Ok(new { 
                            success = true, 
                            message = "Loan created successfully", 
                            data = new { id = loanId } 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // PUT: api/loan/status
        [HttpPut("status")]
        public IActionResult UpdateLoanStatus([FromBody] LoanStatusUpdate update)
        {
            try
            {
                if (!int.TryParse(update.LoanId, out int loanId))
                {
                    return BadRequest(new { success = false, message = "Invalid Loan ID format" });
                }
                
                if (!int.TryParse(update.UserId, out int userId))
                {
                    return BadRequest(new { success = false, message = "Invalid User ID format" });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Get current status before update
                    string previousStatus = GetCurrentLoanStatus(loanId, connection);
                    
                    var query = @"
                        UPDATE Loans 
                        SET Active = @Active, 
                            modified_date = GETDATE()
                        WHERE id = @LoanId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Active", update.Active);
                        command.Parameters.AddWithValue("@LoanId", loanId);

                        var rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Get new status after update
                            string newStatus = GetCurrentLoanStatus(loanId, connection);
                            
                            // Add status change history
                            AddLoanHistory(
                                loanId,
                                "Status Updated",
                                previousStatus,
                                newStatus,
                                userId,
                                $"Loan {(update.Active ? "activated" : "deactivated")}"
                            );

                            return Ok(new { 
                                success = true, 
                                message = "Loan status updated successfully" 
                            });
                        }
                        else
                        {
                            return NotFound(new { success = false, message = "Loan not found" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // POST: api/loan/hold
        [HttpPost("hold")]
        public IActionResult CreateLoanHold([FromBody] LoanHoldRequest request)
        {
            try
            {
                if (!int.TryParse(request.LoanId, out int loanId))
                {
                    return BadRequest(new { success = false, message = "Invalid Loan ID format" });
                }
                
                if (!int.TryParse(request.EmployeeId, out int employeeId))
                {
                    return BadRequest(new { success = false, message = "Invalid Employee ID format" });
                }
                
                if (!int.TryParse(request.CompanyId, out int companyId))
                {
                    return BadRequest(new { success = false, message = "Invalid Company ID format" });
                }
                
                if (!int.TryParse(request.UserId, out int userId))
                {
                    return BadRequest(new { success = false, message = "Invalid User ID format" });
                }

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Get current status before hold
                    string previousStatus = GetCurrentLoanStatus(loanId, connection);
                    
                    // Update loan to inactive when putting on hold
                    var updateLoanQuery = @"
                        UPDATE Loans 
                        SET Active = 0, 
                            modified_date = GETDATE()
                        WHERE id = @LoanId";
                        
                    using (var updateCmd = new SqlCommand(updateLoanQuery, connection))
                    {
                        updateCmd.Parameters.AddWithValue("@LoanId", loanId);
                        updateCmd.ExecuteNonQuery();
                    }
                    
                    // Get current period
                    var currentPeriodQuery = "SELECT TOP 1 period FROM Loans WHERE id = @LoanId";
                    string currentPeriod;
                    
                    using (var periodCmd = new SqlCommand(currentPeriodQuery, connection))
                    {
                        periodCmd.Parameters.AddWithValue("@LoanId", loanId);
                        currentPeriod = periodCmd.ExecuteScalar()?.ToString();
                    }
                    
                    if (string.IsNullOrEmpty(currentPeriod))
                    {
                        return BadRequest(new { success = false, message = "Could not determine current period" });
                    }
                    
                    // Parse the period with month name (e.g., "2025/February")
                    var periodParts = currentPeriod.Split('/');
                    if (periodParts.Length != 2)
                    {
                        return BadRequest(new { success = false, message = "Invalid period format" });
                    }
                    
                    if (!int.TryParse(periodParts[0], out int fromYear))
                    {
                        return BadRequest(new { success = false, message = "Invalid year in period" });
                    }
                    
                    // Convert month name to month number
                    int fromMonth = MonthNameToNumber(periodParts[1]);
                    if (fromMonth == -1)
                    {
                        return BadRequest(new { success = false, message = "Invalid month name in period" });
                    }
                    
                    // Calculate to period
                    var toMonth = fromMonth + request.HoldMonths;
                    var toYear = fromYear;
                    
                    while (toMonth > 12)
                    {
                        toMonth -= 12;
                        toYear++;
                    }
                    
                    // Convert month number back to month name for storage
                    string toMonthName = MonthNumberToName(toMonth);
                    var toPeriod = $"{toYear}/{toMonthName}";
                    
                    var query = @"
                        INSERT INTO Loans_hold (
                            loan_id, employee_id, company_id, period_from, period_to, 
                            hold_count, hold_date
                        ) VALUES (
                            @LoanId, @EmployeeId, @CompanyId, @PeriodFrom, @PeriodTo,
                            @HoldCount, GETDATE()
                        ); SELECT SCOPE_IDENTITY();";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LoanId", loanId);
                        command.Parameters.AddWithValue("@EmployeeId", employeeId);
                        command.Parameters.AddWithValue("@CompanyId", companyId);
                        command.Parameters.AddWithValue("@PeriodFrom", currentPeriod);
                        command.Parameters.AddWithValue("@PeriodTo", toPeriod);
                        command.Parameters.AddWithValue("@HoldCount", request.HoldMonths);

                        var holdId = Convert.ToInt32(command.ExecuteScalar());

                        // Add hold history
                        AddLoanHistory(
                            loanId,
                            "Loan Held",
                            previousStatus,
                            "On Hold",
                            userId,
                            $"Loan put on hold for {request.HoldMonths} months until {toPeriod}"
                        );

                        return Ok(new { 
                            success = true, 
                            message = "Loan hold created successfully", 
                            data = new { id = holdId } 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE: api/loan/hold/{id}
        [HttpDelete("hold/{id}")]
        public IActionResult DeleteLoanHold(int id)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Get loan ID from hold record before deleting
                    var getLoanQuery = "SELECT loan_id FROM Loans_hold WHERE id = @Id";
                    int loanId;
                    
                    using (var getCmd = new SqlCommand(getLoanQuery, connection))
                    {
                        getCmd.Parameters.AddWithValue("@Id", id);
                        loanId = Convert.ToInt32(getCmd.ExecuteScalar());
                    }
                    
                    // Get user ID (you might need to pass this in the request)
                    int userId = 1; // Replace with actual user ID from request
                    
                    var query = "DELETE FROM Loans_hold WHERE id = @Id";
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        
                        var rowsAffected = command.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            // Add hold release history
                            AddLoanHistory(
                                loanId,
                                "Hold Released",
                                "On Hold",
                                "Active",
                                userId,
                                "Loan hold released and reactivated"
                            );
                            
                            // Reactivate the loan
                            var updateQuery = "UPDATE Loans SET Active = 1 WHERE id = @LoanId";
                            using (var updateCmd = new SqlCommand(updateQuery, connection))
                            {
                                updateCmd.Parameters.AddWithValue("@LoanId", loanId);
                                updateCmd.ExecuteNonQuery();
                            }

                            return Ok(new { success = true, message = "Loan hold deleted successfully" });
                        }
                        else
                        {
                            return NotFound(new { success = false, message = "Loan hold not found" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // DELETE: api/loan/{id}
        [HttpDelete("{id}")]
        public IActionResult DeleteLoan(int id)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Get current status before deletion
                    string previousStatus = GetCurrentLoanStatus(id, connection);
                    
                    // Get user ID (you might need to pass this in the request)
                    int userId = 1; // Replace with actual user ID from request
                    
                    var query = "update Loans set deleted='true' WHERE id = @Id";
                    
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        
                        var rowsAffected = command.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            // Add deletion history
                            AddLoanHistory(
                                id,
                                "Loan Deleted",
                                previousStatus,
                                "Deleted",
                                userId,
                                "Loan permanently deleted from system"
                            );

                            return Ok(new { success = true, message = "Loan deleted successfully" });
                        }
                        else
                        {
                            return NotFound(new { success = false, message = "Loan not found" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // GET: api/loan/{id}
        [HttpGet("{id}")]
        public IActionResult GetLoan(int id)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    var query = @"
                        SELECT 
                            l.*,
                            e.SystemName,
                            e.FirstName + ' ' + e.LastName as EmployeeName,
                            ru.FirstName + ' ' + ru.LastName as RequestedByName,
                            au.FirstName + ' ' + au.LastName as ApprovedByName,
                            -- Calculate monthly installment
                            CAST(l.loan_amount AS DECIMAL(18,2)) / CASE WHEN l.Installment = 0 THEN 1 ELSE l.Installment END as MonthlyInstallment,
                            -- Count actual paid installments from loan_paid table
                            ISNULL((SELECT COUNT(*) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as PaidInstallments,
                            -- Calculate pending installments: original - paid
                            l.Installment - ISNULL((SELECT COUNT(*) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as PendingInstallments,
                            -- Calculate settled amount from loan_paid table
                            ISNULL((SELECT SUM(lp.paid_amount) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as CalculatedSettledAmount,
                            -- Calculate balance amount dynamically
                            l.loan_amount - ISNULL((SELECT SUM(lp.paid_amount) FROM loan_paid lp WHERE lp.loan_id = l.id), 0) as CalculatedBalanceAmount
                        FROM Loans l
                        LEFT JOIN Employees e ON l.employee_id = e.Id
                        LEFT JOIN Employees ru ON l.requested_by = ru.Id
                        LEFT JOIN Employees au ON l.approved_by = au.Id
                        WHERE l.id = @LoanId";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LoanId", id);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var loan = new Loan
                                {
                                    Id = reader.GetInt32("id"),
                                    EmployeeId = reader.GetInt32("employee_id"),
                                    CompanyId = reader.GetInt32("company_id"),
                                    Period = reader.GetString("period"),
                                    LoanType = reader.GetString("loan_type"),
                                    Installment = reader.GetInt32("Installment"),
                                    LoanAmount = reader.GetDecimal("loan_amount"),
                                    SettledAmount = SafeGetDecimal(reader, "CalculatedSettledAmount"),
                                    BalanceAmount = SafeGetDecimal(reader, "CalculatedBalanceAmount"),
                                    TakenDate = reader.GetDateTime("taken_date"),
                                    RequestedDate = reader.GetDateTime("requested_date"),
                                    RequestType = reader.GetString("request_type"),
                                    RequestedBy = reader.GetInt32("requested_by"),
                                    Approved = reader.GetBoolean("approved"),
                                    Declined = reader.GetBoolean("declined"),
                                    Remark = reader.IsDBNull("remark") ? null : reader.GetString("remark"),
                                    CreatedDate = reader.GetDateTime("created_date"),
                                    Active = reader.GetBoolean("Active"),
                                    Settled = reader.GetBoolean("Setteled"),
                                    EmployeeName = reader.GetString("EmployeeName"),
                                    SystemName = reader.GetString("SystemName"),
                                    RequestedByName = reader.IsDBNull("RequestedByName") ? "System" : reader.GetString("RequestedByName"),
                                    ApprovedByName = reader.IsDBNull("ApprovedByName") ? null : reader.GetString("ApprovedByName"),
                                    MonthlyInstallment = SafeGetDecimal(reader, "MonthlyInstallment"),
                                    PaidInstallments = reader.GetInt32("PaidInstallments"),
                                    PendingInstallments = reader.GetInt32("PendingInstallments")
                                };

                                if (!reader.IsDBNull("approved_by"))
                                    loan.ApprovedBy = reader.GetInt32("approved_by");
                                if (!reader.IsDBNull("approved_date"))
                                    loan.ApprovedDate = reader.GetDateTime("approved_date");
                                if (!reader.IsDBNull("modified_date"))
                                    loan.ModifiedDate = reader.GetDateTime("modified_date");

                                return Ok(new { success = true, data = loan });
                            }
                            else
                            {
                                return NotFound(new { success = false, message = "Loan not found" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false, 
                    message = "Error loading loan details",
                    error = ex.Message
                });
            }
        }

        // GET: api/loan/payments/{loanId}
        [HttpGet("payments/{loanId}")]
        public IActionResult GetLoanPayments(int loanId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // First check if loan_paid table exists
                    string checkTableQuery = @"
                        SELECT COUNT(*) 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_NAME = 'loan_paid'";
                        
                    using (var checkCmd = new SqlCommand(checkTableQuery, connection))
                    {
                        var tableExists = (int)checkCmd.ExecuteScalar() > 0;
                        
                        if (tableExists)
                        {
                            var query = @"
                                SELECT 
                                    lp.id,
                                    lp.loan_id,
                                    lp.employee_id, 
                                    lp.company_id,
                                    lp.paid_amount,
                                    lp.period,
                                    e.FirstName + ' ' + e.LastName as EmployeeName
                                FROM loan_paid lp
                                INNER JOIN Employees e ON lp.employee_id = e.Id
                                WHERE lp.loan_id = @LoanId
                                ORDER BY lp.id DESC";

                            using (var command = new SqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@LoanId", loanId);

                                using (var reader = command.ExecuteReader())
                                {
                                    var payments = new List<LoanPayment>();
                                    
                                    while (reader.Read())
                                    {
                                        var payment = new LoanPayment
                                        {
                                            Id = reader.GetInt32("id"),
                                            LoanId = reader.GetInt32("loan_id"),
                                            EmployeeId = reader.GetInt32("employee_id"),
                                            CompanyId = reader.GetInt32("company_id"),
                                            Amount = reader.GetDecimal("paid_amount"),
                                            Period = reader.GetString("period"),
                                            EmployeeName = reader.GetString("EmployeeName")
                                        };
                                        
                                        payments.Add(payment);
                                    }

                                    return Ok(new { success = true, data = payments });
                                }
                            }
                        }
                        else
                        {
                            return Ok(new { success = true, data = new List<LoanPayment>() });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = true, data = new List<LoanPayment>() });
            }
        }

        // GET: api/loan/history/{loanId}
        [HttpGet("history/{loanId}")]
        public IActionResult GetLoanHistory(int loanId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Check if loan_history table exists
                    string checkTableQuery = @"
                        SELECT COUNT(*) 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_NAME = 'loan_history'";
                        
                    using (var checkCmd = new SqlCommand(checkTableQuery, connection))
                    {
                        var tableExists = (int)checkCmd.ExecuteScalar() > 0;
                        
                        if (tableExists)
                        {
                            var query = @"
                                SELECT 
                                    lh.*,
                                    e.FirstName + ' ' + e.LastName as ChangedByName
                                FROM loan_history lh
                                LEFT JOIN Employees e ON lh.changed_by = e.Id
                                WHERE lh.loan_id = @LoanId
                                ORDER BY lh.change_date DESC";

                            using (var command = new SqlCommand(query, connection))
                            {
                                command.Parameters.AddWithValue("@LoanId", loanId);

                                using (var reader = command.ExecuteReader())
                                {
                                    var history = new List<LoanHistory>();
                                    
                                    while (reader.Read())
                                    {
                                        var historyItem = new LoanHistory
                                        {
                                            Id = reader.GetInt32("id"),
                                            LoanId = reader.GetInt32("loan_id"),
                                            ChangeDate = reader.GetDateTime("change_date"),
                                            Action = reader.GetString("action"),
                                            PreviousStatus = reader.GetString("previous_status"),
                                            NewStatus = reader.GetString("new_status"),
                                            ChangedBy = reader.IsDBNull("ChangedByName") ? "System" : reader.GetString("ChangedByName"),
                                            Remarks = reader.IsDBNull("remarks") ? null : reader.GetString("remarks")
                                        };
                                        
                                        history.Add(historyItem);
                                    }

                                    return Ok(new { success = true, data = history });
                                }
                            }
                        }
                        else
                        {
                            return Ok(new { success = true, data = new List<LoanHistory>() });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = true, data = new List<LoanHistory>() });
            }
        }

        // GET: api/loan/hold/{loanId}
        [HttpGet("hold/{loanId}")]
        public IActionResult GetLoanHolds(int loanId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    var query = @"
                        SELECT 
                            lh.*,
                            l.loan_type,
                            l.loan_amount
                        FROM Loans_hold lh
                        INNER JOIN Loans l ON lh.loan_id = l.id
                        WHERE lh.loan_id = @LoanId
                        ORDER BY lh.hold_date DESC";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LoanId", loanId);

                        using (var reader = command.ExecuteReader())
                        {
                            var loanHolds = new List<LoanHold>();
                            
                            while (reader.Read())
                            {
                                var loanHold = new LoanHold
                                {
                                    Id = reader.GetInt32("id"),
                                    LoanId = reader.GetInt32("loan_id"),
                                    EmployeeId = reader.GetInt32("employee_id"),
                                    CompanyId = reader.GetInt32("company_id"),
                                    PeriodFrom = reader.GetString("period_from"),
                                    PeriodTo = reader.GetString("period_to"),
                                    HoldCount = reader.GetInt32("hold_count"),
                                    HoldDate = reader.GetDateTime("hold_date"),
                                    LoanType = reader.GetString("loan_type"),
                                    LoanAmount = reader.GetDecimal("loan_amount")
                                };
                                
                                loanHolds.Add(loanHold);
                            }

                            return Ok(new { success = true, data = loanHolds });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        #region Helper Methods

       // Helper method to add loan history
        private bool AddLoanHistory(int loanId, string action, string previousStatus, string newStatus, int changedBy, string remarks = null)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    var query = @"
                        INSERT INTO loan_history (
                            loan_id, action, previous_status, new_status, changed_by, remarks
                        ) VALUES (
                            @LoanId, @Action, @PreviousStatus, @NewStatus, @ChangedBy, @Remarks
                        )";
                        
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@LoanId", loanId);
                        command.Parameters.AddWithValue("@Action", action);
                        command.Parameters.AddWithValue("@PreviousStatus", previousStatus ?? "N/A");
                        command.Parameters.AddWithValue("@NewStatus", newStatus ?? "N/A");
                        command.Parameters.AddWithValue("@ChangedBy", changedBy);
                        command.Parameters.AddWithValue("@Remarks", remarks ?? string.Empty);
                        
                        return command.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                Console.WriteLine($"Error adding loan history: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        // Helper method to get current loan status
        private string GetCurrentLoanStatus(int loanId, SqlConnection connection)
        {
            var query = @"
                SELECT 
                    CASE 
                        WHEN Active = 1 THEN 'Active'
                        WHEN Setteled = 1 THEN 'Settled' 
                        WHEN declined = 1 THEN 'Declined'
                        WHEN approved = 1 THEN 'Approved'
                        ELSE 'Pending'
                    END as Status
                FROM Loans 
                WHERE id = @LoanId";
                
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@LoanId", loanId);
                return command.ExecuteScalar()?.ToString() ?? "Unknown";
            }
        }

        // Helper method for safe decimal conversion
        private decimal SafeGetDecimal(SqlDataReader reader, string columnName)
        {
            try
            {
                var value = reader[columnName];
                if (value == DBNull.Value)
                    return 0;
                
                return Convert.ToDecimal(value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting column {columnName} to decimal: {ex.Message}");
                return 0;
            }
        }

        // Helper method to convert month name to number
        private int MonthNameToNumber(string monthName)
        {
            return monthName.ToLower() switch
            {
                "january" => 1,
                "february" => 2,
                "march" => 3,
                "april" => 4,
                "may" => 5,
                "june" => 6,
                "july" => 7,
                "august" => 8,
                "september" => 9,
                "october" => 10,
                "november" => 11,
                "december" => 12,
                _ => -1
            };
        }

        // Helper method to convert month number to name
        private string MonthNumberToName(int monthNumber)
        {
            return monthNumber switch
            {
                1 => "January",
                2 => "February",
                3 => "March",
                4 => "April",
                5 => "May",
                6 => "June",
                7 => "July",
                8 => "August",
                9 => "September",
                10 => "October",
                11 => "November",
                12 => "December",
                _ => "Unknown"
            };
        }

        #endregion
    }

    #region Model Classes

    public class Loan
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string Period { get; set; }
        public string LoanType { get; set; }
        public int Installment { get; set; }
        public decimal LoanAmount { get; set; }
        public decimal SettledAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public DateTime TakenDate { get; set; }
        public DateTime RequestedDate { get; set; }
        public string RequestType { get; set; }
        public int RequestedBy { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public bool Approved { get; set; }
        public bool Declined { get; set; }
        public string Remark { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool Active { get; set; }
        public bool Settled { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string EmployeeName { get; set; }
        public string SystemName { get; set; }
        public string RequestedByName { get; set; }
        public string ApprovedByName { get; set; }
        public decimal MonthlyInstallment { get; set; }
        public int PaidInstallments { get; set; }
        public int PendingInstallments { get; set; }
    }

    public class LoanRequest
    {
        [Required]
        public int EmployeeId { get; set; }
        [Required]
        public int CompanyId { get; set; }
        [Required]
        public string LoanType { get; set; }
        [Required]
        [Range(1, 60)]
        public int Installment { get; set; }
        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }
        [Required]
        public DateTime TakenDate { get; set; }
        public string Purpose { get; set; }
        [Required]
        public int UserId { get; set; }
        public string Period { get; set; }
    }

    public class LoanStatusUpdate
    {
        [Required]
        public string LoanId { get; set; }
        [Required]
        public bool Active { get; set; }
        [Required]
        public string UserId { get; set; }
    }

    public class LoanHoldRequest
    {
        [Required]
        public string LoanId { get; set; }
        [Required]
        public string EmployeeId { get; set; }
        [Required]
        public string CompanyId { get; set; }
        [Required]
        [Range(1, 60)]
        public int HoldMonths { get; set; }
        [Required]
        public string UserId { get; set; }
    }

    public class LoanHold
    {
        public int Id { get; set; }
        public int LoanId { get; set; }
        public int EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string PeriodFrom { get; set; }
        public string PeriodTo { get; set; }
        public int HoldCount { get; set; }
        public DateTime HoldDate { get; set; }
        public string LoanType { get; set; }
        public decimal LoanAmount { get; set; }
    }

    public class LoanPayment
    {
        public int Id { get; set; }
        public int LoanId { get; set; }
        public int EmployeeId { get; set; }
        public int CompanyId { get; set; }
        public decimal Amount { get; set; }
        public string Period { get; set; }
        public string EmployeeName { get; set; }
    }

    public class LoanHistory
    {
        public int Id { get; set; }
        public int LoanId { get; set; }
        public DateTime ChangeDate { get; set; }
        public string Action { get; set; }
        public string PreviousStatus { get; set; }
        public string NewStatus { get; set; }
        public string ChangedBy { get; set; }
        public string Remarks { get; set; }
    }

    #endregion
}