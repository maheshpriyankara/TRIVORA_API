// Controllers/EmployeesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TRIVORA_API.Data;
using TRIVORA_API.Models;

namespace TRIVORA_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(ApplicationDbContext context, ILogger<EmployeesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("search")]
public async Task<ActionResult<ApiResponse<List<EmployeeSearchResult>>>> SearchEmployees([FromQuery] string term, [FromQuery] int? companyId)
{
    try
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Ok(new ApiResponse<List<EmployeeSearchResult>> { 
                Success = false, 
                Message = "Search term must be at least 2 characters long" 
            });

        var query = _context.Employees
            .Where(e => e.IsActive);

        // Filter by company if provided
        if (companyId.HasValue && companyId > 0)
            query = query.Where(e => e.CompanyId == companyId.Value);

        // Search across multiple fields using LIKE pattern
        var searchTerm = $"%{term}%";
        var employees = await query
            .Where(e => 
                EF.Functions.Like(e.FirstName, searchTerm) ||
                EF.Functions.Like(e.LastName, searchTerm) ||
                EF.Functions.Like(e.SystemName, searchTerm) ||
                EF.Functions.Like(e.EPFNo, searchTerm) ||
                EF.Functions.Like(e.EmployeeNo, searchTerm) ||
                EF.Functions.Like(e.AttendanceId, searchTerm) ||
                EF.Functions.Like(e.NIC, searchTerm) ||
                EF.Functions.Like(e.Mobile, searchTerm) ||
                (e.FirstName + " " + e.LastName).Contains(term)
            )
            .Select(e => new EmployeeSearchResult
            {
                Id = e.Id,
                EmployeeNo = e.EmployeeNo ?? string.Empty,
                EPFNo = e.EPFNo ?? string.Empty,
                AttendanceId = e.AttendanceId ?? string.Empty,
                FirstName = e.FirstName ?? string.Empty,
                LastName = e.LastName ?? string.Empty,
                SystemName = e.SystemName ?? string.Empty,
                NIC = e.NIC ?? string.Empty,
                Mobile = e.Mobile ?? string.Empty,
                CompanyId = e.CompanyId,
                DesignationId = e.DesignationId ?? 0, // Handle nullable int
                DepartmentId = e.DepartmentId ?? 0,   // Handle nullable int
                FullName = (e.FirstName ?? "") + " " + (e.LastName ?? ""),
                DisplayText = $"{e.FirstName} {e.LastName} ({e.EPFNo}) - {e.SystemName}"
            })
            .OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .Take(50)
            .ToListAsync();

        return Ok(new ApiResponse<List<EmployeeSearchResult>> { 
            Success = true, 
            Data = employees,
            Message = employees.Count > 0 ? $"Found {employees.Count} employees" : "No employees found"
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error searching employees");
        return StatusCode(500, new ApiResponse<List<EmployeeSearchResult>> { 
            Success = false, 
            Message = "Internal server error" 
        });
    }
}

// Updated Search DTO with proper null handling
public class EmployeeSearchResult
{
    public int Id { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string EPFNo { get; set; } = string.Empty;
    public string AttendanceId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string NIC { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public int DesignationId { get; set; } // Changed from int? to int
    public int DepartmentId { get; set; }  // Changed from int? to int
    public string FullName { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
}
   
    [HttpGet("debug/employee/{id}")]
    public async Task<ActionResult> DebugEmployee(int id)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return Ok(new
                {
                    exists = false,
                    message = $"No employee found with ID {id}"
                });
            }

            return Ok(new
            {
                exists = true,
                isActive = employee.IsActive,
                employee = new
                {
                    employee.Id,
                    employee.EmployeeNo,
                    employee.EPFNo,
                    employee.FirstName,
                    employee.LastName,
                    employee.CompanyId,
                    employee.DesignationId,
                    employee.DepartmentId,
                    employee.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in debug employee endpoint");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    [HttpGet("debug/all-employees")]
    public async Task<ActionResult> DebugAllEmployees()
    {
        try
        {
            var employees = await _context.Employees
                .Select(e => new
                {
                    e.Id,
                    e.EmployeeNo,
                    e.EPFNo,
                    e.FirstName,
                    e.LastName,
                    e.IsActive
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount = employees.Count,
                employees
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all employees");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    // GET: api/employees/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployee(int id)
    {
        try
        {
            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

            if (employee == null)
                return NotFound(new ApiResponse<Employee> { Success = false, Message = "Employee not foundff" });

            return Ok(new ApiResponse<Employee> { Success = true, Data = employee });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Internal server error" });
        }
    }

    // GET: api/employees/byidentifier/EPF001
    [HttpGet("byidentifier/{identifier}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployeeByIdentifier(string identifier)
    {
        try
        {
            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.IsActive && (
                    e.EPFNo == identifier ||
                    e.EmployeeNo == identifier ||
                    e.AttendanceId == identifier
                ));

            if (employee == null)
                return NotFound(new ApiResponse<Employee> { Success = false, Message = "Employee not found" });

            return Ok(new ApiResponse<Employee> { Success = true, Data = employee });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee by identifier");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Internal server error" });
        }
    }

    // POST: api/employees
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Employee>>> CreateEmployee(Employee employee)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Sanitize data first - set defaults for all fields
            SanitizeEmployeeData(employee);

            // Validate required fields
            var validationResult = ValidateEmployee(employee);
            if (!validationResult.IsValid)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = validationResult.ErrorMessage });

            // Check for duplicates
            if (employee.EPFNo != "0")
            {
                if (await _context.Employees.AnyAsync(e => e.EPFNo == employee.EPFNo && e.CompanyId == employee.CompanyId && e.IsActive))
                    return Conflict(new ApiResponse<Employee> { Success = false, Message = "Employee with this EPF number already exists in this company" });
            }
            if (employee.EmployeeNo != "0")
            {
                if (await _context.Employees.AnyAsync(e => e.EmployeeNo == employee.EmployeeNo && e.CompanyId == employee.CompanyId && e.IsActive))
                    return Conflict(new ApiResponse<Employee> { Success = false, Message = "Employee with this employee number already exists in this company" });
            }

            // **FIX: Store related entities temporarily and clear them**
            var qualifications = employee.Qualifications?.ToList();
            var workExperiences = employee.WorkExperiences?.ToList();

            employee.Qualifications = new List<Qualification>();
            employee.WorkExperiences = new List<WorkExperience>();

            // Create employee first
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // **FIX: Add qualifications after employee is created**
            if (qualifications?.Any() == true)
            {
                foreach (var qualification in qualifications)
                {
                    qualification.Id = 0; // Ensure ID is reset
                    qualification.EmployeeId = employee.Id;
                    SanitizeQualificationData(qualification);
                    _context.Qualifications.Add(qualification);
                }
                await _context.SaveChangesAsync();
            }

            // **FIX: Add work experiences after employee is created**
            if (workExperiences?.Any() == true)
            {
                foreach (var experience in workExperiences)
                {
                    experience.Id = 0; // Ensure ID is reset
                    experience.EmployeeId = employee.Id;
                    SanitizeWorkExperienceData(experience);
                    _context.WorkExperiences.Add(experience);
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // Log initial salary
            await LogBasicSalaryChange(employee.Id, null, employee.BasicSalary, employee.ModifiedBy);

            // Reload employee with related entities
            var createdEmployee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.Id == employee.Id);

            return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id },
                new ApiResponse<Employee> { Success = true, Message = "Employee created successfully", Data = createdEmployee });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating employee");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Internal server error" });
        }
    }
    // PUT: api/employees/5
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Employee>>> UpdateEmployee(int id, Employee employee)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            if (id != employee.Id)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = "ID mismatch" });

            // Get existing employee WITHOUT tracking (to avoid concurrency issues)
            var existingEmployee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

            if (existingEmployee == null)
                return NotFound(new ApiResponse<Employee> { Success = false, Message = "Employee not found" });

            // Sanitize the updated data
            SanitizeEmployeeData(employee);

            // Validate required fields
            var validationResult = ValidateEmployee(employee);
            if (!validationResult.IsValid)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = validationResult.ErrorMessage });

            // Check for duplicates (excluding current employee)
            if (await _context.Employees.AnyAsync(e => e.EPFNo == employee.EPFNo && e.CompanyId == employee.CompanyId && e.Id != id && e.IsActive))
                return Conflict(new ApiResponse<Employee> { Success = false, Message = "Another employee with this EPF number already exists in this company" });

            if (await _context.Employees.AnyAsync(e => e.EmployeeNo == employee.EmployeeNo && e.CompanyId == employee.CompanyId && e.Id != id && e.IsActive))
                return Conflict(new ApiResponse<Employee> { Success = false, Message = "Another employee with this employee number already exists in this company" });

            // Track changes for audit
            await TrackChangesForAudit(existingEmployee, employee);

            // Update main employee properties
            employee.ModifiedDate = DateTime.UtcNow;
            employee.IsActive = true; // Ensure it stays active

            // **FIX: Clear navigation properties before update to avoid concurrency issues**
            var qualifications = employee.Qualifications?.ToList();
            var workExperiences = employee.WorkExperiences?.ToList();
            
            employee.Qualifications = null;
            employee.WorkExperiences = null;

            // Update the employee (without qualifications/experiences)
            _context.Employees.Update(employee);
            await _context.SaveChangesAsync();

            // **FIX: Handle qualifications separately**
            if (qualifications?.Any() == true)
            {
                // Remove existing qualifications
                var existingQuals = await _context.Qualifications
                    .Where(q => q.EmployeeId == id)
                    .ToListAsync();
                _context.Qualifications.RemoveRange(existingQuals);

                // Add updated qualifications
                foreach (var qual in qualifications)
                {
                    qual.Id = 0; // Reset ID for new entries
                    qual.EmployeeId = id;
                    SanitizeQualificationData(qual);
                    _context.Qualifications.Add(qual);
                }
                await _context.SaveChangesAsync();
            }

            // **FIX: Handle work experiences separately**
            if (workExperiences?.Any() == true)
            {
                // Remove existing experiences
                var existingExps = await _context.WorkExperiences
                    .Where(w => w.EmployeeId == id)
                    .ToListAsync();
                _context.WorkExperiences.RemoveRange(existingExps);

                // Add updated experiences
                foreach (var exp in workExperiences)
                {
                    exp.Id = 0; // Reset ID for new entries
                    exp.EmployeeId = id;
                    SanitizeWorkExperienceData(exp);
                    _context.WorkExperiences.Add(exp);
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // Reload the complete employee with related data
            var updatedEmployee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.Id == id);

            return Ok(new ApiResponse<Employee> { Success = true, Message = "Employee updated successfully", Data = updatedEmployee });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error updating employee");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Internal server error" });
        }
    }
    // DELETE: api/employees/5
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployee(int id)
    {
        try
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null || !employee.IsActive)
                return NotFound(new ApiResponse<bool> { Success = false, Message = "Employee not found" });

            employee.IsActive = false;
            employee.ModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<bool> { Success = true, Message = "Employee deleted successfully", Data = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee");
            return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
        }
    }

    [HttpDelete("byempno/{empNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployeeByEmpNoAndCompany(string empNo, int companyId)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeNo == empNo && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
                return NotFound(new ApiResponse<bool> { 
                    Success = false, 
                    Message = $"Employee with Employee No {empNo} not found in company" 
                });

            employee.IsActive = false;
            employee.ModifiedDate = DateTime.UtcNow;
            employee.ModifiedBy = "System"; // or get from auth context
            
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<bool> { 
                Success = true, 
                Message = "Employee deleted successfully", 
                Data = true 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee by Employee No and company");
            return StatusCode(500, new ApiResponse<bool> { 
                Success = false, 
                Message = "Internal server error" 
            });
        }
    }

    [HttpDelete("byepf/{epfNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployeeByEPFAndCompany(string epfNo, int companyId)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EPFNo == epfNo && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
                return NotFound(new ApiResponse<bool> { 
                    Success = false, 
                    Message = $"Employee with EPF {epfNo} not found in company" 
                });

            employee.IsActive = false;
            employee.ModifiedDate = DateTime.UtcNow;
            employee.ModifiedBy = "System"; // or get from auth context
            
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<bool> { 
                Success = true, 
                Message = "Employee deleted successfully", 
                Data = true 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee by EPF and company");
            return StatusCode(500, new ApiResponse<bool> { 
                Success = false, 
                Message = "Internal server error" 
            });
        }
    }
    [HttpGet("byepf/{epfNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployeeByEPFAndCompany(string epfNo, int companyId)
    {
        try
        {
            _logger.LogInformation($"Searching employee with EPF: {epfNo} in company: {companyId}");

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.EPFNo == epfNo && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
            {
                _logger.LogWarning($"Employee not found with EPF: {epfNo} in company: {companyId}");
                return NotFound(new ApiResponse<Employee>
                {
                    Success = false,
                    Message = $"Employee with EPF {epfNo} not found in selected company"
                });
            }

            _logger.LogInformation($"Found employee: {employee.FirstName} {employee.LastName} (ID: {employee.Id})");
            return Ok(new ApiResponse<Employee> { Success = true, Data = employee });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting employee with EPF: {epfNo} in company: {companyId}");
            return StatusCode(500, new ApiResponse<Employee>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    [HttpGet("byempno/{empNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployeeByEmpNoAndCompany(string empNo, int companyId)
    {
        try
        {
            _logger.LogInformation($"Searching employee with EPF: {empNo} in company: {companyId}");

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.AttendanceId == empNo && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
            {
                _logger.LogWarning($"Employee not found with Attendance Id: {empNo} in company: {companyId}");
                return NotFound(new ApiResponse<Employee>
                {
                    Success = false,
                    Message = $"Employee with Attendance Id {empNo} not found in selected company"
                });
            }

            _logger.LogInformation($"Found employee: {employee.FirstName} {employee.LastName} (ID: {employee.Id})");
            return Ok(new ApiResponse<Employee> { Success = true, Data = employee });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting employee with Attendance Id: {empNo} in company: {companyId}");
            return StatusCode(500, new ApiResponse<Employee>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }
   
    
    #region Private Methods

    [HttpGet("byidentifier/{identifier}/{companyId}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployeeByIdentifierAndCompany(string identifier, int companyId)
    {
        try
        {
            _logger.LogInformation($"Searching employee with identifier: {identifier} in company: {companyId}");

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.IsActive && e.CompanyId == companyId && (
                    e.EPFNo == identifier ||
                    e.EmployeeNo == identifier ||
                    e.AttendanceId == identifier
                ));

            if (employee == null)
            {
                _logger.LogWarning($"Employee not found with identifier: {identifier} in company: {companyId}");
                return NotFound(new ApiResponse<Employee>
                {
                    Success = false,
                    Message = $"Employee with identifier {identifier} not found in selected company"
                });
            }

            _logger.LogInformation($"Found employee: {employee.FirstName} {employee.LastName} (ID: {employee.Id})");
            return Ok(new ApiResponse<Employee> { Success = true, Data = employee });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting employee with identifier: {identifier} in company: {companyId}");
            return StatusCode(500, new ApiResponse<Employee>
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }
    private void SanitizeQualificationData(Qualification qualification)
    {
        qualification.QualificationName = qualification.QualificationName ?? string.Empty;
        qualification.InstituteName = qualification.InstituteName ?? string.Empty;
        qualification.Month = qualification.Month ?? string.Empty;
        qualification.Description = qualification.Description ?? string.Empty;

        // Check for default DateTime value
        if (qualification.CreatedDate == default(DateTime))
            qualification.CreatedDate = DateTime.UtcNow;
    }

    private void SanitizeWorkExperienceData(WorkExperience workExperience)
    {
        workExperience.Position = workExperience.Position ?? string.Empty;
        workExperience.Company = workExperience.Company ?? string.Empty;
        workExperience.FromMonth = workExperience.FromMonth ?? string.Empty;
        workExperience.ToMonth = workExperience.ToMonth ?? string.Empty;
        workExperience.Description = workExperience.Description ?? string.Empty;

        // Check for default DateTime value
        if (workExperience.CreatedDate == default(DateTime))
            workExperience.CreatedDate = DateTime.UtcNow;
    }
    private (bool IsValid, string ErrorMessage) ValidateEmployee(Employee employee)
    {
        if (string.IsNullOrWhiteSpace(employee.FirstName))
            return (false, "First name is required");

        if (string.IsNullOrWhiteSpace(employee.LastName))
            return (false, "Last name is required");

        if (string.IsNullOrWhiteSpace(employee.SystemName))
            return (false, "System name is required");

        if (employee.EPFPay)
        {
            if (string.IsNullOrWhiteSpace(employee.EPFNo))
                return (false, "EPF number is required when EPF Pay is enabled");

            if (string.IsNullOrWhiteSpace(employee.EmployeeNo))
                return (false, "Employee number is required when EPF Pay is enabled");

            if (string.IsNullOrWhiteSpace(employee.Initial))
                return (false, "Initial is required when EPF Pay is enabled");

            if (string.IsNullOrWhiteSpace(employee.NIC))
                return (false, "NIC is required when EPF Pay is enabled");
        }
        else
        {
            // When EPF Pay is not checked, generate default values
            if (string.IsNullOrWhiteSpace(employee.EmployeeNo))
                employee.EmployeeNo = "0"; // Your logic to generate employee number

            if (string.IsNullOrWhiteSpace(employee.EPFNo))
                employee.EPFNo = "0"; // Or generate a default EPF number
        }
        if (employee.CompanyId <= 0)
            return (false, "Company is required");

        if (!employee.DesignationId.HasValue || employee.DesignationId <= 0)
            return (false, "Designation is required");

        if (!employee.DepartmentId.HasValue || employee.DepartmentId <= 0)
            return (false, "Department is required");

        if (!employee.ShiftBlockId.HasValue || employee.ShiftBlockId <= 0)
            return (false, "ShiftBlock is required");

        if (employee.BasicSalary < 0)
            return (false, "Basic salary must be positive");

        // Validate bank details if Bank Pay is selected
        if (employee.SalaryPayType == "BankPay")
        {
            if (string.IsNullOrWhiteSpace(employee.AccountNumber))
                return (false, "Account number is required for Bank Pay");

            if (string.IsNullOrWhiteSpace(employee.BankAccountName))
                return (false, "Bank account name is required for Bank Pay");

            if (string.IsNullOrWhiteSpace(employee.BankCode))
                return (false, "Bank code is required for Bank Pay");

            if (string.IsNullOrWhiteSpace(employee.BranchCode))
                return (false, "Branch code is required for Bank Pay");
        }

        // Validate date of birth (must be at least 16 years old)
        if (employee.DOB.HasValue && employee.DOB.Value > DateTime.Today.AddYears(-16))
            return (false, "Employee must be at least 16 years old");

        // Validate date of appointment cannot be in future
        if (employee.DateOfAppointment.HasValue && employee.DateOfAppointment.Value > DateTime.Today)
            return (false, "Date of appointment cannot be in the future");

        // Validate resigned date
        if (employee.Resigned && employee.ResignedDate.HasValue)
        {
            if (employee.ResignedDate.Value > DateTime.Today)
                return (false, "Resigned date cannot be in the future");

            if (employee.DateOfAppointment.HasValue && employee.ResignedDate.Value < employee.DateOfAppointment.Value)
                return (false, "Resigned date cannot be before appointment date");
        }

        return (true, null);
    }
    private void SanitizeEmployeeData(Employee employee)
    {
        // Set default values for all string fields to empty string instead of null
        employee.Title = employee.Title ?? string.Empty;
        employee.Initial = employee.Initial ?? string.Empty;
        employee.FirstName = employee.FirstName ?? string.Empty;
        employee.LastName = employee.LastName ?? string.Empty;
        employee.SystemName = employee.SystemName ?? string.Empty;
        employee.NIC = employee.NIC ?? string.Empty;
        employee.Gender = employee.Gender ?? string.Empty;
        employee.MaritalStatus = employee.MaritalStatus ?? string.Empty;
        employee.BloodGroup = employee.BloodGroup ?? string.Empty;
        employee.DrivingLicense = employee.DrivingLicense ?? string.Empty;
        employee.Religion = employee.Religion ?? string.Empty;
        employee.Nationality = employee.Nationality ?? string.Empty;
        employee.Race = employee.Race ?? string.Empty;
        employee.Mobile = employee.Mobile ?? string.Empty;
        employee.LandPhone = employee.LandPhone ?? string.Empty;
        employee.ContactNo = employee.ContactNo ?? string.Empty;
        employee.ResidentialAddress = employee.ResidentialAddress ?? string.Empty;
        employee.PermanentAddress = employee.PermanentAddress ?? string.Empty;
        employee.ProfileImage = employee.ProfileImage ?? string.Empty;
        employee.EmployeeNo = employee.EmployeeNo ?? string.Empty;
        employee.EPFNo = employee.EPFNo ?? string.Empty;
        employee.AttendanceId = employee.AttendanceId ?? string.Empty;

        // Bank details
        employee.SalaryPayType = employee.SalaryPayType ?? "CashPay";
        employee.AccountNumber = employee.AccountNumber ?? string.Empty;
        employee.BankAccountName = employee.BankAccountName ?? string.Empty;
        employee.BankCode = employee.BankCode ?? string.Empty;
        employee.BankName = employee.BankName ?? string.Empty;
        employee.BranchCode = employee.BranchCode ?? string.Empty;
        employee.BranchName = employee.BranchName ?? string.Empty;

        // Status fields
        employee.ReviewedBy = employee.ReviewedBy ?? string.Empty;
        employee.BlockUntil = employee.BlockUntil ?? string.Empty;
        employee.BlockReason = employee.BlockReason ?? string.Empty;
        employee.BlockRemark = employee.BlockRemark ?? string.Empty;
        employee.ExitType = employee.ExitType ?? string.Empty;
        employee.ExitReason = employee.ExitReason ?? string.Empty;
        employee.ExitRemark = employee.ExitRemark ?? string.Empty;

        // Emergency contacts
        employee.Keen1ContactName = employee.Keen1ContactName ?? string.Empty;
        employee.Keen1ContactNumber = employee.Keen1ContactNumber ?? string.Empty;
        employee.Keen1Relationship = employee.Keen1Relationship ?? string.Empty;
        employee.Keen1Address = employee.Keen1Address ?? string.Empty;
        employee.Keen1Position = employee.Keen1Position ?? string.Empty;
        employee.Keen1WorkPlace = employee.Keen1WorkPlace ?? string.Empty;
        employee.Keen1WorkPlaceContact = employee.Keen1WorkPlaceContact ?? string.Empty;
        employee.Keen2ContactName = employee.Keen2ContactName ?? string.Empty;
        employee.Keen2ContactNumber = employee.Keen2ContactNumber ?? string.Empty;
        employee.Keen2Relationship = employee.Keen2Relationship ?? string.Empty;
        employee.Keen2Address = employee.Keen2Address ?? string.Empty;
        employee.Keen2Position = employee.Keen2Position ?? string.Empty;
        employee.Keen2WorkPlace = employee.Keen2WorkPlace ?? string.Empty;
        employee.Keen2WorkPlaceContact = employee.Keen2WorkPlaceContact ?? string.Empty;

        // Audit fields
        employee.CreatedBy = employee.CreatedBy ?? "System";
        employee.ModifiedBy = employee.ModifiedBy ?? "System";

        // Set default dates - DateTime is non-nullable, so we check for default value
        if (employee.CreatedDate == default(DateTime))
            employee.CreatedDate = DateTime.UtcNow;

        employee.ModifiedDate = DateTime.UtcNow;

        // Set default boolean values
        employee.IsActive = true; // Default to active when creating
        employee.EPFPay = employee.EPFPay; // Keep existing value
        employee.Probation = employee.Probation; // Keep existing value
        employee.Block = employee.Block; // Keep existing value
        employee.Resigned = employee.Resigned; // Keep existing value
        employee.BlockAttendance = employee.BlockAttendance; // Keep existing value

        // Set default numeric values
        employee.BasicSalary = employee.BasicSalary;
        employee.BudgetaryAllowance = employee.BudgetaryAllowance;
        employee.BudgetaryAllowance2 = employee.BudgetaryAllowance2;
        employee.AttendanceAllowance = employee.AttendanceAllowance;
        employee.FixedAllowance = employee.FixedAllowance;
        employee.MealAllowance = employee.MealAllowance;
        employee.SpecialAllowance = employee.SpecialAllowance;
        employee.TransportAllowance = employee.TransportAllowance;
        employee.AccommodationAllowance = employee.AccommodationAllowance;
        employee.FuelAllowance = employee.FuelAllowance;
        employee.CostOfLivingAllowance = employee.CostOfLivingAllowance;
        employee.PerformanceAllowance = employee.PerformanceAllowance;
        employee.HealthAllowance = employee.HealthAllowance;
    }
    private async Task TrackChangesForAudit(Employee existing, Employee updated)
    {
        var changedBy = updated.ModifiedBy ?? "System";

        // Track basic salary changes
        if (existing.BasicSalary != updated.BasicSalary)
        {
            await LogBasicSalaryChange(existing.Id, existing.BasicSalary, updated.BasicSalary, changedBy);
        }

        // Track probation/block/resignation changes
        await TrackStatusChanges(existing, updated, changedBy);

        // Track bank details changes
        await TrackBankDetailsChanges(existing, updated, changedBy);
    }

    private async Task LogBasicSalaryChange(int employeeId, decimal? oldSalary, decimal? newSalary, string changedBy)
    {
        // Ensure changedBy is never null
        var changedByValue = changedBy ?? "System";

        _context.BasicSalaryHistory.Add(new BasicSalaryHistory
        {
            EmployeeId = employeeId,
            OldBasicSalary = oldSalary,
            NewBasicSalary = newSalary,
            ChangedBy = changedByValue, // Use the ensured non-null value
            ChangedDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task TrackStatusChanges(Employee existing, Employee updated, string changedBy)
    {
        var changes = new List<EmployeeStatusHistory>();

        // Define fields to track using ValueTuples
        var fieldsToTrack = new[]
        {
        (Field: "Probation", Old: existing.Probation.ToString().ToLower(), New: updated.Probation.ToString().ToLower()),
        (Field: "ProbationPeriod", Old: existing.ProbationPeriod?.ToString() ?? "", New: updated.ProbationPeriod?.ToString() ?? ""),
        (Field: "ProbationEndDate", Old: existing.ProbationEndDate?.ToString("yyyy-MM-dd") ?? "", New: updated.ProbationEndDate?.ToString("yyyy-MM-dd") ?? ""),
        (Field: "ReviewedBy", Old: existing.ReviewedBy ?? "", New: updated.ReviewedBy ?? ""),
        (Field: "Block", Old: existing.Block.ToString().ToLower(), New: updated.Block.ToString().ToLower()),
        (Field: "BlockUntil", Old: existing.BlockUntil ?? "", New: updated.BlockUntil ?? ""),
        (Field: "BlockReason", Old: existing.BlockReason ?? "", New: updated.BlockReason ?? ""),
        (Field: "BlockRemark", Old: existing.BlockRemark ?? "", New: updated.BlockRemark ?? ""),
        (Field: "Resigned", Old: existing.Resigned.ToString().ToLower(), New: updated.Resigned.ToString().ToLower()),
        (Field: "ResignedDate", Old: existing.ResignedDate?.ToString("yyyy-MM-dd") ?? "", New: updated.ResignedDate?.ToString("yyyy-MM-dd") ?? ""),
        (Field: "ExitType", Old: existing.ExitType ?? "", New: updated.ExitType ?? ""),
        (Field: "ExitReason", Old: existing.ExitReason ?? "", New: updated.ExitReason ?? ""),
        (Field: "ExitRemark", Old: existing.ExitRemark ?? "", New: updated.ExitRemark ?? ""),
        (Field: "BlockAttendance", Old: existing.BlockAttendance.ToString().ToLower(), New: updated.BlockAttendance.ToString().ToLower())
    };

        foreach (var field in fieldsToTrack)
        {
            if (field.Old != field.New)
            {
                changes.Add(new EmployeeStatusHistory
                {
                    EmployeeId = existing.Id,
                    FieldName = field.Field,
                    OldValue = field.Old,
                    NewValue = field.New,
                    ChangedBy = changedBy,
                    ChangedDate = DateTime.UtcNow
                });
            }
        }

        if (changes.Any())
        {
            _context.EmployeeStatusHistory.AddRange(changes);
            await _context.SaveChangesAsync();
        }
    }

    private async Task TrackBankDetailsChanges(Employee existing, Employee updated, string changedBy)
    {
        var bankFields = new[]
        {
            new { Field = "SalaryPayType", Old = existing.SalaryPayType, New = updated.SalaryPayType },
            new { Field = "AccountNumber", Old = existing.AccountNumber, New = updated.AccountNumber },
            new { Field = "BankAccountName", Old = existing.BankAccountName, New = updated.BankAccountName },
            new { Field = "BankCode", Old = existing.BankCode, New = updated.BankCode },
            new { Field = "BranchCode", Old = existing.BranchCode, New = updated.BranchCode }
        };

        bool hasChanges = bankFields.Any(f => f.Old != f.New);

        if (hasChanges)
        {
            _context.BankDetailsHistory.Add(new BankDetailsHistory
            {
                EmployeeId = existing.Id,
                SalaryPayType = updated.SalaryPayType,
                AccountNumber = updated.AccountNumber,
                BankAccountName = updated.BankAccountName,
                BankCode = updated.BankCode,
                BankName = updated.BankName,
                BranchCode = updated.BranchCode,
                BranchName = updated.BranchName,
                ChangedBy = changedBy,
                ChangedDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    private void UpdateEmployeeProperties(Employee existing, Employee updated)
    {
        // Personal Data
        existing.Title = updated.Title;
        existing.Initial = updated.Initial;
        existing.FirstName = updated.FirstName;
        existing.LastName = updated.LastName;
        existing.SystemName = updated.SystemName;
        existing.NIC = updated.NIC;
        existing.DOB = updated.DOB;
        existing.Gender = updated.Gender;
        existing.MaritalStatus = updated.MaritalStatus;
        existing.BloodGroup = updated.BloodGroup;
        existing.DrivingLicense = updated.DrivingLicense;
        existing.Religion = updated.Religion;
        existing.Nationality = updated.Nationality;
        existing.Race = updated.Race;
        existing.Mobile = updated.Mobile;
        existing.LandPhone = updated.LandPhone;
        existing.ContactNo = updated.ContactNo;
        existing.ResidentialAddress = updated.ResidentialAddress;
        existing.PermanentAddress = updated.PermanentAddress;
        existing.ProfileImage = updated.ProfileImage;

        // Employment Data
        existing.CompanyId = updated.CompanyId;
        existing.DesignationId = updated.DesignationId;
        existing.DepartmentId = updated.DepartmentId;
        existing.ShiftBlockId = updated.ShiftBlockId;
        existing.DateOfAppointment = updated.DateOfAppointment;
        existing.AttendanceId = updated.AttendanceId;
        existing.EPFPay = updated.EPFPay;
        existing.EPFNo = updated.EPFNo;

        // Salary Data
        existing.BasicSalary = updated.BasicSalary;
        existing.BudgetaryAllowance = updated.BudgetaryAllowance;
        existing.BudgetaryAllowance2 = updated.BudgetaryAllowance2;
        existing.AttendanceAllowance = updated.AttendanceAllowance;
        existing.FixedAllowance = updated.FixedAllowance;
        existing.MealAllowance = updated.MealAllowance;
        existing.SpecialAllowance = updated.SpecialAllowance;
        existing.TransportAllowance = updated.TransportAllowance;
        existing.AccommodationAllowance = updated.AccommodationAllowance;
        existing.FuelAllowance = updated.FuelAllowance;
        existing.CostOfLivingAllowance = updated.CostOfLivingAllowance;
        existing.PerformanceAllowance = updated.PerformanceAllowance;
        existing.HealthAllowance = updated.HealthAllowance;

        // Bank Details
        existing.SalaryPayType = updated.SalaryPayType;
        existing.AccountNumber = updated.AccountNumber;
        existing.BankAccountName = updated.BankAccountName;
        existing.BankCode = updated.BankCode;
        existing.BankName = updated.BankName;
        existing.BranchCode = updated.BranchCode;
        existing.BranchName = updated.BranchName;

        // Status
        existing.Probation = updated.Probation;
        existing.ProbationPeriod = updated.ProbationPeriod;
        existing.ProbationEndDate = updated.ProbationEndDate;
        existing.ReviewedBy = updated.ReviewedBy;
        existing.Block = updated.Block;
        existing.BlockUntil = updated.BlockUntil;
        existing.BlockReason = updated.BlockReason;
        existing.BlockRemark = updated.BlockRemark;
        existing.Resigned = updated.Resigned;
        existing.ResignedDate = updated.ResignedDate;
        existing.ExitType = updated.ExitType;
        existing.ExitReason = updated.ExitReason;
        existing.ExitRemark = updated.ExitRemark;
        existing.BlockAttendance = updated.BlockAttendance;

        // Emergency Contacts
        existing.Keen1ContactName = updated.Keen1ContactName;
        existing.Keen1ContactNumber = updated.Keen1ContactNumber;
        existing.Keen1Relationship = updated.Keen1Relationship;
        existing.Keen1Address = updated.Keen1Address;
        existing.Keen1Position = updated.Keen1Position;
        existing.Keen1WorkPlace = updated.Keen1WorkPlace;
        existing.Keen1WorkPlaceContact = updated.Keen1WorkPlaceContact;
        existing.Keen2ContactName = updated.Keen2ContactName;
        existing.Keen2ContactNumber = updated.Keen2ContactNumber;
        existing.Keen2Relationship = updated.Keen2Relationship;
        existing.Keen2Address = updated.Keen2Address;
        existing.Keen2Position = updated.Keen2Position;
        existing.Keen2WorkPlace = updated.Keen2WorkPlace;
        existing.Keen2WorkPlaceContact = updated.Keen2WorkPlaceContact;

        existing.ModifiedBy = updated.ModifiedBy;
    }

    private void UpdateQualifications(Employee existing, List<Qualification> updatedQualifications)
    {
        // Remove qualifications not in updated list
        var qualificationsToRemove = existing.Qualifications
            .Where(eq => !updatedQualifications.Any(uq => uq.Id == eq.Id))
            .ToList();

        foreach (var qual in qualificationsToRemove)
        {
            _context.Qualifications.Remove(qual);
        }

        // Update or add qualifications
        foreach (var updatedQual in updatedQualifications)
        {
            var existingQual = existing.Qualifications.FirstOrDefault(q => q.Id == updatedQual.Id);
            if (existingQual != null)
            {
                // Sanitize and update existing qualification
                SanitizeQualificationData(updatedQual);

                existingQual.QualificationName = updatedQual.QualificationName;
                existingQual.InstituteName = updatedQual.InstituteName;
                existingQual.Year = updatedQual.Year;
                existingQual.Month = updatedQual.Month;
                existingQual.Description = updatedQual.Description;
            }
            else
            {
                // Sanitize and add new qualification
                SanitizeQualificationData(updatedQual);
                updatedQual.EmployeeId = existing.Id;
                existing.Qualifications.Add(updatedQual);
            }
        }
    }

    private void UpdateWorkExperiences(Employee existing, List<WorkExperience> updatedExperiences)
    {
        // Remove experiences not in updated list
        var experiencesToRemove = existing.WorkExperiences
            .Where(ew => !updatedExperiences.Any(uw => uw.Id == ew.Id))
            .ToList();

        foreach (var exp in experiencesToRemove)
        {
            _context.WorkExperiences.Remove(exp);
        }

        // Update or add experiences
        foreach (var updatedExp in updatedExperiences)
        {
            var existingExp = existing.WorkExperiences.FirstOrDefault(e => e.Id == updatedExp.Id);
            if (existingExp != null)
            {
                // Sanitize and update existing experience
                SanitizeWorkExperienceData(updatedExp);

                existingExp.Position = updatedExp.Position;
                existingExp.Company = updatedExp.Company;
                existingExp.FromYear = updatedExp.FromYear;
                existingExp.FromMonth = updatedExp.FromMonth;
                existingExp.ToYear = updatedExp.ToYear;
                existingExp.ToMonth = updatedExp.ToMonth;
                existingExp.Description = updatedExp.Description;
            }
            else
            {
                // Sanitize and add new experience
                SanitizeWorkExperienceData(updatedExp);
                updatedExp.EmployeeId = existing.Id;
                existing.WorkExperiences.Add(updatedExp);
            }
        }
    }


    private void ProcessQualificationsForNewEmployee(Employee employee)
    {
        if (employee.Qualifications != null && employee.Qualifications.Any())
        {
            foreach (var qualification in employee.Qualifications)
            {
                // Ensure ID is properly handled for int/long conversion
                qualification.Id = 0; // Reset to default

                SanitizeQualificationData(qualification);
                qualification.CreatedDate = DateTime.UtcNow;
            }
        }
    }

    private void ProcessWorkExperiencesForNewEmployee(Employee employee)
    {
        if (employee.WorkExperiences != null && employee.WorkExperiences.Any())
        {
            foreach (var experience in employee.WorkExperiences)
            {
                // Ensure ID is properly handled for int/long conversion
                experience.Id = 0; // Reset to default

                // Handle ToYear data type if needed
                // If your database expects nvarchar but model has int, convert:
                // experience.ToYear = experience.ToYear.ToString();

                SanitizeWorkExperienceData(experience);
                experience.CreatedDate = DateTime.UtcNow;
            }
        }
    }
    #endregion
}


