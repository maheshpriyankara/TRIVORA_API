using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProcessQueueController : ControllerBase
    {
        private readonly string _connectionString;

        public ProcessQueueController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

       
        // GET /api/attendance/status?employeeId=X&period=Y
        [HttpGet("/api/attendance/status")]
        public async Task<IActionResult> GetStatus([FromQuery] int employeeId, [FromQuery] string period)
        {
            using var con = new SqlConnection(_connectionString);

            var row = await con.QueryFirstOrDefaultAsync<ProcessQueue>(
                @"SELECT TOP 1 * FROM process_queue 
                  WHERE emp_systemID = @EmployeeId AND period_ = @Period
                  ORDER BY request_date DESC",
                new { EmployeeId = employeeId, Period = period });

            if (row == null)
                return Ok(new { success = true, isProcessing = false });

            return Ok(new
            {
                success = true,
                isProcessing = row.processing && !row.process_end,
                processStatus = row.process_status,
                processEnd = row.process_end
            });
        }
    }

    public class TriggerProcessRequest
    {
        public int EmpSystemId { get; set; }
        public string EmpEmpId { get; set; } = string.Empty;
        public string PeriodKey { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? UserId { get; set; }
    }
}