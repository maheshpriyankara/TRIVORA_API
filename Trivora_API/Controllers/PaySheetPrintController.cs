using Microsoft.AspNetCore.Mvc;
using TRIVORA_API.Service;
using TrivoraHrisBackend.Models;
using TrivoraHrisBackend.Repositories;
using TrivoraHrisBackend.Services;

namespace TrivoraHrisBackend.Controllers
{
    public interface IEmailService
    {
        Task SendAsync(EmailMessage message);
    }

    public class EmailMessage
    {
        public List<string> To { get; set; } = new();
        public List<string> Cc { get; set; } = new();
        public List<string> Bcc { get; set; } = new();
        public string Subject { get; set; } = "";
        public string HtmlBody { get; set; } = "";
        public List<EmailAttachment> Attachments { get; set; } = new();
    }

    public interface IPdfService
    {
        Task<byte[]> GeneratePdfFromHtmlAsync(string html);
    }

    public class EmailAttachment
    {
        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "application/octet-stream";
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    [ApiController]
    [Route("api/paysheetPrint")]
    public class PaySheetPrintController : ControllerBase
    {
        private readonly PaySheetPrintRepository _repo;
        private readonly EmailService _email;
        private readonly ILogger<PaySheetPrintController> _logger;

        public PaySheetPrintController(
            PaySheetPrintRepository repo,
            EmailService email,
            ILogger<PaySheetPrintController> logger)
        {
            _repo = repo;
            _email = email;
            _logger = logger;
        }
        // ═══════════════════════════════════════════════════════════
        // EPF/ETF HISTORY — BATCH (all employees)
        // ═══════════════════════════════════════════════════════════
        [HttpGet("epf-etf-history/batch")]
        public async Task<IActionResult> GetEpfEtfHistoryBatch(
            [FromQuery] int companyId,
            [FromQuery] string monthFrom, [FromQuery] int yearFrom,
            [FromQuery] string monthTo, [FromQuery] int yearTo,
            [FromQuery] int? departmentId = null)
        {
            if (companyId <= 0 || string.IsNullOrWhiteSpace(monthFrom) ||
                string.IsNullOrWhiteSpace(monthTo) || yearFrom <= 0 || yearTo <= 0)
                return BadRequest(new { success = false, message = "Invalid parameters" });

            var data = await _repo.GetEpfEtfHistoryBatchAsync(
                companyId, departmentId, monthFrom, yearFrom, monthTo, yearTo);

            if (data.Count == 0)
                return Ok(new
                {
                    success = false,
                    message = "No EPF/ETF history found for the selected range",
                    data = new List<PaySheetPrintDto>()
                });

            return Ok(new { success = true, data });
        }

        // ═══════════════════════════════════════════════════════════
        // EPF/ETF EMAIL — BATCH (combined PDF of all employees)
        // ═══════════════════════════════════════════════════════════
        [HttpPost("send-epf-etf-email-batch")]
        public async Task<IActionResult> SendEpfEtfEmailBatch(
            [FromBody] SendBatchEpfEtfEmailRequest req)
        {
            if (req == null || req.CompanyId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            var toList = SplitEmails(req.ToEmail);
            if (toList.Count == 0)
                return BadRequest(new { success = false, message = "Please provide at least one 'To' address." });

            if (string.IsNullOrWhiteSpace(req.HtmlBody))
                return BadRequest(new { success = false, message = "Missing report HTML" });

            var ccList = SplitEmails(req.CcEmail);
            var bccList = SplitEmails(req.BccEmail);

            byte[] pdfBytes;
            try
            {
                pdfBytes = await PaySlipPdfBuilder.BuildAsync(req.HtmlBody, GetPrintedBy());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "PDF failed: " + ex.Message });
            }

            var periodLabel = $"{req.MonthFrom}_{req.YearFrom}_to_{req.MonthTo}_{req.YearTo}"
                .Replace(" ", "_");
            var fileName = $"EPF-ETF_All_{periodLabel}.pdf";
            var subject = $"EPF/ETF Summary — {req.MonthFrom} {req.YearFrom} to {req.MonthTo} {req.YearTo}";

            var intro = string.IsNullOrWhiteSpace(req.Message)
                ? "<p>Dear Team,</p><p>Please find the combined EPF/ETF contribution summary attached.</p><p style='margin-top:20px;'>Regards,<br><b>HR Department</b></p>"
                : $"<p>{System.Net.WebUtility.HtmlEncode(req.Message).Replace("\n", "<br>")}</p>";

            var emailHtml = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#fff;margin:0;padding:24px;color:#1f2937;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;'>
        <div style='font-size:15px;line-height:1.6;color:#1e3a5f;'>{intro}</div>
    </div>
</body></html>";

            try
            {
                var attachments = new List<(string FileName, byte[] Data)>();
                if (pdfBytes != null && pdfBytes.Length > 0)
                    attachments.Add((fileName, pdfBytes));

                await _email.SendAsync(
                    toEmail: string.Join(",", toList),
                    subject: subject,
                    htmlBody: emailHtml,
                    attachments: attachments,
                    cc: ccList,
                    bcc: bccList);

                return Ok(new
                {
                    success = true,
                    message = $"EPF/ETF summary sent to {toList.Count} recipient(s).",
                    to = toList,
                    cc = ccList,
                    bcc = bccList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
            }
        }

        // Request model — add near the other request models
        public class SendBatchEpfEtfEmailRequest
        {
            public int CompanyId { get; set; }
            public int? DepartmentId { get; set; }
            public string MonthFrom { get; set; } = "";
            public int YearFrom { get; set; }
            public string MonthTo { get; set; } = "";
            public int YearTo { get; set; }
            public string? ToEmail { get; set; }
            public string? CcEmail { get; set; }
            public string? BccEmail { get; set; }
            public string? Message { get; set; }
            public string? HtmlBody { get; set; }
        }
        // ═══════════════════════════════════════════════════════════
        // Central helper — display name for footer
        // ═══════════════════════════════════════════════════════════
        private string GetPrintedBy()
        {
            // Resolve IUserContextService from DI (namespace: TRIVORA_API.Services)
            try
            {
                var ctx = HttpContext.RequestServices
                    .GetService(typeof(TRIVORA_API.Services.IUserContextService))
                    as TRIVORA_API.Services.IUserContextService;

                if (ctx != null)
                {
                    var name = ctx.GetCurrentUserName();
                    if (!string.IsNullOrWhiteSpace(name) && name != "System")
                        return name;
                }
            }
            catch { /* fall through */ }

            // Fallback: read claims directly
            var claimName =
                User?.FindFirst("SystemName")?.Value ??
                User?.FindFirst("FullName")?.Value ??
                User?.FindFirst("name")?.Value ??
                User?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value;

            if (!string.IsNullOrWhiteSpace(claimName))
                return claimName;

            return User?.Identity?.Name ?? "User";
        }
        // ═══════════════════════════════════════════════════════════
        // GET endpoints (unchanged)
        // ═══════════════════════════════════════════════════════════
        [HttpGet("employee/{employeeId}/month/{month}/year/{year}")]
        public async Task<IActionResult> GetSingle(int employeeId, string month, int year)
        {
            var data = await _repo.GetSingleAsync(employeeId, month, year);
            if (data == null)
                return Ok(new { success = false, reason = "notfound", message = "No paysheet found" });
            return Ok(new { success = true, data });
        }

        [HttpGet("batch")]
        public async Task<IActionResult> GetBatch(
            [FromQuery] int companyId, [FromQuery] string month,
            [FromQuery] int year, [FromQuery] int? departmentId = null)
        {
            if (companyId <= 0 || string.IsNullOrWhiteSpace(month) || year <= 0)
                return BadRequest(new { success = false, message = "Invalid parameters" });
            var data = await _repo.GetBatchAsync(companyId, departmentId, month, year);
            return Ok(new { success = true, data });
        }

        [HttpGet("epf-etf-history/{employeeId}")]
        public async Task<IActionResult> GetEpfEtfHistory(
            int employeeId, [FromQuery] string monthFrom, [FromQuery] int yearFrom,
            [FromQuery] string monthTo, [FromQuery] int yearTo)
        {
            if (employeeId <= 0 || string.IsNullOrWhiteSpace(monthFrom) ||
                string.IsNullOrWhiteSpace(monthTo) || yearFrom <= 0 || yearTo <= 0)
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

        // ═══════════════════════════════════════════════════════════
        // SEND PAYROLL EMAIL — Single button (paysheet and/or timesheet)
        // ═══════════════════════════════════════════════════════════
        [HttpPost("send-payroll-email")]
        public async Task<IActionResult> SendPayrollEmail([FromBody] SendPayrollEmailRequest req)
        {
            if (req == null || req.EmployeeId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            if (!req.SendPaysheet && !req.SendTimesheet)
                return BadRequest(new { success = false, message = "Select at least one document (Paysheet or Timesheet)." });

            var toList = SplitEmails(req.Email);
            if (toList.Count == 0)
                return BadRequest(new { success = false, message = "Please provide at least one 'To' address." });

            var ccList = SplitEmails(req.CcEmail);
            var bccList = SplitEmails(req.BccEmail);

            var periodLabel = string.IsNullOrWhiteSpace(req.Period)
                ? $"{req.Month} {req.Year}"
                : req.Period;

            var printedBy = GetPrintedBy();
            var attachments = new List<EmailAttachment>();

            // ── Paysheet (portrait) ──
            if (req.SendPaysheet)
            {
                byte[] pdfBytes;
                try
                {
                    var html = !string.IsNullOrWhiteSpace(req.PaysheetHtmlBody)
                        ? req.PaysheetHtmlBody!
                        : BuildPaysheetHtmlFromRepo(await _repo.GetSingleAsync(req.EmployeeId, req.Month, req.Year));

                    if (string.IsNullOrWhiteSpace(html))
                        return Ok(new { success = false, message = "Could not build paysheet HTML." });

                    pdfBytes = await PaySlipTimesheetPdfBuilder.BuildPayslipAsync(html, printedBy);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = "Paysheet PDF failed: " + ex.Message });
                }

                var safePeriod = Sanitize(req.Period);
                attachments.Add(new EmailAttachment
                {
                    FileName = $"Payslip_{Sanitize(req.EmployeeNo)}_{safePeriod}.pdf",
                    ContentType = "application/pdf",
                    Data = pdfBytes
                });
            }

            // ── Timesheet (landscape) ──
            if (req.SendTimesheet)
            {
                if (string.IsNullOrWhiteSpace(req.TimesheetHtmlBody))
                    return Ok(new { success = false, message = "Timesheet HTML is empty." });

                byte[] pdfBytes;
                try
                {
                    pdfBytes = await PaySlipTimesheetPdfBuilder.BuildTimesheetAsync(
                        req.TimesheetHtmlBody!, printedBy);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = "Timesheet PDF failed: " + ex.Message });
                }

                var safePeriod = Sanitize(req.Period);
                attachments.Add(new EmailAttachment
                {
                    FileName = $"Timesheet_{Sanitize(req.EmployeeNo)}_{safePeriod}.pdf",
                    ContentType = "application/pdf",
                    Data = pdfBytes
                });
            }

            // ── Email wrapper ──
            var bodyHtml = string.IsNullOrWhiteSpace(req.Message)
                ? $@"<p>Dear {System.Net.WebUtility.HtmlEncode(req.EmployeeName)},</p>
                     <p>Please find your payroll document(s) for <b>{periodLabel}</b> attached.</p>
                     <p style='margin-top:20px;'>Regards,<br><b>HR Department</b></p>"
                : $@"<p>Dear {System.Net.WebUtility.HtmlEncode(req.EmployeeName)},</p>
                     <p>{System.Net.WebUtility.HtmlEncode(req.Message).Replace("\n", "<br>")}</p>
                     <p style='margin-top:20px;'>Regards,<br><b>HR Department</b></p>";

            var emailHtml = WrapEmailHtml(bodyHtml);

            var subjectBits = new List<string>();
            if (req.SendPaysheet) subjectBits.Add("Payslip");
            if (req.SendTimesheet) subjectBits.Add("Timesheet");
            var subject = $"{string.Join(" & ", subjectBits)} – {periodLabel} – {req.EmployeeName}";

            try
            {
                var attachmentTuples = attachments.Select(a => (a.FileName, a.Data)).ToList();

                await _email.SendAsync(
                    toEmail: string.Join(",", toList),
                    subject: subject,
                    htmlBody: emailHtml,
                    attachments: attachmentTuples,
                    cc: ccList,
                    bcc: bccList);

                return Ok(new
                {
                    success = true,
                    message = $"Sent to {toList.Count} recipient(s) with {attachments.Count} attachment(s).",
                    to = toList,
                    cc = ccList,
                    bcc = bccList,
                    attached = attachments.Select(a => a.FileName).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "send-payroll-email failed for employee {EmployeeId}", req.EmployeeId);
                return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        // SEND EMAIL BATCH — Reports tab "Email Payslip(s)" (portrait only)
        // ═══════════════════════════════════════════════════════════
        [HttpPost("send-email-batch")]
        public async Task<IActionResult> SendEmailBatch([FromBody] SendBatchPayslipEmailRequest req)
        {
            if (req == null || req.CompanyId <= 0)
                return BadRequest(new { success = false, message = "Invalid request" });

            var printedBy = GetPrintedBy();

            // ── Mode 1: Combined PDF ──
            if (!string.IsNullOrWhiteSpace(req.HtmlBody))
            {
                var toList = SplitEmails(req.ToEmail);
                if (toList.Count == 0)
                    return BadRequest(new { success = false, message = "Please provide at least one 'To' address." });

                var ccList = SplitEmails(req.CcEmail);
                var bccList = SplitEmails(req.BccEmail);

                byte[] pdfBytes;
                try
                {
                    pdfBytes = await PaySlipPdfBuilder.BuildAsync(req.HtmlBody, printedBy);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = "PDF generation failed: " + ex.Message });
                }

                var safePeriod = $"{req.Month}_{req.Year}".Replace(" ", "_");
                var fileName = $"Paysheets_{safePeriod}.pdf";
                var subject = $"Paysheets — {req.Month} {req.Year}";

                var intro = string.IsNullOrWhiteSpace(req.Message)
                    ? $"<p>Dear Team,</p><p>Please find the payslips for <b>{req.Month} {req.Year}</b> attached as a single PDF.</p><p style='margin-top:20px;'>Regards,<br><b>HR Department</b></p>"
                    : $"<p>{System.Net.WebUtility.HtmlEncode(req.Message).Replace("\n", "<br>")}</p>";

                var emailHtml = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#fff;margin:0;padding:24px;color:#1f2937;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;'>
        <div style='font-size:15px;line-height:1.6;color:#1e3a5f;'>{intro}</div>
    </div>
</body></html>";

                try
                {
                    var attachments = new List<(string FileName, byte[] Data)>();
                    if (pdfBytes != null && pdfBytes.Length > 0)
                        attachments.Add((fileName, pdfBytes));

                    await _email.SendAsync(
                        toEmail: string.Join(",", toList),
                        subject: subject,
                        htmlBody: emailHtml,
                        attachments: attachments,
                        cc: ccList,
                        bcc: bccList);

                    return Ok(new
                    {
                        success = true,
                        mode = "combined",
                        message = $"Sent 1 email with combined payslips to {toList.Count} recipient(s).",
                        to = toList,
                        cc = ccList,
                        bcc = bccList,
                        attached = fileName
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
                }
            }

            // ── Mode 2: Legacy per-employee ──
            if (req.Items == null || req.Items.Count == 0)
                return BadRequest(new { success = false, message = "No content to send." });

            var explicitTo2 = SplitEmails(req.ToEmail);
            var ccList2 = SplitEmails(req.CcEmail);
            var bccList2 = SplitEmails(req.BccEmail);

            int sent = 0, failed = 0, skipped = 0;
            var errors = new List<string>();

            foreach (var item in req.Items)
            {
                List<string> toList;
                if (explicitTo2.Count > 0) { toList = explicitTo2; }
                else
                {
                    var dbEmail = !string.IsNullOrWhiteSpace(item.Email)
                        ? item.Email
                        : await _repo.GetEmployeeEmailAsync(item.EmployeeId);

                    if (string.IsNullOrWhiteSpace(dbEmail))
                    {
                        skipped++;
                        errors.Add($"{item.EmployeeName}: no email");
                        continue;
                    }
                    toList = new List<string> { dbEmail };
                }

                var intro = string.IsNullOrWhiteSpace(req.Message)
                    ? $"Dear {item.EmployeeName},<br><br>Please find your payslip for <b>{item.Period}</b> attached.<br><br>Regards,<br><b>HR Department</b>"
                    : req.Message.Replace("\n", "<br>");

                var emailHtml = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#fff;margin:0;padding:24px;color:#1f2937;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;'>
        <div style='font-size:15px;line-height:1.6;color:#1e3a5f;'>{intro}</div>
    </div>
</body></html>";

                try
                {
                    byte[] pdfBytes = Array.Empty<byte>();
                    try { pdfBytes = await PaySlipPdfBuilder.BuildAsync(item.HtmlBody ?? "", printedBy); }
                    catch { /* non-fatal */ }

                    var fileName = $"Payslip_{item.EmployeeNo}_{item.Period.Replace("/", "-")}.pdf";
                    var subject = $"Payslip – {item.Period} – {item.EmployeeName}";

                    var attachments = new List<(string FileName, byte[] Data)>();
                    if (pdfBytes.Length > 0) attachments.Add((fileName, pdfBytes));

                    await _email.SendAsync(
                        toEmail: string.Join(",", toList),
                        subject: subject,
                        htmlBody: emailHtml,
                        attachments: attachments,
                        cc: ccList2,
                        bcc: bccList2);

                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{item.EmployeeName}: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = true,
                mode = "per-employee",
                sent,
                failed,
                skipped,
                total = req.Items.Count,
                errors
            });
        }

        // ═══════════════════════════════════════════════════════════
        // SEND EPF/ETF email
        // ═══════════════════════════════════════════════════════════
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

            var ccList = SplitEmails(req.CcEmail);
            var bccList = SplitEmails(req.BccEmail);

            var intro = string.IsNullOrWhiteSpace(req.Message)
                ? $"Dear {req.EmployeeName},<br><br>Please find your <b>EPF / ETF History Summary</b> for the period <b>{req.PeriodLabel}</b> attached.<br><br>Regards,<br><b>HR Department</b>"
                : req.Message.Replace("\n", "<br>");

            var emailHtml = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#fff;margin:0;padding:24px;color:#1f2937;'>
    <div style='max-width:640px;margin:0 auto;padding:24px;background:#f8fafc;border-radius:10px;border:1px solid #e2e8f0;'>
        <div style='font-size:15px;line-height:1.6;color:#1e3a5f;'>{intro}</div>
    </div>
</body></html>";

            byte[] pdfBytes;
            try { pdfBytes = await PaySlipPdfBuilder.BuildAsync(req.HtmlBody, GetPrintedBy()); }
            catch (Exception ex) { return StatusCode(500, new { success = false, message = "PDF failed: " + ex.Message }); }

            var safePeriod = (req.PeriodLabel ?? "Report").Replace("/", "-").Replace(" ", "_");
            var fileName = $"EPF-ETF_{req.EmployeeNo}_{safePeriod}.pdf";
            var subject = $"EPF/ETF Summary – {req.PeriodLabel} – {req.EmployeeName}";

            try
            {
                var attachments = new List<(string FileName, byte[] Data)>();
                if (pdfBytes != null && pdfBytes.Length > 0)
                    attachments.Add((fileName, pdfBytes));

                await _email.SendAsync(
                    toEmail: email, subject: subject, htmlBody: emailHtml,
                    attachments: attachments, cc: ccList, bcc: bccList);

                return Ok(new { success = true, message = $"EPF/ETF summary sent to {email}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        // SEND ATTENDANCE REPORT email (still uses PaySheetPdfBuilder)
        // ═══════════════════════════════════════════════════════════
        [HttpPost("send-attendance-report-email")]
        public async Task<IActionResult> SendAttendanceReportEmail([FromBody] SendAttendanceReportEmailRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ToEmail))
                return BadRequest(new { success = false, message = "To email is required" });

            if (string.IsNullOrWhiteSpace(req.HtmlBody))
                return BadRequest(new { success = false, message = "Report HTML is empty" });

            var toList = SplitEmails(req.ToEmail);
            if (toList.Count == 0)
                return BadRequest(new { success = false, message = "Invalid To email" });

            var ccList = SplitEmails(req.CcEmail);
            var bccList = SplitEmails(req.BccEmail);

            var periodLabel = $"{req.FromMonthName} {req.FromYear}";
            if (req.FromYear != req.ToYear || req.FromMonthName != req.ToMonthName)
                periodLabel = $"{req.FromMonthName} {req.FromYear} – {req.ToMonthName} {req.ToYear}";

            var emailHtml = AttendanceReportEmailTemplate.Build(req.ReportTitle, periodLabel, req.CustomMessage);

            byte[] pdfBytes;
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var fixedHtml = req.HtmlBody
                    .Replace("src=\"images/", $"src=\"{baseUrl}/images/")
                    .Replace("src='images/", $"src='{baseUrl}/images/");

                pdfBytes = await PaySheetPdfBuilder.BuildFromHtmlAsync(fixedHtml);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "PDF generation failed: " + ex.Message });
            }

            var safePeriod = periodLabel.Replace(" ", "_").Replace("/", "-").Replace("→", "-");
            var fileName = $"{req.ReportFileNamePrefix}_{safePeriod}.pdf";
            var subject = $"{req.ReportTitle} — {periodLabel}";

            try
            {
                await _email.SendAsync(string.Join(",", toList), subject, emailHtml,
                    pdfBytes, fileName, ccList, bccList);

                return Ok(new
                {
                    success = true,
                    message = $"Report emailed to {toList.Count} recipient(s).",
                    to = toList,
                    cc = ccList,
                    bcc = bccList
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Send failed: " + ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════
        [HttpGet("get-email/{employeeId}")]
        public async Task<IActionResult> GetEmail(int employeeId)
        {
            var email = await _repo.GetEmployeeEmailAsync(employeeId);
            if (string.IsNullOrWhiteSpace(email))
                return Ok(new { success = false, email = "", message = "No email address found" });
            return Ok(new { success = true, email });
        }

        [HttpGet("locked-periods")]
        public async Task<IActionResult> GetLockedPeriods([FromQuery] int companyId)
        {
            if (companyId <= 0)
                return BadRequest(new { success = false, message = "Invalid company" });
            var periods = await _repo.GetLockedPeriodsAsync(companyId);
            return Ok(new { success = true, data = periods });
        }

        private static string BuildPaysheetHtmlFromRepo(PaySheetPrintDto? d)
        {
            if (d == null) return "";
            try { return PaySheetEmailRenderer.BuildHtml(d); }
            catch { return ""; }
        }

        private static string Sanitize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "unknown";
            var safe = new string(s.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
            return safe.Length > 0 ? safe : "file";
        }

        private static string WrapEmailHtml(string bodyHtml)
        {
            return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Arial,sans-serif;'>
    <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;padding:24px 0;'>
        <tr><td align='center'>
            <table role='presentation' width='560' cellpadding='0' cellspacing='0'
                   style='max-width:560px;width:100%;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,0.06);'>
                <tr><td style='background:linear-gradient(135deg,#1e40af 0%,#3b82f6 60%,#60a5fa 100%);padding:22px 28px;color:#fff;'>
                    <div style='font-size:18px;font-weight:700;letter-spacing:0.3px;'>Payroll</div>
                    <div style='font-size:12px;opacity:0.85;margin-top:2px;'>Your documents are attached</div>
                </td></tr>
                <tr><td style='padding:24px 28px;color:#1e293b;font-size:14px;line-height:1.6;'>
                    {bodyHtml}
                    <p style='margin-top:24px;color:#64748b;font-size:12px;'>This is an automated message. Please do not reply directly.</p>
                </td></tr>
                <tr><td style='background:#f8fafc;padding:14px 28px;text-align:center;font-size:11px;color:#94a3b8;border-top:1px solid #e2e8f0;'>
                    Trivora HRIS · Generated {DateTime.Now:dd MMM yyyy HH:mm}
                </td></tr>
            </table>
        </td></tr>
    </table>
</body></html>";
        }

        private static List<string> SplitEmails(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new List<string>();
            return s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .ToList();
        }

        // ═══════════════════════════════════════════════════════════
        // REQUEST MODELS
        // ═══════════════════════════════════════════════════════════
        public class SendPayslipEmailRequest
        {
            public int EmployeeId { get; set; }
            public string Month { get; set; } = "";
            public int Year { get; set; }
            public string? Email { get; set; }
            public string? Message { get; set; }
            public string? HtmlBody { get; set; }
        }

        public class SendBatchPayslipEmailRequest
        {
            public int CompanyId { get; set; }
            public int? DepartmentId { get; set; }
            public string Month { get; set; } = "";
            public int Year { get; set; }
            public string? ToEmail { get; set; }
            public string? CcEmail { get; set; }
            public string? BccEmail { get; set; }
            public string? Message { get; set; }
            public string? HtmlBody { get; set; }
            public List<BatchEmailItem> Items { get; set; } = new();
        }

        public class BatchEmailItem
        {
            public int EmployeeId { get; set; }
            public string EmployeeNo { get; set; } = "";
            public string EmployeeName { get; set; } = "";
            public string Period { get; set; } = "";
            public string? Email { get; set; }
            public string? HtmlBody { get; set; }
        }

        public class SendEpfEtfEmailRequest
        {
            public int EmployeeId { get; set; }
            public string EmployeeNo { get; set; } = "";
            public string EmployeeName { get; set; } = "";
            public string PeriodLabel { get; set; } = "";
            public string? Email { get; set; }
            public string? CcEmail { get; set; }
            public string? BccEmail { get; set; }
            public string? Message { get; set; }
            public string? HtmlBody { get; set; }
        }
    }
}