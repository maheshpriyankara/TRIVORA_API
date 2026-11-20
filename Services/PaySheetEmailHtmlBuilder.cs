using System.Text;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Services
{
    public static class PaySheetEmailHtmlBuilder
    {
        public static string Build(PaySheetPrintDto d)
        {
            var companyId = d.CompanyId;
            var logoUrl   = $"/images/logo/{companyId}.png";   // replace with full URL if sending to external
            var isCash    = string.Equals(d.SalaryPayType, "Cash", StringComparison.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            sb.Append($@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; background:#eef2f7; margin:0; padding:20px; color:#1f2937; }}
    .wrap {{ max-width:720px; margin:0 auto; background:#fff; border-radius:10px; overflow:hidden; box-shadow:0 6px 24px rgba(0,0,0,0.08); }}
    .head {{ background:linear-gradient(135deg,#1e3a5f,#2c5282); color:#fff; padding:22px 26px; }}
    .head h1 {{ margin:0; font-size:22px; letter-spacing:3px; }}
    .head p {{ margin:6px 0 0 0; font-size:12px; color:#cbd5e1; }}
    .period {{ display:inline-block; margin-top:10px; background:rgba(255,255,255,0.15); border:1px solid rgba(255,255,255,0.35); padding:4px 12px; border-radius:4px; font-size:11px; }}
    .emp {{ display:grid; grid-template-columns:1fr 1fr; gap:4px 18px; background:#f8fafc; padding:18px 26px; border-bottom:2px solid #e2e8f0; font-size:13px; }}
    .emp b {{ color:#64748b; font-size:10px; text-transform:uppercase; letter-spacing:0.5px; margin-right:6px; }}
    .body {{ display:grid; grid-template-columns:1fr 1fr 1fr; gap:16px; padding:22px 26px; }}
    .col-title {{ font-size:12px; font-weight:800; color:#fff; padding:7px 12px; letter-spacing:1px; text-transform:uppercase; border-radius:3px; margin-bottom:8px; }}
    .g {{ background:#166534; }} .b {{ background:#1e40af; }} .r {{ background:#991b1b; }} .a {{ background:#92400e; }}
    .row {{ display:flex; justify-content:space-between; padding:4px 6px; font-size:12px; border-bottom:1px dotted #e5e7eb; }}
    .row:nth-child(even) {{ background:#fafbfc; }}
    .row .lbl {{ color:#374151; }}
    .row .val {{ font-weight:600; color:#0f172a; }}
    .subtotal {{ background:#f1f5f9; border-top:1.5px solid #94a3b8; border-bottom:2px double #475569; padding:6px; font-weight:800; display:flex; justify-content:space-between; font-size:12.5px; }}
    .summary {{ background:#f0f9ff; border:1.5px solid #1e40af; border-radius:6px; padding:12px 14px; margin-bottom:12px; }}
    .summary-row {{ display:flex; justify-content:space-between; padding:6px 0; border-bottom:1px dashed #cbd5e1; font-size:13px; }}
    .summary-row:last-child {{ border-bottom:none; }}
    .net {{ background:#1e3a5f; color:#fff; padding:10px 14px; margin:8px -14px -12px -14px; border-radius:0 0 5px 5px; display:flex; justify-content:space-between; font-weight:800; font-size:15px; }}
    .stat {{ background:#f0fdf4; border:1px solid #16a34a; border-radius:6px; padding:10px 12px; margin-top:10px; font-size:12px; }}
    .hours {{ background:#fef3c7; border:1px solid #d97706; border-radius:6px; padding:10px 12px; margin-top:10px; }}
    .hours-title {{ font-size:11px; font-weight:800; color:#92400e; text-transform:uppercase; border-bottom:1px solid #fcd34d; padding-bottom:4px; margin-bottom:6px; }}
    .hours-row {{ display:flex; justify-content:space-between; padding:3px 0; font-size:12px; }}
    .foot {{ background:#f8fafc; border-top:2px solid #1e3a5f; padding:18px 26px; display:flex; justify-content:space-between; align-items:flex-end; }}
    .bank {{ font-size:12px; line-height:1.7; color:#334155; }}
    .bank b {{ color:#0f172a; min-width:80px; display:inline-block; }}
    .cash-notice {{ background:#fef3c7; border:1px dashed #d97706; padding:10px 14px; border-radius:5px; font-size:12px; color:#92400e; font-weight:600; }}
    .gen {{ background:#f8fafc; text-align:right; font-size:11px; color:#94a3b8; font-style:italic; padding:8px 26px 14px; }}
</style>
</head>
<body>
<div class='wrap'>
    <div class='head'>
        <h1>PAYSLIP</h1>
        <p>{E(d.CompanyName)} &middot; {E(d.CompanyAddress)}</p>
        <div class='period'>{E(d.Period)}</div>
    </div>

    <div class='emp'>
        <div><b>Employee</b> {E(d.EmployeeName)}</div>
        <div><b>Emp No</b> {E(d.EmployeeNo)}</div>
        <div><b>Designation</b> {E(d.Designation)}</div>
        <div><b>EPF No</b> {E(d.EpfNo)}</div>
        <div><b>Department</b> {E(d.Department)}</div>
        <div><b>Days</b> {d.WorkingDays} | No Pay: {d.NoPayDays}</div>
    </div>

    <div class='body'>
        <div>
            <div class='col-title g'>Earnings</div>
            {Row("Basic Salary", d.BasicSalary)}
            {Row("Budgetary Allow 1", d.BudgetaryAllowance)}
            {Row("Budgetary Allow 2", d.BudgetaryAllowance2)}
            <div class='subtotal'><span>Total Basic</span><span>{F(d.BasicSalary + d.BudgetaryAllowance + d.BudgetaryAllowance2)}</span></div>
            {Row("Salary for EPF", d.SalaryForEPF)}

            <div class='col-title b' style='margin-top:10px;'>Allowances &amp; OT</div>
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
            <div class='subtotal'><span>Total Earning</span><span>{F(d.TotalEarning)}</span></div>
        </div>

        <div>
            <div class='col-title r'>Deductions</div>
            {Row("Pay Cut", d.PayCutDeduction)}
            {Row("No Pay", d.NoPayDeduction)}
            {Row("EPF 8%", d.Epf8)}
            {Row("PAYE", d.PayeeDeduction)}
            {Row("Cash Short", d.CashShortDeduction)}
            {Row("Welfare", d.WelfareDeduction)}
            {Row("Loan", d.Loan)}
            {Row("Salary Advance", d.SalaryAdvance)}
            {Row("Other Deductions", d.OtherDeduction)}
            <div class='subtotal'><span>Total Deductions</span><span>{F(d.TotalDeduction)}</span></div>

            <div class='stat'>
                <div style='display:flex;justify-content:space-between;padding:3px 0;'><span>EPF 12% (Employer)</span><b>{F(d.Epf12)}</b></div>
                <div style='display:flex;justify-content:space-between;padding:3px 0;'><span>ETF 3% (Employer)</span><b>{F(d.Etf3)}</b></div>
            </div>

            <div class='hours'>
                <div class='hours-title'>Hours Summary</div>
                <div class='hours-row'><span>Pay Cut</span><b>{Sec(d.PayCutHours)}</b></div>
                <div class='hours-row'><span>OT Normal</span><b>{Sec(d.OverTimeHours)}</b></div>
                <div class='hours-row'><span>OT Poya</span><b>{Sec(d.OverTimePoyaHours)}</b></div>
                <div class='hours-row'><span>OT Bank Holiday</span><b>{Sec(d.OverTimeBankHolidayHours)}</b></div>
                <div class='hours-row'><span>OT Sunday</span><b>{Sec(d.OverTimeSundayHours)}</b></div>
            </div>
        </div>

        <div>
            <div class='col-title a'>Payment Summary</div>
            <div class='summary'>
                <div class='summary-row'><span>Gross Salary</span><b>{F(d.GrossSalary)}</b></div>
                <div class='summary-row'><span>Total Deduction</span><b>− {F(d.TotalDeduction)}</b></div>
                <div class='net'><span>Net Salary</span><span>{F(d.NetPay)}</span></div>
            </div>
        </div>
    </div>

    <div class='foot'>
        {(isCash
            ? "<div class='cash-notice'>💵 Cash Payment — No bank details applicable</div>"
            : $@"<div class='bank'>
                    <div style='font-weight:800;color:#1e3a5f;text-transform:uppercase;letter-spacing:1px;margin-bottom:4px;'>Bank Details</div>
                    <div><b>Account No</b>{E(d.AccountNumber)}</div>
                    <div><b>Bank</b>{E(d.BankName)} {(!string.IsNullOrEmpty(d.BankCode) ? "(" + E(d.BankCode) + ")" : "")}</div>
                    <div><b>Branch</b>{E(d.BranchName)} {(!string.IsNullOrEmpty(d.BranchCode) ? "(" + E(d.BranchCode) + ")" : "")}</div>
                 </div>")}
    </div>

    <div class='gen'>Generated on {DateTime.Now:dd/MM/yyyy HH:mm} · Computer-generated payslip.</div>
</div>
</body>
</html>");

            return sb.ToString();
        }

        private static string Row(string label, decimal value)
            => $"<div class='row'><span class='lbl'>{E(label)}</span><span class='val'>{F(value)}</span></div>";

        private static string F(decimal v)
            => v.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

        private static string E(string s)
            => string.IsNullOrEmpty(s) ? "" : System.Net.WebUtility.HtmlEncode(s);

        private static string Sec(int seconds)
        {
            if (seconds <= 0) return "00:00";
            var m = seconds / 60;
            var s = seconds % 60;
            return $"{m:D2}:{s:D2}";
        }
    }
}