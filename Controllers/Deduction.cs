using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRIVORA_API.Data;
using TRIVORA_API.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeductionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DeductionController> _logger;

        public DeductionController(ApplicationDbContext context, ILogger<DeductionController> logger)
        {
            _context = context;
            _logger = logger;
        }

    [HttpGet("employee/{employeeId}/{companyId}")]
    public async Task<ActionResult> GetEmployeeDeductions(int employeeId, int companyId)
    {
        try
        {
            // First, get all deductions
            var deductions = await _context.Deductions
                .Where(d => d.employee_id == employeeId && d.company_id == companyId && d.declined == false)
                .OrderByDescending(d => d.taken_date)
                .ThenByDescending(d => d.created_date)
                .ToListAsync();

            // Get all user IDs from system requests
            var userIds = deductions
                .Where(d => d.request_type == "System")
                .SelectMany(d => new int?[] { d.requested_by, d.approved_by })
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();

            // Get admin users using raw SQL
            var adminUsers = new Dictionary<int, string>();
            if (userIds.Any())
            {
                var userIdString = string.Join(",", userIds);
                var sql = $"SELECT Id, SystemName FROM Admin_Users WHERE Id IN ({userIdString})";
                
                var users = await _context.Admin_Users
                    .FromSqlRaw(sql)
                    .Select(u => new { u.Id, u.SystemName })
                    .ToListAsync();
                
                adminUsers = users.ToDictionary(u => u.Id, u => u.SystemName ?? u.Id.ToString());
            }

            // Transform the results
            var result = deductions.Select(d => new
            {
                Id = d.id,
                EmployeeId = d.employee_id,
                CompanyId = d.company_id,
                DeductionType = d.deduction_type,
                Amount = d.amount,
                TakenDate = d.taken_date,
                RequestedDate = d.requested_date,
                RequestType = d.request_type,
                RequestedBy = GetDisplayName(d.request_type, d.requested_by, adminUsers),
                ApprovedBy = GetDisplayName(d.request_type, d.approved_by, adminUsers),
                ApprovedDate = d.approved_date,
                Approved = d.approved,
                Declined = d.declined,
                Remark = d.remark,
                CreatedDate = d.created_date,
                
            }).ToList();

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deductions for employee {EmployeeId}", employeeId);
            return StatusCode(500, new { success = false, message = "Error retrieving deductions" });
        }
    }

    private string GetDisplayName(string requestType, int? userId, Dictionary<int, string> adminUsers)
    {
        if (userId == null) return null;

        if (requestType == "System" && adminUsers.ContainsKey(userId.Value))
        {
            return adminUsers[userId.Value];
        }

        return userId.ToString();
    }

    // POST: api/deduction
    [HttpPost]
    public async Task<ActionResult> AddDeduction([FromBody] AddDeductionRequest request)
    {
        try
        {
            // Get user ID from token (you can modify this based on your auth system)
            var userId = 1; // Default system user

            var deduction = new Deduction
            {
                employee_id = request.EmployeeId,
                company_id = request.CompanyId,
                deduction_type = request.DeductionType,
                amount = request.Amount,
                taken_date = request.TakenDate,
                requested_by = request.UserId,
                approved_by = request.UserId,
                remark = request.Remark,
                requested_date = DateTime.UtcNow,
                created_date = DateTime.UtcNow,
                approved_date = DateTime.UtcNow,
                approved = true,
                declined = false,
                request_type = request.RequestType,
                period = request.Period,

            };

            _context.Deductions.Add(deduction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Deduction added successfully", data = new { id = deduction.id } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding deduction");
            return StatusCode(500, new { success = false, message = "Error adding deduction" });
        }
    }
    
    
    [HttpGet("employee/{employeeId}/{companyId}/{periodYear}/{periodMonth}")]
    public async Task<ActionResult> GetEmployeeDeductionsByPeriod(int employeeId, int companyId, string periodYear, string periodMonth)
    {
        var period = periodYear + '/' + periodMonth;
        try
        {
            

            // Get deductions for specific period only
            var deductions = await _context.Deductions
                .Where(d => d.employee_id == employeeId 
                        && d.company_id == companyId 
                        && d.declined == false
                        && d.period == period) // Filter by period in database
                .OrderByDescending(d => d.taken_date)
                .ThenByDescending(d => d.created_date)
                .ToListAsync();

            // Get all user IDs from system requests
            var userIds = deductions
                .Where(d => d.request_type == "System")
                .SelectMany(d => new int?[] { d.requested_by, d.approved_by })
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();

            // Get admin users using raw SQL
            var adminUsers = new Dictionary<int, string>();
            if (userIds.Any())
            {
                var userIdString = string.Join(",", userIds);
                var sql = $"SELECT Id, SystemName FROM Admin_Users WHERE Id IN ({userIdString})";
                
                var users = await _context.Admin_Users
                    .FromSqlRaw(sql)
                    .Select(u => new { u.Id, u.SystemName })
                    .ToListAsync();
                
                adminUsers = users.ToDictionary(u => u.Id, u => u.SystemName ?? u.Id.ToString());
            }

            // Transform the results
            var result = deductions.Select(d => new
            {
                Id = d.id,
                EmployeeId = d.employee_id,
                CompanyId = d.company_id,
                DeductionType = d.deduction_type,
                Amount = d.amount,
                TakenDate = d.taken_date,
                RequestedDate = d.requested_date,
                RequestType = d.request_type,
                RequestedBy = GetDisplayName(d.request_type, d.requested_by, adminUsers),
                ApprovedBy = GetDisplayName(d.request_type, d.approved_by, adminUsers),
                ApprovedDate = d.approved_date,
                Approved = d.approved,
                Declined = d.declined,
                Remark = d.remark,
                CreatedDate = d.created_date,
                Period = d.period
            }).ToList();

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deductions for employee {EmployeeId} period {Period}", employeeId, period);
            return StatusCode(500, new { success = false, message = "Error retrieving deductions" });
        }
    }


    // DELETE: api/deduction/5
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteDeduction(int id)
    {
        try
        {
            var deduction = await _context.Deductions.FindAsync(id);
            
            if (deduction == null)
            {
                return NotFound(new { success = false, message = "Deduction not found" });
            }

            _context.Deductions.Remove(deduction);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Deduction deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting deduction {DeductionId}", id);
            return StatusCode(500, new { success = false, message = "Error deleting deduction" });
        }
    }

    // GET: api/deduction/types
    [HttpGet("types")]
    public ActionResult GetDeductionTypes()
    {
        var types = new List<string>
        {
            "Cash Short",
            "Salary Advance", 
            "Other Deductions"
        };
        
        return Ok(new { success = true, data = types });
    }
}

    // Request model
    public class AddDeductionRequest
    {
        public int EmployeeId { get; set; }
        public int UserId { get; set; }
        public int ApprovedBy { get; set; }
        public int CompanyId { get; set; }
        public string DeductionType { get; set; }
        public decimal Amount { get; set; }
        public DateTime TakenDate { get; set; }
        public DateTime ApprovedDate { get; set; }
        public string Remark { get; set; }
        public string RequestType { get; set; }
        public string Period { get; set; }
    }

    // Simple Deduction model that matches your database columns exactly
    [Table("Deductions")]
    public class Deduction
    {
        [Column("id")]
        public int id { get; set; }

        [Column("employee_id")]
        public int employee_id { get; set; }

        [Column("company_id")]
        public int company_id { get; set; }

        [Column("period")]
        public string period { get; set; }

        [Column("deduction_type")]
        public string deduction_type { get; set; }

        [Column("amount")]
        public decimal amount { get; set; }

        [Column("taken_date")]
        public DateTime taken_date { get; set; }

        [Column("requested_date")]
        public DateTime requested_date { get; set; }

        [Column("request_type")]
        public string request_type { get; set; }

        [Column("requested_by")]
        public int requested_by { get; set; }

        [Column("approved_by")]
        public int? approved_by { get; set; }

        [Column("approved_date")]
        public DateTime? approved_date { get; set; }

        [Column("approved")]
        public bool approved { get; set; }

        [Column("declined")]
        public bool declined { get; set; }

        [Column("remark")]
        public string remark { get; set; }

        [Column("created_date")]
        public DateTime created_date { get; set; }

        [Column("modified_date")]
        public DateTime? modified_date { get; set; }
    }
}
public class AdminUser
{
    public int Id { get; set; }
    public string Nic { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string AccessType { get; set; }
    public string MobileNumber { get; set; }
    public string WhastsupNumber { get; set; }
    public string EmailId { get; set; }
    public bool IsActive { get; set; }
    public int? inactivityTimeout { get; set; }
    public string SystemName { get; set; }
}