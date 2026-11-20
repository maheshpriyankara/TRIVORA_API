// ─────────────────────────────────────────────────────────────
// File: Services/TimesheetReportEmailTemplate.cs
// ─────────────────────────────────────────────────────────────
using System.Text;

namespace TrivoraHrisBackend.Services
{
    public static class TimesheetReportEmailTemplate
    {
        public static string Build(string periodLabel, string? customMessage)
        {
            var intro = string.IsNullOrWhiteSpace(customMessage)
                ? $"Please find the Attendance Timesheet report for the period <b>{System.Net.WebUtility.HtmlEncode(periodLabel)}</b> attached to this email."
                : System.Net.WebUtility.HtmlEncode(customMessage).Replace("\n", "<br>");

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head>");
            sb.Append("<meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.Append("</head>");
            sb.Append("<body style=\"margin:0;padding:0;background:#f5f7fa;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Arial,sans-serif;color:#1f2937;\">");
            sb.Append("  <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f5f7fa;padding:24px 0;\">");
            sb.Append("    <tr>");
            sb.Append("      <td align=\"center\">");
            sb.Append("        <table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:600px;width:100%;background:#ffffff;border:1px solid #e5e7eb;border-radius:10px;overflow:hidden;\">");
            // ── Header ──
            sb.Append("          <tr>");
            sb.Append("            <td style=\"background:#1e40af;padding:20px 28px;\">");
            sb.Append("              <h1 style=\"margin:0;font-size:18px;font-weight:600;color:#ffffff;\">Attendance Timesheet Report</h1>");
            sb.Append("              <p style=\"margin:4px 0 0 0;font-size:13px;color:#c7d2fe;\">").Append(System.Net.WebUtility.HtmlEncode(periodLabel)).Append("</p>");
            sb.Append("            </td>");
            sb.Append("          </tr>");
            // ── Body ──
            sb.Append("          <tr>");
            sb.Append("            <td style=\"padding:28px;font-size:14px;line-height:1.6;color:#374151;\">");
            sb.Append("              <p style=\"margin:0 0 16px 0;\">").Append(intro).Append("</p>");
            sb.Append("              <p style=\"margin:0 0 8px 0;font-size:13px;color:#6b7280;\">The full report is attached as a PDF.</p>");
            sb.Append("            </td>");
            sb.Append("          </tr>");
            // ── Divider ──
            sb.Append("          <tr><td style=\"padding:0 28px;\"><hr style=\"border:none;border-top:1px solid #e5e7eb;margin:0;\" /></td></tr>");
            // ── Footer ──
            sb.Append("          <tr>");
            sb.Append("            <td style=\"padding:16px 28px;font-size:12px;color:#9ca3af;\">This is an automated message from the HRIS attendance system.</td>");
            sb.Append("          </tr>");
            sb.Append("        </table>");
            sb.Append("      </td>");
            sb.Append("    </tr>");
            sb.Append("  </table>");
            sb.Append("</body></html>");

            return sb.ToString();
        }
    }
}