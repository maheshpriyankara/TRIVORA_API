using Microsoft.AspNetCore.Mvc;
using TrivoraHrisBackend.Models;
using TrivoraHrisBackend.Repositories;
using TrivoraHrisBackend.Service;
using TrivoraHrisBackend.Services;

namespace TrivoraHrisBackend.Controllers
{
    [ApiController]
    [Route("api/paysheetPrint")]
    public class PaySheetPrintController : ControllerBase
    {
        private readonly PaySheetPrintRepository _repo;
        private readonly EmailService _email;

        public PaySheetPrintController(
            PaySheetPrintRepository repo,
            EmailService email)
        {
            _repo = repo;
            _email = email;
        }

        // ---------- Single JSON ----------
        [HttpGet("employee/{employeeId}/month/{month}/year/{year}")]
        public async Task<IActionResult> GetSingle(int employeeId, string month, int year)
        {
            var data = await _repo.GetSingleAsync(employeeId, month, year);
            if (data == null)
                return NotFound(new { success = false, message = "No paysheet found" });
            return Ok(new { success = true, data });
        }

        // ---------- Batch JSON ----------
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
// ============================================================
// EPF/ETF HISTORY — one row per month for the selected range
// ============================================================
[HttpGet("epf-etf-history/{employeeId}")]
public async Task<IActionResult> GetEpfEtfHistory(
    int employeeId,
    [FromQuery] string monthFrom,
    [FromQuery] int yearFrom,
    [FromQuery] string monthTo,
    [FromQuery] int yearTo)
{
    if (employeeId <= 0 ||
        string.IsNullOrWhiteSpace(monthFrom) ||
        string.IsNullOrWhiteSpace(monthTo) ||
        yearFrom <= 0 || yearTo <= 0)
        return BadRequest(new { success = false, message = "Invalid parameters" });

    var data = await _repo.GetEpfEtfHistoryAsync(employeeId, monthFrom, yearFrom, monthTo, yearTo);
    return Ok(new { success = true, data });
}
        // ---------- Get email ----------
        [HttpGet("get-email/{employeeId}")]
        public async Task<IActionResult> GetEmail(int employeeId)
        {
            var email = await _repo.GetEmployeeEmailAsync(employeeId);
            if (string.IsNullOrWhiteSpace(email))
                return Ok(new { success = false, email = "", message = "No email address found" });
            return Ok(new { success = true, email });
        }

        // ============================================================
        // SEND SINGLE EMAIL
        // ============================================================
        [HttpPost("send-email")]
        public async Task<IActionResult> SendEmail([FromBody] SendPayslipEmailRequest req)
        {
            if (req == null || req.EmployeeId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            var data = await _repo.GetSingleAsync(req.EmployeeId, req.Month, req.Year);
            if (data == null)
                return NotFound(new { success = false, message = "No paysheet found" });

            var email = !string.IsNullOrWhiteSpace(req.Email)
                ? req.Email
                : await _repo.GetEmployeeEmailAsync(req.EmployeeId);

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { success = false, message = "No email address found" });

            // HTML body (same as print layout)
            var emailHtml = PaySheetEmailRenderer.BuildHtml(data, req.Message);

            // PDF attachment (same layout)
            byte[] pdfBytes;
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                pdfBytes = await PaySheetPdfBuilder.BuildAsync(data, baseUrl);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "PDF generation failed: " + ex.Message });
            }

            var fileName = $"Payslip_{data.EmployeeNo}_{data.Period.Replace("/", "-")}.pdf";
            var subject  = $"Payslip – {data.Period} – {data.EmployeeName}";

            try
            {
                await _email.SendAsync(email, subject, emailHtml, pdfBytes, fileName);
                return Ok(new { success = true, message = $"Payslip sent to {email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
            }
        }

        // ============================================================
        // SEND BATCH EMAILS
        // ============================================================
        [HttpPost("send-email-batch")]
        public async Task<IActionResult> SendEmailBatch([FromBody] SendBatchPayslipEmailRequest req)
        {
            if (req == null || req.CompanyId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            var list = await _repo.GetBatchAsync(req.CompanyId, req.DepartmentId, req.Month, req.Year);
            if (list.Count == 0)
                return NotFound(new { success = false, message = "No paysheets found" });

            int sent = 0, failed = 0, skipped = 0;
            var errors = new List<string>();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            foreach (var p in list)
            {
                var email = await _repo.GetEmployeeEmailAsync(p.EmployeeId);
                if (string.IsNullOrWhiteSpace(email))
                {
                    skipped++;
                    errors.Add($"{p.EmployeeName}: no email");
                    continue;
                }

                try
                {
                    var html     = PaySheetEmailRenderer.BuildHtml(p, null);
                    var pdfBytes = await PaySheetPdfBuilder.BuildAsync(p, baseUrl);
                    var fileName = $"Payslip_{p.EmployeeNo}_{p.Period.Replace("/", "-")}.pdf";
                    var subject  = $"Payslip – {p.Period} – {p.EmployeeName}";

                    await _email.SendAsync(email, subject, html, pdfBytes, fileName);
                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{p.EmployeeName}: {ex.Message}");
                }
            }

            return Ok(new { success = true, sent, failed, skipped, total = list.Count, errors });
        }
    }

    public class SendPayslipEmailRequest
    {
        public int     EmployeeId { get; set; }
        public string  Month      { get; set; } = "";
        public int     Year       { get; set; }
        public string? Email      { get; set; }
        public string? Message    { get; set; }
    }

    public class SendBatchPayslipEmailRequest
    {
        public int    CompanyId    { get; set; }
        public int?   DepartmentId { get; set; }
        public string Month        { get; set; } = "";
        public int    Year         { get; set; }
    }
}