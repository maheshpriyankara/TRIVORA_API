// Services/CompanySettingsService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TRIVORA_API.Data;

namespace TRIVORA_API.Services
{
    public class CompanySettingsService : ICompanySettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CompanySettingsService> _logger;

        public CompanySettingsService(ApplicationDbContext context, ILogger<CompanySettingsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CompanySettings> GetCompanySettingsAsync(int companyId)
        {
            try
            {
                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Id == companyId);

                if (company == null)
                {
                    _logger.LogWarning($"Company with ID {companyId} not found");
                    return new CompanySettings
                    {
                        CompanyId = companyId,
                        MinBasic = 0,
                        MinBudgetaryAllowanceOne = 0,
                        MinBudgetaryAllowanceTwo = 0,
                        MaxEmployeeCount = 0,
                        SalaryStartDate_Office = 1,
                        SalaryStartDate_Wages = 1
                    };
                }

                _logger.LogInformation($"Company settings loaded: " +
                    $"MinBasic={company.MinBasic}, " +
                    $"MinBudgetaryAllowanceOne={company.MinBudgetaryAllowanceOne}, " +
                    $"MinBudgetaryAllowanceTwo={company.MinBudgetaryAllowanceTwo}");

                return new CompanySettings
                {
                    CompanyId = company.Id,
                    CompanyName = company.CompanyName,
                    MinBasic = company.MinBasic,
                    MinBudgetaryAllowanceOne = company.MinBudgetaryAllowanceOne,
                    MinBudgetaryAllowanceTwo = company.MinBudgetaryAllowanceTwo,
                    MaxEmployeeCount = company.MaxEmployeeCount,
                    SalaryStartDate_Office = company.SalaryStartDate_Office,
                    SalaryStartDate_Wages = company.SalaryStartDate_Wages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting company settings for CompanyId {companyId}");
                return new CompanySettings
                {
                    CompanyId = companyId,
                    MinBasic = 0,
                    MinBudgetaryAllowanceOne = 0,
                    MinBudgetaryAllowanceTwo = 0,
                    MaxEmployeeCount = 0,
                    SalaryStartDate_Office = 1,
                    SalaryStartDate_Wages = 1
                };
            }
        }
    }
}