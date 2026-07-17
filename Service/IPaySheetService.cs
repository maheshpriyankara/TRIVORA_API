// Services/IPaySheetService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using TRIVORA_API.Models;

namespace TRIVORA_API.Service
{
    public interface IPaySheetService
    {
        // ✅ Get paysheet by employee and month (single record)
        Task<PaySheetDto?> GetPaySheetByEmployeeAndMonthAsync(int employeeId, string month);
        
        // Get all paysheets for an employee (all months)
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByEmployeeAsync(int employeeId);
        
        // Get all paysheets for a specific month (all employees)
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByMonthAsync(string month);
        
        // Get paysheet by ID
        Task<PaySheetDto?> GetPaySheetByIdAsync(int id);
        
        // Create new paysheet
        Task<PaySheetDto?> CreatePaySheetAsync(PaySheetRequestDto request);
        
        // Update existing paysheet
        Task<PaySheetDto?> UpdatePaySheetAsync(int id, PaySheetRequestDto request);
        
        // Delete paysheet
        Task<bool> DeletePaySheetAsync(int id);
        
        // Calculate paysheet for an employee
        Task<PaySheetDto?> CalculatePaySheetAsync(int employeeId, string month);
        
        // Get all paysheets for a company and month
        Task<IEnumerable<PaySheetDto>> GetPaySheetsByCompanyAndMonthAsync(int companyId, string month);
        
        // Process payroll for all employees in a company
        Task<bool> ProcessPayrollAsync(int companyId, string month);
    }
}