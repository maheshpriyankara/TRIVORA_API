using Microsoft.AspNetCore.Mvc;
using TrivoraHrisBackend.Models;
using TrivoraHrisBackend.Repositories;

namespace TrivoraHrisBackend.Controllers
{
    [ApiController]
    [Route("api/paysheet")]
    public class PaySheetController : ControllerBase
    {
        private readonly PaySheetRepository _repo;

        public PaySheetController(PaySheetRepository repo)
        {
            _repo = repo;
        }

        // ============================================================
        // GET /api/paysheet/employee/{id}/month/{month}/year/{year}
        // Used by the frontend to LOAD the paysheet onto the screen.
        // ============================================================
        [HttpGet("employee/{employeeId}/month/{month}/year/{year}")]
        public async Task<IActionResult> GetSingle(int employeeId, string month, int year)
        {
            var data = await _repo.GetSingleAsync(employeeId, month, year);
            if (data == null)
                return Ok(new { success = false, reason = "notfound", message = "No paysheet found" });

            return Ok(new { success = true, data });
        }

        // ============================================================
        // GET /api/paysheet/batch?companyId=1&month=January&year=2025&departmentId=2
        // ============================================================
        [HttpGet("batch")]
        public async Task<IActionResult> GetBatch(
            [FromQuery] int companyId,
            [FromQuery] string month,
            [FromQuery] int year,
            [FromQuery] int? departmentId = null)
        {
            if (companyId <= 0 || string.IsNullOrWhiteSpace(month) || year <= 0)
                return BadRequest(new { success = false, message = "Invalid parameters" });

            var data = await _repo.GetBatchAsync(companyId, departmentId, month, year);
            return Ok(new { success = true, data });
        }
    }
}