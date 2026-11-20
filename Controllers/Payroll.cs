using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PayrollController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PayrollController> _logger;

        public PayrollController(ApplicationDbContext context, ILogger<PayrollController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/payroll/test
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new { 
                message = "Payroll controller is working!", 
                timestamp = DateTime.Now,
                status = "SUCCESS"
            });
        }

        // GET: api/payroll/periods/unlocked
        [HttpGet("periods/unlocked")]
        public async Task<ActionResult> GetUnlockedPeriods()
        {
            try
            {
                var sql = @"
                    SELECT 
                        id AS Id,
                        company_id AS CompanyId,
                        year AS Year,
                        month AS Month,
                        payrollStartDate AS PayrollStartDate,
                        processing AS Processing,
                        locked AS Locked
                    FROM Payroll_Process 
                    WHERE locked = 0 
                    ORDER BY payrollStartDate";

                // Use Database.SqlQueryRaw for dynamic results
                var periods = await _context.Database
                    .SqlQueryRaw<PayrollPeriodDto>(sql)
                    .ToListAsync();

                return Ok(new ApiResponse<List<PayrollPeriodDto>>
                {
                    Success = true,
                    Message = $"Found {periods.Count} unlocked periods",
                    Data = periods
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching unlocked payroll periods");
                return StatusCode(500, new ApiResponse<List<PayrollPeriodDto>>
                {
                    Success = false,
                    Message = "Internal server error while fetching payroll periods"
                });
            }
        }

        // GET: api/payroll
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new 
            {
                message = "Payroll API is running",
                endpoints = new[]
                {
                    "GET /api/payroll/test",
                    "GET /api/payroll/periods/unlocked"
                },
                timestamp = DateTime.Now
            });
        }
    }
}