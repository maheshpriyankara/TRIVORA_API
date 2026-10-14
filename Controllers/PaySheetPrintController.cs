using Microsoft.AspNetCore.Mvc;
using TRIVORA_API.Service;
using TrivoraHrisBackend.Models;
using TrivoraHrisBackend.Repositories;
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

        
        // ============================================================
        // SINGLE — full data (used for print / email)
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
        // BATCH — all paysheets for a period
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

            if (data.Count == 0)
                return Ok(new
                {
                    success = false,
                    message = "No EPF/ETF history found for the selected range",
                    data = new List<PaySheetPrintDto>()
                });

            return Ok(new { success = true, data });
        }

        // ============================================================
        // GET EMAIL — reads Employees.Mobile
        // ============================================================
        [HttpGet("get-email/{employeeId}")]
        public async Task<IActionResult> GetEmail(int employeeId)
        {
            var email = await _repo.GetEmployeeEmailAsync(employeeId);
            if (string.IsNullOrWhiteSpace(email))
                return Ok(new { success = false, email = "", message = "No email address found" });
            return Ok(new { success = true, email });
        }

        // ============================================================
        // LOCKED PERIODS — for frontend gating
        // ============================================================
        [HttpGet("locked-periods")]
        public async Task<IActionResult> GetLockedPeriods([FromQuery] int companyId)
        {
            if (companyId <= 0)
                return BadRequest(new { success = false, message = "Invalid company" });

            var periods = await _repo.GetLockedPeriodsAsync(companyId);
            return Ok(new { success = true, data = periods });
        }

        // ============================================================
        // SEND SINGLE EMAIL — HTML from frontend, PDF attached
        // ============================================================
        [HttpPost("send-email")]
        public async Task<IActionResult> SendEmail([FromBody] SendPayslipEmailRequest req)
        {
            if (req == null || req.EmployeeId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            var data = await _repo.GetSingleAsync(req.EmployeeId, req.Month, req.Year);
            if (data == null)
                return Ok(new { success = false, message = "No paysheet found" });

            var email = !string.IsNullOrWhiteSpace(req.Email)
                ? req.Email
                : await _repo.GetEmployeeEmailAsync(req.EmployeeId);

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { success = false, message = "No email address found" });

            var emailHtml = PaySheetEmailRenderer.BuildHtml(data, req.Message);

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
            var subject = $"Payslip – {data.Period} – {data.EmployeeName}";

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
        // SEND BATCH EMAILS — one email per employee, PDF with all months
        // ============================================================
        [HttpPost("send-email-batch")]
        public async Task<IActionResult> SendEmailBatch([FromBody] SendBatchPayslipEmailRequest req)
        {
            if (req == null || req.CompanyId <= 0 || req.Items == null || req.Items.Count == 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            int sent = 0, failed = 0, skipped = 0;
            var errors = new List<string>();

            foreach (var item in req.Items)
            {
                var email = !string.IsNullOrWhiteSpace(item.Email)
                    ? item.Email
                    : await _repo.GetEmployeeEmailAsync(item.EmployeeId);

                if (string.IsNullOrWhiteSpace(email))
                {
                    skipped++;
                    errors.Add($"{item.EmployeeName}: no email");
                    continue;
                }

                var intro = $"Dear {item.EmployeeName},<br><br>" +
                            $"Please find your payslip for <b>{item.Period}</b> attached.<br><br>" +
                            $"Regards,<br><b>HR Department</b>";

                var emailHtml = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#fff;margin:0;padding:24px;color:#1f2937;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;'>
        <div style='font-size:15px;line-height:1.6;color:#1e3a5f;'>{intro}</div>
    </div>
</body></html>";

                try
                {
                    byte[] pdfBytes;
                    try
                    {
                        var baseUrl = $"{Request.Scheme}://{Request.Host}";
                        pdfBytes = await PaySheetPdfBuilder.BuildFromHtmlAsync(item.HtmlBody ?? "");
                    }
                    catch
                    {
                        pdfBytes = Array.Empty<byte>();
                    }

                    var fileName = $"Payslip_{item.EmployeeNo}_{item.Period.Replace("/", "-")}.pdf";
                    var subject = $"Payslip – {item.Period} – {item.EmployeeName}";

                    await _email.SendAsync(email, subject, emailHtml, pdfBytes.Length > 0 ? pdfBytes : null, fileName);
                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{item.EmployeeName}: {ex.Message}");
                }
            }

            return Ok(new { success = true, sent, failed, skipped, total = req.Items.Count, errors });
        }

        // ============================================================
        // SEND EPF/ETF SUMMARY EMAIL
        // ============================================================
        [HttpPost("send-epf-etf-email")]
        public async Task<IActionResult> SendEpfEtfEmail([FromBody] SendEpfEtfEmailRequest req)
        {
            if (req == null || req.EmployeeId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            var email = !string.IsNullOrWhiteSpace(req.Email)
                ? req.Email
                : await _repo.GetEmployeeEmailAsync(req.EmployeeId);

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { success = false, message = "No email address found" });

            if (string.IsNullOrWhiteSpace(req.HtmlBody))
                return BadRequest(new { success = false, message = "Missing report HTML" });

            var intro = string.IsNullOrWhiteSpace(req.Message)
                ? $"Dear {req.EmployeeName},<br><br>" +
                  $"Please find your <b>EPF / ETF History Summary</b> for the period " +
                  $"<b>{req.PeriodLabel}</b> attached.<br><br>" +
                  $"Regards,<br><b>HR Department</b>"
                : req.Message.Replace("\n", "<br>");

            var emailHtml = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#fff;margin:0;padding:24px;color:#1f2937;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;'>
        <div style='font-size:15px;line-height:1.6;color:#1e3a5f;'>{intro}</div>
    </div>
</body></html>";

            byte[] pdfBytes;
            try
            {
                pdfBytes = await PaySheetPdfBuilder.BuildFromHtmlAsync(req.HtmlBody);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "PDF failed: " + ex.Message });
            }

            var safePeriod = (req.PeriodLabel ?? "Report").Replace("/", "-").Replace(" ", "_");
            var fileName   = $"EPF-ETF_{req.EmployeeNo}_{safePeriod}.pdf";
            var subject    = $"EPF/ETF Summary – {req.PeriodLabel} – {req.EmployeeName}";

            try
            {
                await _email.SendAsync(email, subject, emailHtml, pdfBytes, fileName);
                return Ok(new { success = true, message = $"EPF/ETF summary sent to {email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
            }
        }
    }

    // ============================================================
    // REQUEST MODELS
    // ============================================================
    public class SendPayslipEmailRequest
    {
        public int     EmployeeId { get; set; }
        public string  Month      { get; set; } = "";
        public int     Year       { get; set; }
        public string? Email      { get; set; }
        public string? Message    { get; set; }
        public string? HtmlBody   { get; set; }
    }

    public class SendBatchPayslipEmailRequest
    {
        public int    CompanyId    { get; set; }
        public int?   DepartmentId { get; set; }
        public string Month        { get; set; } = "";
        public int    Year         { get; set; }
        public List<BatchEmailItem> Items { get; set; } = new();
    }

    public class BatchEmailItem
    {
        public int     EmployeeId   { get; set; }
        public string  EmployeeNo   { get; set; } = "";
        public string  EmployeeName { get; set; } = "";
        public string  Period       { get; set; } = "";
        public string? Email        { get; set; }
        public string? HtmlBody     { get; set; }
    }

    public class SendEpfEtfEmailRequest
    {
        public int     EmployeeId   { get; set; }
        public string  EmployeeNo   { get; set; } = "";
        public string  EmployeeName { get; set; } = "";
        public string  PeriodLabel  { get; set; } = "";
        public string? Email        { get; set; }
        public string? Message      { get; set; }
        public string? HtmlBody     { get; set; }
    }
}