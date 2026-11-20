using System.Collections.Generic;
using System.Threading.Tasks;
using TRIVORA_API.Models;

namespace TRIVORA_API.Services
{
    public interface IPaySheetService
    {
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAsync(int employeeId);
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByMonthAsync(string month);
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAndMonthAsync(int employeeId, string month);
        Task<PaySheetDto?> GetPaySheetByIdAsync(int id);
        Task<PaySheetDto> CreatePaySheetAsync(PaySheetRequestDto request);
        Task<PaySheetDto> UpdatePaySheetAsync(int id, PaySheetRequestDto request);
        Task<bool> DeletePaySheetAsync(int id);
        Task<PaySheetDto> CalculatePaySheetAsync(int employeeId, string month);
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByCompanyAndMonthAsync(int companyId, string month);
        Task<bool> ProcessPayrollAsync(int companyId, string month);
    }
}