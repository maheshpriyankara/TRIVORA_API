using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Services
{
    /// <summary>
    /// Renders the payslip HTML for emails.
    /// Mirrors the print template 1:1.
    /// Inlines logo/signature as base64 so emails are self-contained.
    /// </summary>
    public static class PaySheetEmailRenderer
    {
        public static string BuildHtml(PaySheetPrintDto d, string? customIntro = null)
        {
            var isCash     = string.Equals(d.SalaryPayType, "Cash", StringComparison.OrdinalIgnoreCase);
            var companyId  = d.CompanyId;
            var totalBasic = d.BasicSalary + d.BudgetaryAllowance + d.BudgetaryAllowance2;

            // -------- Load logo & signature as base64 --------
            var logoDataUri = TryLoadImageAsDataUri(Path.Combine("images", "logo",       $"{companyId}.png"));
            var sigDataUri  = TryLoadImageAsDataUri(Path.Combine("images", "signatures", $"{companyId}.png"));

            var intro = string.IsNullOrWhiteSpace(customIntro)
                ? $"<b>Dear {E(d.EmployeeName)},</b><br><br>" +
                  $"Please find your payslip for the period <b>{E(d.Period)}</b> below. " +
                  $"A PDF copy is attached to this email.<br><br>" +
                  $"Regards,<br><b>{E(d.CompanyName)}</b>"
                : customIntro;

            // ---- Bank footer ----
            var bankNameDisplay = string.IsNullOrEmpty(d.BankCode)
                ? E(d.BankName)
                : $"{E(d.BankName)} <span class='ps-bank-code'>({E(d.BankCode)})</span>";

            var branchNameDisplay = string.IsNullOrEmpty(d.BranchCode)
                ? E(d.BranchName)
                : $"{E(d.BranchName)} <span class='ps-bank-code'>({E(d.BranchCode)})</span>";

            var logoHtml = logoDataUri != null
                ? $"<div class='ps-logo-box'><img src='{logoDataUri}' alt='logo' width='44' height='44' /></div>"
                : "";

            var sigHtml = sigDataUri != null
                ? $"<img src='{sigDataUri}' alt='signature' style='max-height:44px;max-width:140px;' />"
                : "";

            var sb = new StringBuilder();

            sb.Append($@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>Payslip - {E(d.EmployeeName)} - {E(d.Period)}</title>
<style>
    body {{ font-family: 'Segoe UI', Roboto, Arial, sans-serif; background:#eef2f7; margin:0; padding:18px; color:#1f2937; }}
    .ps-wrap {{ max-width:760px; margin:0 auto; }}

    .ps-intro {{ background:#f0f9ff; border-left:4px solid #1e40af; padding:16px 22px;
                 border-radius:8px; font-size:13px; color:#1e3a5f; line-height:1.6;
                 margin-bottom:16px; }}

    .ps-slip {{ background:#fff; border:1px solid #d1d5db; border-radius:10px;
                overflow:hidden; font-size:11px; color:#1f2937; }}

    /* ---------- HEADER ---------- */
    .ps-header {{
        background-color: #1e3a5f;
        background: linear-gradient(135deg, #1e3a5f 0%, #2c5282 100%);
        color: #ffffff;
        padding: 18px 22px;
    }}
    .ps-header-table {{ width:100%; border-collapse:collapse; table-layout:fixed; }}
    .ps-header-left {{ width:40%; vertical-align:middle; padding:0; }}
    .ps-header-right {{ width:60%; vertical-align:middle; padding:0 0 0 18px;
                        border-left:1px solid rgba(255,255,255,0.22); }}
    .ps-logo-box {{ width:52px; height:52px; background:#ffffff; border-radius:6px;
                    display:inline-block; vertical-align:middle; padding:4px;
                    text-align:center; line-height:0; }}
    .ps-logo-box img {{ max-width:44px; max-height:44px; display:inline-block; vertical-align:middle; }}
    .ps-left-text {{ display:inline-block; vertical-align:middle; margin-left:10px; }}
    .ps-slip-title {{ font-size:20px; font-weight:800; letter-spacing:3px; line-height:1; color:#ffffff; }}
    .ps-period-tag {{ display:inline-block; background:rgba(255,255,255,0.15);
                      border:1px solid rgba(255,255,255,0.35); padding:3px 10px;
                      border-radius:3px; font-size:10px; font-weight:600;
                      letter-spacing:1px; text-transform:uppercase;
                      margin-top:5px; color:#ffffff; }}

    /* Row 1: full-width name */
    .ps-emp-full {{ display:block; padding:1px 0 3px 0; line-height:1.35; }}
    .ps-emp-full .ps-emp-label {{ font-size:8.5px; font-weight:700; color:#94a3b8;
                                  text-transform:uppercase; letter-spacing:0.6px;
                                  display:inline-block; width:92px; vertical-align:top; }}
    .ps-emp-full .ps-emp-value {{ font-weight:700; color:#ffffff; font-size:12.5px;
                                  display:inline-block; vertical-align:top;
                                  word-break:break-word; white-space:normal;
                                  max-width:calc(100% - 100px); }}

    /* Rows 2 & 3: table-based two-column */
    .ps-emp-row {{ width:100%; border-collapse:collapse; table-layout:fixed; }}
    .ps-emp-cell {{ vertical-align:top; padding:1px 6px 1px 0; }}
    .ps-emp-cell .ps-emp-label {{ font-size:8.5px; font-weight:700; color:#94a3b8;
                                  text-transform:uppercase; letter-spacing:0.6px;
                                  display:inline-block; width:92px; vertical-align:top; }}
    .ps-emp-cell .ps-emp-value {{ font-weight:700; color:#ffffff; font-size:11px;
                                  display:inline-block; vertical-align:top;
                                  word-break:break-word; white-space:normal;
                                  max-width:calc(100% - 100px); }}

    /* ---------- BODY ---------- */
    .ps-body {{ width:100%; border-collapse:collapse; table-layout:fixed; padding:14px 22px; }}
    .ps-body-table {{ width:100%; border-collapse:collapse; table-layout:fixed; }}
    .ps-col {{ display:table-cell; vertical-align:top; width:33.33%; padding:0 8px; }}
    .ps-col:first-child {{ padding-left:0; }}
    .ps-col:last-child {{ padding-right:0; }}

    .ps-col-title {{ font-size:10px; font-weight:800; color:#fff;
                     padding:5px 8px; letter-spacing:1px;
                     text-transform:uppercase; border-radius:2px; margin-bottom:6px; }}
    .ps-green {{ background:#166534; }}
    .ps-blue  {{ background:#1e40af; }}
    .ps-red   {{ background:#991b1b; }}
    .ps-amber {{ background:#92400e; }}

    .ps-row {{ display:block; padding:2.5px 5px; font-size:10px;
               border-bottom:1px dotted #e5e7eb; line-height:1.3; }}
    .ps-row .ps-row-label {{ color:#374151; }}
    .ps-row .ps-row-value {{ float:right; font-weight:600; color:#0f172a; }}
    .ps-subtotal {{ background:#f1f5f9; border-top:1.5px solid #94a3b8;
                    border-bottom:2px double #475569; padding:5px;
                    display:block; font-weight:800; font-size:10.5px; }}
    .ps-subtotal .ps-row-label,
    .ps-subtotal .ps-row-value {{ font-weight:800; color:#0f172a; }}

    .ps-summary-box {{ background:#f0f9ff; border:1.5px solid #1e40af;
                       border-radius:4px; padding:8px 10px; margin-bottom:6px; }}
    .ps-summary-row {{ display:block; padding:4px 0;
                       border-bottom:1px dashed #cbd5e1; font-size:11px; }}
    .ps-summary-label {{ color:#1e3a5f; font-weight:600;
                         text-transform:uppercase; font-size:9.5px; }}
    .ps-summary-value {{ float:right; font-weight:700; color:#0f172a; }}
    .ps-net {{ background:#1e3a5f; color:#fff; padding:8px 10px;
               margin:5px -10px -8px -10px; border-radius:0 0 4px 4px;
               display:block; font-size:13px; }}
    .ps-net .ps-summary-label,
    .ps-net .ps-summary-value {{ color:#fff; font-weight:800; }}

    .ps-stat-box {{ background:#f0fdf4; border:1px solid #16a34a;
                    border-radius:4px; padding:6px 9px; margin-top:8px; }}
    .ps-stat-row {{ display:block; padding:2.5px 0; font-size:10px; }}
    .ps-stat-label {{ color:#166534; font-weight:600; }}
    .ps-stat-value {{ float:right; color:#0f172a; font-weight:700; }}

    .ps-hours-box {{ background:#fef3c7; border:1px solid #d97706;
                     border-radius:4px; padding:6px 9px; margin-top:8px; }}
    .ps-hours-title {{ font-size:9px; font-weight:800; color:#92400e;
                       text-transform:uppercase; letter-spacing:0.6px;
                       border-bottom:1px solid #fcd34d;
                       padding-bottom:3px; margin-bottom:4px; }}
    .ps-hours-row {{ display:block; padding:2.5px 0; font-size:10px; }}
    .ps-hours-label {{ color:#78350f; font-weight:600; }}
    .ps-hours-value {{ float:right; color:#0f172a; font-weight:700;
                       font-family:Consolas,Monaco,monospace; }}

    /* ---------- FOOTER ---------- */
    .ps-footer {{ border-top:2px solid #1e3a5f; background:#f8fafc; padding:14px 22px; }}
    .ps-footer-table {{ width:100%; border-collapse:collapse; table-layout:fixed; }}
    .ps-bank-block {{ display:table-cell; vertical-align:bottom; width:65%;
                      font-size:10px; line-height:1.6; color:#334155; }}
    .ps-bank-title {{ font-size:9px; font-weight:800; color:#1e3a5f;
                      letter-spacing:1px; text-transform:uppercase;
                      margin-bottom:4px; padding-bottom:2px;
                      border-bottom:1px solid #cbd5e1; display:inline-block; }}
    .ps-bank-line {{ display:block; padding:1px 0; }}
    .ps-bank-line strong {{ color:#0f172a; min-width:74px; display:inline-block; font-weight:700; }}
    .ps-bank-code {{ color:#64748b; font-weight:600; margin-left:3px; }}

    .ps-cash-notice {{ background:#fef3c7; border:1px dashed #d97706;
                       padding:6px 10px; border-radius:4px;
                       color:#92400e; font-weight:600; font-size:10.5px; }}

    .ps-sign-block {{ display:table-cell; vertical-align:bottom; width:35%; text-align:center; }}
    .ps-sign-img-wrap {{ height:44px; margin-bottom:3px; line-height:0; }}
    .ps-sign-line {{ border-top:1px solid #334155; padding-top:3px;
                     font-size:9px; font-weight:700; color:#1e3a5f;
                     letter-spacing:0.6px; text-transform:uppercase; }}
    .ps-sign-sub {{ font-size:8px; color:#64748b; margin-top:2px; }}

    .ps-generated {{ background:#f8fafc; text-align:right;
                     padding:6px 22px 12px; font-size:8px;
                     color:#94a3b8; font-style:italic; }}
</style>
</head>
<body>
<div class='ps-wrap'>

    <div class='ps-intro'>{intro}</div>

    <div class='ps-slip'>

        <!-- ============ HEADER ============ -->
        <div class='ps-header'>
            <table class='ps-header-table' width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <td class='ps-header-left'>
                        {logoHtml}
                        <div class='ps-left-text'>
                            <div class='ps-slip-title'>PAYSLIP</div>
                            <div class='ps-period-tag'>{E(d.Period)}</div>
                        </div>
                    </td>
                    <td class='ps-header-right'>

                        <!-- Row 1: Name (full width) -->
                        <div class='ps-emp-full'>
                            <span class='ps-emp-label'>Employee</span>
                            <span class='ps-emp-value'>{E(d.EmployeeName)}</span>
                        </div>

                        <!-- Row 2: Emp No / EPF No  |  Designation -->
                        <table class='ps-emp-row' width='100%' cellpadding='0' cellspacing='0' border='0'>
                            <tr>
                                <td class='ps-emp-cell' style='width:58%;'>
                                    <span class='ps-emp-label'>Emp No / EPF No</span>
                                    <span class='ps-emp-value'>{E(d.EmployeeNo)} / {E(d.EpfNo)}</span>
                                </td>
                                <td class='ps-emp-cell' style='width:42%;'>
                                    <span class='ps-emp-label'>Designation</span>
                                    <span class='ps-emp-value'>{E(d.Designation)}</span>
                                </td>
                            </tr>
                        </table>

                        <!-- Row 3: Working Days / No Pay  |  Department -->
                        <table class='ps-emp-row' width='100%' cellpadding='0' cellspacing='0' border='0'>
                            <tr>
                                <td class='ps-emp-cell' style='width:58%;'>
                                    <span class='ps-emp-label'>Working Days</span>
                                    <span class='ps-emp-value'>{d.WorkingDays} | No Pay: {d.NoPayDays}</span>
                                </td>
                                <td class='ps-emp-cell' style='width:42%;'>
                                    <span class='ps-emp-label'>Department</span>
                                    <span class='ps-emp-value'>{E(d.Department)}</span>
                                </td>
                            </tr>
                        </table>

                    </td>
                </tr>
            </table>
        </div>

        <!-- ============ BODY ============ -->
        <div class='ps-body'>
            <table class='ps-body-table' width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <!-- EARNINGS -->
                    <td class='ps-col'>
                        <div class='ps-col-title ps-green'>Earnings</div>
                        {Row("Basic Salary", d.BasicSalary)}
                        {Row("Budgetary Allowance 1", d.BudgetaryAllowance)}
                        {Row("Budgetary Allowance 2", d.BudgetaryAllowance2)}
                        {Subtotal("Total Basic", totalBasic)}
                        {Row("Salary for EPF", d.SalaryForEPF)}

                        <div class='ps-col-title ps-blue' style='margin-top:8px;'>Allowances &amp; Overtime</div>
                        {Row("OT Normal", d.OverTimePay)}
                        {Row("OT Poya", d.OverTimePoyaPay)}
                        {Row("OT Bank Holiday", d.OverTimeBankHolidayPay)}
                        {Row("OT Sunday", d.OverTimeSundayPay)}
                        {Row("Fixed Allowance", d.FixedAllowance)}
                        {Row("Meal Allowance", d.MealAllowance)}
                        {Row("Special Allowance", d.SpecialAllowance)}
                        {Row("Transport", d.TransportAllowance)}
                        {Row("Accommodation", d.AccommodationAllowance)}
                        {Row("Fuel", d.FuelAllowance)}
                        {Row("Cost of Living", d.CostOfLivingAllowance)}
                        {Row("Performance", d.PerformanceAllowance)}
                        {Row("Health", d.HealthAllowance)}
                        {Row("Other Allowances", d.OtherAllowances)}
                        {Row("Attendance", d.AttendanceAllowance)}
                        {Subtotal("Total Earning", d.TotalEarning)}
                    </td>

                    <!-- DEDUCTIONS -->
                    <td class='ps-col'>
                        <div class='ps-col-title ps-red'>Deductions</div>
                        {Row("Pay Cut", d.PayCutDeduction)}
                        {Row("No Pay", d.NoPayDeduction)}
                        {Row("EPF 8% (Employee)", d.Epf8)}
                        {Row("PAYE Tax", d.PayeeDeduction)}
                        {Row("Cash Short", d.CashShortDeduction)}
                        {Row("Welfare", d.WelfareDeduction)}
                        {Row("Loan", d.Loan)}
                        {Row("Salary Advance", d.SalaryAdvance)}
                        {Row("Other Deductions", d.OtherDeduction)}
                        {Subtotal("Total Deductions", d.TotalDeduction)}

                        <div class='ps-stat-box'>
                            <div class='ps-stat-row'>
                                <span class='ps-stat-label'>EPF 12% (Employer)</span>
                                <span class='ps-stat-value'>{F(d.Epf12)}</span>
                            </div>
                            <div class='ps-stat-row'>
                                <span class='ps-stat-label'>ETF 3% (Employer)</span>
                                <span class='ps-stat-value'>{F(d.Etf3)}</span>
                            </div>
                        </div>

                        <div class='ps-hours-box'>
                            <div class='ps-hours-title'>Hours Summary</div>
                            <div class='ps-hours-row'><span class='ps-hours-label'>Pay Cut</span><span class='ps-hours-value'>{Sec(d.PayCutHours)}</span></div>
                            <div class='ps-hours-row'><span class='ps-hours-label'>OT Normal</span><span class='ps-hours-value'>{Sec(d.OverTimeHours)}</span></div>
                            <div class='ps-hours-row'><span class='ps-hours-label'>OT Poya</span><span class='ps-hours-value'>{Sec(d.OverTimePoyaHours)}</span></div>
                            <div class='ps-hours-row'><span class='ps-hours-label'>OT Bank Holiday</span><span class='ps-hours-value'>{Sec(d.OverTimeBankHolidayHours)}</span></div>
                            <div class='ps-hours-row'><span class='ps-hours-label'>OT Sunday</span><span class='ps-hours-value'>{Sec(d.OverTimeSundayHours)}</span></div>
                        </div>
                    </td>

                    <!-- SUMMARY -->
                    <td class='ps-col'>
                        <div class='ps-col-title ps-amber'>Payment Summary</div>
                        <div class='ps-summary-box'>
                            <div class='ps-summary-row'>
                                <span class='ps-summary-label'>Gross Salary</span>
                                <span class='ps-summary-value'>{F(d.GrossSalary)}</span>
                            </div>
                            <div class='ps-summary-row'>
                                <span class='ps-summary-label'>Total Deduction</span>
                                <span class='ps-summary-value'>− {F(d.TotalDeduction)}</span>
                            </div>
                            <div class='ps-net'>
                                <span class='ps-summary-label'>Net Salary</span>
                                <span class='ps-summary-value'>{F(d.NetPay)}</span>
                            </div>
                        </div>

                        {(isCash
                            ? "<div class='ps-cash-notice'>⚠ Salary Pay Type: CASH<br><span style='font-weight:400;font-size:9px;'>Payment will be made in cash. Bank details not applicable.</span></div>"
                            : @"<div class='ps-stat-box' style='background:#eff6ff;border-color:#1e40af;'>
                                   <div class='ps-stat-row'>
                                     <span class='ps-stat-label' style='color:#1e40af;'>Payment Mode</span>
                                     <span class='ps-stat-value'>Bank Transfer</span>
                                   </div>
                               </div>")}
                    </td>
                </tr>
            </table>
        </div>

        <!-- ============ FOOTER ============ -->
        <div class='ps-footer'>
            <table class='ps-footer-table' width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    {(isCash
                        ? @"<td class='ps-bank-block'>
                                <div class='ps-bank-title'>Payment Method</div>
                                <div style='font-size:10.5px;color:#92400e;font-weight:600;'>
                                    💵 Cash Payment — No bank details applicable
                                </div>
                             </td>"
                        : $@"<td class='ps-bank-block'>
                                <div class='ps-bank-title'>Bank Details</div>
                                <div class='ps-bank-line'><strong>Account No</strong> {E(d.AccountNumber)}</div>
                                <div class='ps-bank-line'><strong>Bank</strong> {bankNameDisplay}</div>
                                <div class='ps-bank-line'><strong>Branch</strong> {branchNameDisplay}</div>
                             </td>")}

                    <td class='ps-sign-block'>
                        <div class='ps-sign-img-wrap'>{sigHtml}</div>
                        <div class='ps-sign-line'>Authorized Signatory</div>
                        <div class='ps-sign-sub'>For {E(d.CompanyName)}</div>
                    </td>
                </tr>
            </table>
        </div>

        <div class='ps-generated'>
            Generated on {DateTime.Now:dd/MM/yyyy HH:mm} · This is a computer-generated payslip.
        </div>
    </div>
</div>
</body>
</html>");

            return sb.ToString();
        }

        // ---------- helpers ----------
        private static string Row(string label, decimal value)
            => $"<div class='ps-row'><span class='ps-row-label'>{E(label)}</span>" +
               $"<span class='ps-row-value'>{F(value)}</span></div>";

        private static string Subtotal(string label, decimal value)
            => $"<div class='ps-subtotal'><span class='ps-row-label'>{E(label)}</span>" +
               $"<span class='ps-row-value'>{F(value)}</span></div>";

        private static string F(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);

        private static string E(string s) => string.IsNullOrEmpty(s) ? "" : WebUtility.HtmlEncode(s);

        private static string Sec(int seconds)
        {
            if (seconds <= 0) return "00:00";
            var m = seconds / 60;
            var s = seconds % 60;
            return $"{m:D2}:{s:D2}";
        }

        private static string? TryLoadImageAsDataUri(string subPath)
        {
            try
            {
                var candidates = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", subPath),
                    Path.Combine(AppContext.BaseDirectory, "wwwroot", subPath),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "wwwroot", subPath),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot", subPath),
                    Path.Combine("/Users/maheshpriyankara/TRIVORA_API/wwwroot", subPath),
                };

                foreach (var c in candidates)
                {
                    var full = Path.GetFullPath(c);
                    if (File.Exists(full))
                    {
                        Console.WriteLine($"[PaySheetEmailRenderer] Loaded: {full}");
                        var bytes = File.ReadAllBytes(full);
                        var ext   = Path.GetExtension(full).ToLowerInvariant();
                        var mime  = ext switch
                        {
                            ".png"  => "image/png",
                            ".jpg"  => "image/jpeg",
                            ".jpeg" => "image/jpeg",
                            ".gif"  => "image/gif",
                            ".svg"  => "image/svg+xml",
                            _       => "image/png"
                        };
                        return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                    }
                }

                Console.WriteLine($"[PaySheetEmailRenderer] NOT FOUND: {subPath}");
                foreach (var c in candidates) Console.WriteLine($"  tried: {Path.GetFullPath(c)}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaySheetEmailRenderer] Error: {ex.Message}");
                return null;
            }
        }
    }
}