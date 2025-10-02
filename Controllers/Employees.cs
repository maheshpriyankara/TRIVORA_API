// Controllers/EmployeesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployeesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/employees/search?term={term}
        [HttpGet("search")]
        public async Task<ActionResult<ApiResponse>> SearchEmployees(string term)
        {
            try
            {
                // SQL Query: Search by name, EPF no, or employee no
                var employees = await _context.Employees
                    .Where(e => e.FirstName.Contains(term) ||
                               e.LastName.Contains(term) ||
                               e.EPFNo.ToString().Contains(term) ||
                               e.EmployeeNo.Contains(term))
                    .Select(e => new {
                        value = e.EPFNo,
                        label = $"{e.FirstName} {e.LastName} ({e.EPFNo})"
                    })
                    .ToListAsync();

                return new ApiResponse { Success = true, Data = employees };
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse { Success = false, Message = $"Search failed: {ex.Message}" });
            }
        }

        // GET: api/employees/{identifier}
        [HttpGet("{identifier}")]
        public async Task<ActionResult<ApiResponse>> GetEmployee(int identifier)
        {
            try
            {
                // SQL Query: Get employee by EPF no or Employee no
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EPFNo == identifier);

                if (employee == null)
                {
                    return Ok(new ApiResponse { Success = false, Message = "Employee not found" });
                }

                return new ApiResponse { Success = true, Data = employee };
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse { Success = false, Message = $"Error loading employee: {ex.Message}" });
            }
        }

        // POST: api/employees
        [HttpPost]

        public async Task<ActionResult<ApiResponse>> SaveEmployee([FromBody] Employee employeeData)
        {
            try
            {
                // Check if employee exists (update) or new (add)
                var existingEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EPFNo == employeeData.EPFNo);

                if (existingEmployee != null)
                {
                    // Update existing employee - EF Core will generate UPDATE SQL
                    _context.Entry(existingEmployee).CurrentValues.SetValues(employeeData);
                    await _context.SaveChangesAsync();

                    return new ApiResponse { Success = true, Message = "Employee updated successfully" };
                }
                else
                {
                    // Add new employee - EF Core will generate INSERT SQL
                    _context.Employees.Add(employeeData);
                    await _context.SaveChangesAsync();

                    return new ApiResponse { Success = true, Message = "Employee created successfully" };
                }
            }
            catch (Exception ex)
            {
                var dd = ex.Message;
                    return Ok(new ApiResponse { Success = false, Message = $"Error saving employee: {ex.InnerException?.Message}" });
            }
        }

        // DELETE: api/employees/{epfNo}
        [HttpDelete("{epfNo}")]
        public async Task<ActionResult<ApiResponse>> DeleteEmployee(int epfNo)
        {
            try
            {
                // SQL Query: Find and delete employee
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EPFNo == epfNo);

                if (employee == null)
                {
                    return Ok(new ApiResponse { Success = false, Message = "Employee not found" });
                }

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();

                return new ApiResponse { Success = true, Message = "Employee deleted successfully" };
            }
            catch (Exception ex)
            {
                return Ok(new ApiResponse { Success = false, Message = $"Error deleting employee: {ex.Message}" });
            }
        }
    }

    // Response model to match your front-end expectations
    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}