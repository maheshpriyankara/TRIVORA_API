// Controllers/PaySheetController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TRIVORA_API.Models;
using TRIVORA_API.Service;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaySheetController : ControllerBase
    {
        private readonly IPaySheetService _paySheetService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PaySheetController(IPaySheetService paySheetService, IHttpContextAccessor httpContextAccessor)
        {
            _paySheetService = paySheetService;
            _httpContextAccessor = httpContextAccessor;
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpGet("employee/{employeeId}")]
        public async Task<IActionResult> GetPaySheetsByEmployee(int employeeId)
        {
            try
            {
                var paySheets = await _paySheetService.GetPaySheetsByEmployeeAsync(employeeId);
                return Ok(new { success = true, data = paySheets });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpGet("month/{month}")]
        public async Task<IActionResult> GetPaySheetsByMonth(string month)
        {
            try
            {
                var paySheets = await _paySheetService.GetPaySheetsByMonthAsync(month);
                return Ok(new { success = true, data = paySheets });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpGet("employee/{employeeId}/month/{month}")]
        public async Task<IActionResult> GetPaySheetByEmployeeAndMonth(int employeeId, string month)
        {
            try
            {
                var paySheet = await _paySheetService.GetPaySheetByEmployeeAndMonthAsync(employeeId, month);
                if (paySheet == null)
                {
                    return NotFound(new { success = false, message = "PaySheet not found" });
                }
                return Ok(new { success = true, data = paySheet });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPaySheetById(int id)
        {
            try
            {
                var paySheet = await _paySheetService.GetPaySheetByIdAsync(id);
                if (paySheet == null)
                {
                    return NotFound(new { success = false, message = "PaySheet not found" });
                }
                return Ok(new { success = true, data = paySheet });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpPost]
        public async Task<IActionResult> CreatePaySheet([FromBody] PaySheetRequestDto request)
        {
            try
            {
                if (request == null || request.EmployeeId <= 0 || string.IsNullOrEmpty(request.Month))
                {
                    return BadRequest(new { success = false, message = "Invalid data" });
                }

                var paySheet = await _paySheetService.CreatePaySheetAsync(request);
                if (paySheet == null)
                {
                    return StatusCode(500, new { success = false, message = "Failed to create paysheet" });
                }
                return Ok(new { success = true, data = paySheet, message = "PaySheet created successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePaySheet(int id, [FromBody] PaySheetRequestDto request)
        {
            try
            {
                if (request == null || request.EmployeeId <= 0 || string.IsNullOrEmpty(request.Month))
                {
                    return BadRequest(new { success = false, message = "Invalid data" });
                }

                var paySheet = await _paySheetService.UpdatePaySheetAsync(id, request);
                if (paySheet == null)
                {
                    return NotFound(new { success = false, message = $"PaySheet with ID {id} not found" });
                }
                return Ok(new { success = true, data = paySheet, message = "PaySheet updated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, message = $"PaySheet with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaySheet(int id)
        {
            try
            {
                var result = await _paySheetService.DeletePaySheetAsync(id);
                if (!result)
                {
                    return NotFound(new { success = false, message = $"PaySheet with ID {id} not found" });
                }
                return Ok(new { success = true, message = "PaySheet deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
        [HttpPost("calculate")]
        public async Task<IActionResult> CalculatePaySheet([FromBody] CalculatePaySheetRequest request)
        {
            try
            {
                if (request == null || request.EmployeeId <= 0 || string.IsNullOrEmpty(request.Month))
                {
                    return BadRequest(new { success = false, message = "Invalid data" });
                }

                var paySheet = await _paySheetService.CalculatePaySheetAsync(request.EmployeeId, request.Month);
                if (paySheet == null)
                {
                    return NotFound(new { success = false, message = "Could not calculate paysheet" });
                }
                return Ok(new { success = true, data = paySheet, message = "PaySheet calculated successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // ✅ FIXED: Use correct method name (singular)
 [HttpGet("employee/{employeeId}/month/{month}/year/{year}")]
    public async Task<IActionResult> GetPaySheetByEmployeeMonthYear(int employeeId, string month, string year)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(month) || string.IsNullOrEmpty(year))
            {
                return BadRequest(new { success = false, message = "Month and year are required" });
            }

            // Format: "2024/January"
            var period = $"{year}/{month}";
            
            var paySheet = await _paySheetService.GetPaySheetByEmployeeAndMonthAsync(employeeId, period);
            
            if (paySheet == null)
            {
                return NotFound(new { success = false, message = $"PaySheet not found for employee {employeeId} in {month} {year}" });
            }
            
            return Ok(new { success = true, data = paySheet });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

        // ✅ FIXED: Use correct method name (singular)
        [HttpPost("process-payroll")]
        public async Task<IActionResult> ProcessPayroll([FromBody] ProcessPayrollRequest request)
        {
            try
            {
                if (request == null || request.CompanyId <= 0 || string.IsNullOrEmpty(request.Month))
                {
                    return BadRequest(new { success = false, message = "Invalid data" });
                }

                var result = await _paySheetService.ProcessPayrollAsync(request.CompanyId, request.Month);
                if (result)
                {
                    return Ok(new { success = true, message = $"Payroll processed successfully for {request.Month}" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Failed to process payroll" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class CalculatePaySheetRequest
    {
        public int EmployeeId { get; set; }
        public string Month { get; set; } = string.Empty;
    }

    public class ProcessPayrollRequest
    {
        public int CompanyId { get; set; }
        public string Month { get; set; } = string.Empty;
    }
}