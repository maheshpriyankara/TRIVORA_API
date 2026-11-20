using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRIVORA_API.Data;
using TRIVORA_API.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AllowanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AllowanceController> _logger;

        public AllowanceController(ApplicationDbContext context, ILogger<AllowanceController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("employee/{employeeId}/{companyId}")]
        public async Task<ActionResult> GetEmployeeAllowances(int employeeId, int companyId)
        {
            try
            {
                // First, get all allowances
                var allowances = await _context.Allowances
                    .Where(a => a.employee_id == employeeId && a.company_id == companyId && a.declined == false)
                    .OrderByDescending(a => a.taken_date)
                    .ThenByDescending(a => a.created_date)
                    .ToListAsync();

                // Get all user IDs from system requests
                var userIds = allowances
                    .Where(a => a.request_type == "System")
                    .SelectMany(a => new int?[] { a.requested_by, a.approved_by })
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
                var result = allowances.Select(a => new
                {
                    Id = a.id,
                    EmployeeId = a.employee_id,
                    CompanyId = a.company_id,
                    AllowanceType = a.allowances_type,
                    Amount = a.amount,
                    TakenDate = a.taken_date,
                    RequestedDate = a.requested_date,
                    RequestType = a.request_type,
                    RequestedBy = GetDisplayName(a.request_type, a.requested_by, adminUsers),
                    ApprovedBy = GetDisplayName(a.request_type, a.approved_by, adminUsers),
                    ApprovedDate = a.approved_date,
                    Approved = a.approved,
                    Declined = a.declined,
                    Remark = a.remark,
                    CreatedDate = a.created_date
                }).ToList();

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting allowances for employee {EmployeeId}", employeeId);
                return StatusCode(500, new { success = false, message = "Error retrieving allowances" });
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

        // POST: api/allowance
        [HttpPost]
        public async Task<ActionResult> AddAllowance([FromBody] AddAllowanceRequest request)
        {
            try
            {
                var allowance = new Allowance
                {
                    employee_id = request.EmployeeId,
                    company_id = request.CompanyId,
                    allowances_type = request.AllowanceType,
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
                    period=request.Period,
                };

                _context.Allowances.Add(allowance);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Allowance added successfully", data = new { id = allowance.id } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding allowance");
                return StatusCode(500, new { success = false, message = "Error adding allowance" });
            }
        }
         [HttpGet("employee/{employeeId}/{companyId}/{periodYear}/{periodMonth}")]
        public async Task<ActionResult> GetEmployeeAllowancesByPeriod(int employeeId, int companyId, string periodYear,string periodMonth )
        { 
            var period = periodYear + '/' + periodMonth;
            try
            {
               
                // Get allowances for specific period only
                var allowances = await _context.Allowances
                    .Where(a => a.employee_id == employeeId
                            && a.company_id == companyId
                            && a.declined == false
                            && a.period == period) // Filter by period in database
                    .OrderByDescending(a => a.taken_date)
                    .ThenByDescending(a => a.created_date)
                    .ToListAsync();

                // Get all user IDs from system requests
                var userIds = allowances
                    .Where(a => a.request_type == "System")
                    .SelectMany(a => new int?[] { a.requested_by, a.approved_by })
                    .Where(id => id.HasValue)
                    .Select(id => id.Value)
                    .Distinct()
                    .ToList();

                // Get admin users
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
                var result = allowances.Select(a => new
                {
                    Id = a.id,
                    EmployeeId = a.employee_id,
                    CompanyId = a.company_id,
                    AllowanceType = a.allowances_type,
                    Amount = a.amount,
                    TakenDate = a.taken_date,
                    RequestedDate = a.requested_date,
                    RequestType = a.request_type,
                    RequestedBy = GetDisplayName(a.request_type, a.requested_by, adminUsers),
                    ApprovedBy = GetDisplayName(a.request_type, a.approved_by, adminUsers),
                    ApprovedDate = a.approved_date,
                    Approved = a.approved,
                    Declined = a.declined,
                    Remark = a.remark,
                    CreatedDate = a.created_date,
                    Period = a.period
                }).ToList();

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting allowances for employee {EmployeeId} period {Period}", employeeId, period);
                return StatusCode(500, new { success = false, message = "Error retrieving allowances" });
            }
        }
       
        // DELETE: api/allowance/5
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAllowance(int id)
        {
            try
            {
                var allowance = await _context.Allowances.FindAsync(id);
                
                if (allowance == null)
                {
                    return NotFound(new { success = false, message = "Allowance not found" });
                }

                _context.Allowances.Remove(allowance);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Allowance deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting allowance {AllowanceId}", id);
                return StatusCode(500, new { success = false, message = "Error deleting allowance" });
            }
        }

        // GET: api/allowance/types
        [HttpGet("types")]
        public ActionResult GetAllowanceTypes()
        {
            var types = new List<string>
            {
                "Transport Allowance",
                "Food Allowance", 
                "Medical Allowance",
                "Housing Allowance",
                "Other Allowances"
            };
            
            return Ok(new { success = true, data = types });
        }
    }

    // Request model for Allowance
    public class AddAllowanceRequest
    {
        public int EmployeeId { get; set; }
        public int UserId { get; set; }
        public int ApprovedBy { get; set; }
        public int CompanyId { get; set; }
        public string AllowanceType { get; set; }
        public decimal Amount { get; set; }
        public DateTime TakenDate { get; set; }
        public DateTime ApprovedDate { get; set; }
        public string Remark { get; set; }
        public string RequestType { get; set; }
        public string Period { get; set; }

    }

    // Allowance model that matches your database
    [Table("Allowances")]
    public class Allowance
    {
        [Column("id")]
        public int id { get; set; }

        [Column("employee_id")]
        public int employee_id { get; set; }

        [Column("company_id")]
        public int company_id { get; set; }
        [Column("period")]
        public string period { get; set; }
        [Column("allowances_type")]
        public string allowances_type { get; set; }

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