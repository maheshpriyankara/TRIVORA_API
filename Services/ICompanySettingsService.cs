// Services/ICompanySettingsService.cs
using System.Threading.Tasks;

namespace TRIVORA_API.Services
{
    public interface ICompanySettingsService
    {
        Task<CompanySettings> GetCompanySettingsAsync(int companyId);
    }

    public class CompanySettings
    {
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public decimal MinBasic { get; set; }
        public decimal MinBudgetaryAllowanceOne { get; set; }
        public decimal MinBudgetaryAllowanceTwo { get; set; }
        public int MaxEmployeeCount { get; set; }
        public int SalaryStartDate_Wages { get; set; }
        public int SalaryStartDate_Office { get; set; }
    }
}