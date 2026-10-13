using System.Collections.Generic;
using System.Threading.Tasks;

namespace TRIVORA_API.Services
{
    public interface IPaySheetService
    {
        Task<IEnumerable<TRIVORA_API.Models.PaySheetDto>> GetPaySheetsByEmployeeAsync(int employeeId);
        Task<IEnumerable<TRIVORA_API.Models.PaySheetDto>> GetPaySheetsByMonthAsync(string month);
        Task<IEnumerable<TRIVORA_API.Models.PaySheetDto>> GetPaySheetsByEmployeeAndMonthAsync(int employeeId, string month);
        Task<TRIVORA_API.Models.PaySheetDto?> GetPaySheetByIdAsync(int id);
        Task<TRIVORA_API.Models.PaySheetDto> CreatePaySheetAsync(TRIVORA_API.Models.PaySheetRequestDto request);
        Task<TRIVORA_API.Models.PaySheetDto> UpdatePaySheetAsync(int id, TRIVORA_API.Models.PaySheetRequestDto request);
        Task<bool> DeletePaySheetAsync(int id);
        Task<TRIVORA_API.Models.PaySheetDto> CalculatePaySheetAsync(int employeeId, string month);
        Task<IEnumerable<TRIVORA_API.Models.PaySheetDto>> GetPaySheetsByCompanyAndMonthAsync(int companyId, string month);
        Task<bool> ProcessPayrollAsync(int companyId, string month);
    }
}