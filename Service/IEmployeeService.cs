// Services/IEmployeeService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using TRIVORA_API.Models;

namespace TRIVORA_API.Services
{
    public interface IEmployeeService
    {
        Task<Employee?> GetEmployeeByIdAsync(int id);
        Task<IEnumerable<Employee>> GetEmployeesByCompanyAsync(int companyId);
        Task<Employee?> GetEmployeeByEPFAsync(string epfNo, int companyId);
        Task<Employee?> GetEmployeeByEmployeeNoAsync(string employeeNo, int companyId);
        Task<IEnumerable<Employee>> SearchEmployeesAsync(string searchTerm, int companyId);
        // Add other methods as needed
    }
}