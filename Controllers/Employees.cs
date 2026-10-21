// Controllers/EmployeesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TRIVORA_API.Data;
using TRIVORA_API.Models;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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

    // ============================================================
    // SEARCH
    // ============================================================
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<List<EmployeeSearchResult>>>> SearchEmployees([FromQuery] string term, [FromQuery] int? companyId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
                return Ok(new ApiResponse<List<EmployeeSearchResult>>
                {
                    Success = false,
                    Message = "Search term must be at least 2 characters long"
                });

            var query = _context.Employees.Where(e => e.IsActive);

            if (companyId.HasValue && companyId > 0)
                query = query.Where(e => e.CompanyId == companyId.Value);

            var searchTerm = $"%{term}%";

            // ✅ EPFNo is now int in the model
            int? epfAsInt = int.TryParse(term, out int parsedEpf) ? parsedEpf : (int?)null;

            var employees = await query
                .Where(e =>
                    EF.Functions.Like(e.FirstName, searchTerm) ||
                    EF.Functions.Like(e.LastName, searchTerm) ||
                    EF.Functions.Like(e.SystemName, searchTerm) ||
                    (e.EmployeeNo != null && EF.Functions.Like(e.EmployeeNo, searchTerm)) ||
                    // ✅ AttendanceId is now string? in the model
                    (e.AttendanceId != null && EF.Functions.Like(e.AttendanceId, searchTerm)) ||
                    (e.NIC != null && EF.Functions.Like(e.NIC, searchTerm)) ||
                    (e.Mobile != null && EF.Functions.Like(e.Mobile, searchTerm)) ||
                    // ✅ EPFNo exact match using int parse
                    (epfAsInt.HasValue && e.EPFNo == epfAsInt.Value) ||
                    (e.FirstName + " " + e.LastName).Contains(term)
                )
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Take(50)
                .Select(e => new EmployeeSearchResult
                {
                    Id = e.Id,
                    EmployeeNo = e.EmployeeNo ?? string.Empty,
                    EPFNo = e.EPFNo.ToString(),                       // int → string
                    AttendanceId = e.AttendanceId ?? string.Empty,    // already string?
                    FirstName = e.FirstName ?? string.Empty,
                    LastName = e.LastName ?? string.Empty,
                    SystemName = e.SystemName ?? string.Empty,
                    NIC = e.NIC ?? string.Empty,
                    Mobile = e.Mobile ?? string.Empty,
                    CompanyId = e.CompanyId,
                    DesignationId = e.DesignationId ?? 0,
                    DepartmentId = e.DepartmentId ?? 0,
                    FullName = (e.FirstName ?? "") + " " + (e.LastName ?? ""),
                    DisplayText = $"{e.FirstName} {e.LastName} ({e.EPFNo}) - {e.SystemName}"
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<EmployeeSearchResult>>
            {
                Success = true,
                Data = employees,
                Message = employees.Count > 0 ? $"Found {employees.Count} employees" : "No employees found"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching employees");
            return StatusCode(500, new ApiResponse<List<EmployeeSearchResult>>
            {
                Success = false,
                Message = $"Internal server error: {ex.Message}"
            });
        }
    }

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
        public int DesignationId { get; set; }
        public int DepartmentId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
    }

    // ============================================================
    // GET BY ID
    // ============================================================
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
                return NotFound(new ApiResponse<Employee> { Success = false, Message = "Employee not found" });

            return Ok(new ApiResponse<Employee> { Success = true, Data = employee });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Internal server error" });
        }
    }

    // ============================================================
    // GET BY IDENTIFIER (EPFNo, EmployeeNo, AttendanceId)
    // ============================================================
    [HttpGet("byidentifier/{identifier}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployeeByIdentifier(string identifier)
    {
        try
        {
            // ✅ EPFNo is int now — parse the identifier
            int? epfAsInt = int.TryParse(identifier, out int parsedEpf) ? parsedEpf : (int?)null;

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.IsActive && (
                    (epfAsInt.HasValue && e.EPFNo == epfAsInt.Value) ||
                    e.EmployeeNo == identifier ||
                    e.AttendanceId == identifier   // ✅ string comparison
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

    // ============================================================
    // GET BY IDENTIFIER + COMPANY
    // ============================================================
    [HttpGet("byidentifier/{identifier}/{companyId}")]
    public async Task<ActionResult<ApiResponse<Employee>>> GetEmployeeByIdentifierAndCompany(string identifier, int companyId)
    {
        try
        {
            _logger.LogInformation($"Searching employee with identifier: {identifier} in company: {companyId}");

            int? epfAsInt = int.TryParse(identifier, out int parsedEpf) ? parsedEpf : (int?)null;

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .FirstOrDefaultAsync(e => e.IsActive && e.CompanyId == companyId && (
                    (epfAsInt.HasValue && e.EPFNo == epfAsInt.Value) ||
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

    // ============================================================
    // CREATE
    // ============================================================
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Employee>>> CreateEmployee(Employee employee)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        employee.CreatedBy = GetCurrentUser();
        employee.ModifiedBy = GetCurrentUser();
        try
        {
            SanitizeEmployeeData(employee);

            var validationResult = ValidateEmployee(employee);
            if (!validationResult.IsValid)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = validationResult.ErrorMessage });

            // EPFNo is int now — comparing to 0
            if (employee.EPFNo != 0)
            {
                if (await _context.Employees.AnyAsync(e => e.EPFNo == employee.EPFNo && e.CompanyId == employee.CompanyId && e.IsActive))
                    return Conflict(new ApiResponse<Employee> { Success = false, Message = "Employee with this EPF number already exists in this company" });
            }
            if (!string.IsNullOrWhiteSpace(employee.EmployeeNo) && employee.EmployeeNo != "0")
            {
                if (await _context.Employees.AnyAsync(e => e.EmployeeNo == employee.EmployeeNo && e.CompanyId == employee.CompanyId && e.IsActive))
                    return Conflict(new ApiResponse<Employee> { Success = false, Message = "Employee with this employee number already exists in this company" });
            }

            var qualifications = employee.Qualifications?.ToList();
            var workExperiences = employee.WorkExperiences?.ToList();
            var leaveApprovers = employee.LeaveCustomApprovers?.ToList();
            var financeApprovers = employee.FinanceCustomApprovers?.ToList();

            employee.Qualifications = new List<Qualification>();
            employee.WorkExperiences = new List<WorkExperience>();
            employee.LeaveCustomApprovers = new List<LeaveCustomApprover>();
            employee.FinanceCustomApprovers = new List<FinanceCustomApprovers>();

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            if (qualifications?.Any() == true)
            {
                foreach (var qualification in qualifications)
                {
                    qualification.Id = 0;
                    qualification.EmployeeId = employee.Id;
                    SanitizeQualificationData(qualification);
                    _context.Qualifications.Add(qualification);
                }
                await _context.SaveChangesAsync();
            }

            if (workExperiences?.Any() == true)
            {
                foreach (var experience in workExperiences)
                {
                    experience.Id = 0;
                    experience.EmployeeId = employee.Id;
                    SanitizeWorkExperienceData(experience);
                    _context.WorkExperiences.Add(experience);
                }
                await _context.SaveChangesAsync();
            }

            if (leaveApprovers?.Any() == true)
            {
                foreach (var approver in leaveApprovers)
                {
                    approver.Id = 0;
                    approver.EmployeeId = employee.Id;
                    approver.CompanyId = employee.CompanyId;
                    approver.CreatedBy = employee.CreatedBy;
                    _context.LeaveCustomApprovers.Add(approver);
                }
                await _context.SaveChangesAsync();
            }

            if (financeApprovers?.Any() == true)
            {
                foreach (var approver in financeApprovers)
                {
                    approver.Id = 0;
                    approver.EmployeeId = employee.Id;
                    approver.CompanyId = employee.CompanyId;
                    approver.CreatedBy = employee.CreatedBy;
                    _context.FinanceCustomApprovers.Add(approver);
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            await LogBasicSalaryChange(employee.Id, null, employee.BasicSalary, employee.ModifiedBy);
            await LogEmployeeOperation(employee.Id, "CREATE", "API", employee);

            var createdEmployee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .Include(e => e.LeaveCustomApprovers)
                .Include(e => e.FinanceCustomApprovers)
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

    // ============================================================
    // UPDATE
    // ============================================================
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Employee>>> UpdateEmployee(int id, Employee employee)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        employee.ModifiedBy = GetCurrentUser();
        try
        {
            if (id != employee.Id)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = "ID mismatch" });

            var existingEmployee = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .Include(e => e.LeaveCustomApprovers)
                .Include(e => e.FinanceCustomApprovers)
                .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

            if (existingEmployee == null)
                return NotFound(new ApiResponse<Employee> { Success = false, Message = "Employee not found" });

            var oldEmployeeData = CaptureEmployeeData(existingEmployee);

            SanitizeEmployeeData(employee);

            var validationResult = ValidateEmployee(employee);
            if (!validationResult.IsValid)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = validationResult.ErrorMessage });

            if (await _context.Employees.AnyAsync(e => e.EPFNo == employee.EPFNo && e.CompanyId == employee.CompanyId && e.Id != id && e.IsActive))
                return Conflict(new ApiResponse<Employee> { Success = false, Message = "Another employee with this EPF number already exists in this company" });

            if (!string.IsNullOrWhiteSpace(employee.EmployeeNo) && employee.EmployeeNo != "0")
            {
                if (await _context.Employees.AnyAsync(e => e.EmployeeNo == employee.EmployeeNo && e.CompanyId == employee.CompanyId && e.Id != id && e.IsActive))
                    return Conflict(new ApiResponse<Employee> { Success = false, Message = "Another employee with this employee number already exists in this company" });
            }

            await TrackChangesForAudit(existingEmployee, employee);

            employee.ModifiedDate = DateTime.UtcNow;
            employee.IsActive = true;

            var qualifications = employee.Qualifications?.ToList();
            var workExperiences = employee.WorkExperiences?.ToList();
            var leaveApprovers = employee.LeaveCustomApprovers?.ToList();
            var financeApprovers = employee.FinanceCustomApprovers?.ToList();

            employee.Qualifications = null;
            employee.WorkExperiences = null;
            employee.LeaveCustomApprovers = null;
            employee.FinanceCustomApprovers = null;

            _context.Employees.Update(employee);
            await _context.SaveChangesAsync();

            if (qualifications?.Any() == true)
            {
                var existingQuals = await _context.Qualifications.Where(q => q.EmployeeId == id).ToListAsync();
                _context.Qualifications.RemoveRange(existingQuals);
                foreach (var qual in qualifications)
                {
                    qual.Id = 0;
                    qual.EmployeeId = id;
                    SanitizeQualificationData(qual);
                    _context.Qualifications.Add(qual);
                }
                await _context.SaveChangesAsync();
            }

            if (workExperiences?.Any() == true)
            {
                var existingExps = await _context.WorkExperiences.Where(w => w.EmployeeId == id).ToListAsync();
                _context.WorkExperiences.RemoveRange(existingExps);
                foreach (var exp in workExperiences)
                {
                    exp.Id = 0;
                    exp.EmployeeId = id;
                    SanitizeWorkExperienceData(exp);
                    _context.WorkExperiences.Add(exp);
                }
                await _context.SaveChangesAsync();
            }

            if (leaveApprovers?.Any() == true)
            {
                var existingLeaveApprovers = await _context.LeaveCustomApprovers.Where(l => l.EmployeeId == id).ToListAsync();
                _context.LeaveCustomApprovers.RemoveRange(existingLeaveApprovers);
                foreach (var approver in leaveApprovers)
                {
                    approver.Id = 0;
                    approver.EmployeeId = id;
                    approver.CompanyId = employee.CompanyId;
                    approver.CreatedBy = employee.ModifiedBy;
                    _context.LeaveCustomApprovers.Add(approver);
                }
                await _context.SaveChangesAsync();
            }

            if (financeApprovers?.Any() == true)
            {
                var existingFinanceApprovers = await _context.FinanceCustomApprovers.Where(f => f.EmployeeId == id).ToListAsync();
                _context.FinanceCustomApprovers.RemoveRange(existingFinanceApprovers);
                foreach (var approver in financeApprovers)
                {
                    approver.Id = 0;
                    approver.EmployeeId = id;
                    approver.CompanyId = employee.CompanyId;
                    approver.CreatedBy = employee.ModifiedBy;
                    _context.FinanceCustomApprovers.Add(approver);
                }
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            var auditData = new
            {
                OldValues = oldEmployeeData,
                NewValues = CaptureEmployeeData(employee)
            };
            await LogEmployeeOperation(id, "UPDATE", "API", auditData);

            var updatedEmployee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .Include(e => e.LeaveCustomApprovers)
                .Include(e => e.FinanceCustomApprovers)
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

    // ============================================================
    // DELETE
    // ============================================================
    [HttpDelete("byempno/{empNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployeeByEmpNoAndCompany(string empNo, int companyId)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeNo == empNo && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Employee with Employee No {empNo} not found in company"
                });

            var employeeData = CaptureEmployeeData(employee);
            string modifiedBy = GetCurrentUser();

            employee.IsActive = false;
            employee.ModifiedDate = DateTime.UtcNow;
            employee.ModifiedBy = modifiedBy;

            await _context.SaveChangesAsync();
            await LogEmployeeOperation(employee.Id, "DELETE", "API", employeeData);

            return Ok(new ApiResponse<bool> { Success = true, Message = "Employee deleted successfully", Data = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee by Employee No and company");
            return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
        }
    }

    [HttpDelete("byepf/{epfNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEmployeeByEPFAndCompany(string epfNo, int companyId)
    {
        try
        {
            // ✅ EPFNo is int now
            if (!int.TryParse(epfNo, out int epfInt))
                return BadRequest(new ApiResponse<bool> { Success = false, Message = "Invalid EPF number format" });

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EPFNo == epfInt && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"Employee with EPF {epfNo} not found in company"
                });

            var employeeData = CaptureEmployeeData(employee);
            string modifiedBy = GetCurrentUser();

            employee.IsActive = false;
            employee.ModifiedDate = DateTime.UtcNow;
            employee.ModifiedBy = modifiedBy;

            await _context.SaveChangesAsync();
            await LogEmployeeOperation(employee.Id, "DELETE", "API", employeeData);

            return Ok(new ApiResponse<bool> { Success = true, Message = "Employee deleted successfully", Data = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee by EPF and company");
            return StatusCode(500, new ApiResponse<bool> { Success = false, Message = "Internal server error" });
        }
    }

    // ============================================================
    // GET BY EPF + COMPANY (DTO)
    // ============================================================
    [HttpGet("byepf/{epfNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<EmployeeResponseDto>>> GetEmployeeByEPFAndCompanyWithDto(string epfNo, int companyId)
    {
        try
        {
            if (!int.TryParse(epfNo, out int epfInt))
                return BadRequest(new ApiResponse<EmployeeResponseDto> { Success = false, Message = "Invalid EPF number format" });

            _logger.LogInformation($"Searching employee with EPF: {epfNo} in company: {companyId}");

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .Include(e => e.LeaveCustomApprovers).ThenInclude(la => la.ApproverEmployee)
                .Include(e => e.FinanceCustomApprovers).ThenInclude(fa => fa.ApproverEmployee)
                .FirstOrDefaultAsync(e => e.EPFNo == epfInt && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
            {
                _logger.LogWarning($"Employee not found with EPF: {epfNo} in company: {companyId}");
                return NotFound(new ApiResponse<EmployeeResponseDto>
                {
                    Success = false,
                    Message = $"Employee with EPF {epfNo} not found in selected company"
                });
            }

            var employeeDto = MapEmployeeToDto(employee);
            return Ok(new ApiResponse<EmployeeResponseDto> { Success = true, Data = employeeDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting employee with EPF: {epfNo} in company: {companyId}");
            return StatusCode(500, new ApiResponse<EmployeeResponseDto> { Success = false, Message = "Internal server error" });
        }
    }

    // ============================================================
    // GET BY EMPNO + COMPANY (DTO)
    // ============================================================
    [HttpGet("byempno/{empNo}/{companyId}")]
    public async Task<ActionResult<ApiResponse<EmployeeResponseDto>>> GetEmployeeByEmpNoAndCompanyWithDto(string empNo, int companyId)
    {
        try
        {
            _logger.LogInformation($"Searching employee with Employee No: {empNo} in company: {companyId}");

            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .Include(e => e.LeaveCustomApprovers).ThenInclude(la => la.ApproverEmployee)
                .Include(e => e.FinanceCustomApprovers).ThenInclude(fa => fa.ApproverEmployee)
                .FirstOrDefaultAsync(e => e.EmployeeNo == empNo && e.CompanyId == companyId && e.IsActive);

            if (employee == null)
            {
                _logger.LogWarning($"Employee not found with Employee No: {empNo} in company: {companyId}");
                return NotFound(new ApiResponse<EmployeeResponseDto>
                {
                    Success = false,
                    Message = $"Employee with Employee No {empNo} not found in selected company"
                });
            }

            var employeeDto = MapEmployeeToDto(employee);
            return Ok(new ApiResponse<EmployeeResponseDto> { Success = true, Data = employeeDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting employee with Employee No: {empNo} in company: {companyId}");
            return StatusCode(500, new ApiResponse<EmployeeResponseDto> { Success = false, Message = "Internal server error" });
        }
    }

    // ============================================================
    // GET BY ID (DTO)
    // ============================================================
    [HttpGet("{id}/dto")]
    public async Task<ActionResult<ApiResponse<EmployeeResponseDto>>> GetEmployeeWithDto(int id)
    {
        try
        {
            var employee = await _context.Employees
                .Include(e => e.Qualifications)
                .Include(e => e.WorkExperiences)
                .Include(e => e.LeaveCustomApprovers).ThenInclude(la => la.ApproverEmployee)
                .Include(e => e.FinanceCustomApprovers).ThenInclude(fa => fa.ApproverEmployee)
                .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

            if (employee == null)
                return NotFound(new ApiResponse<EmployeeResponseDto> { Success = false, Message = "Employee not found" });

            var employeeDto = MapEmployeeToDto(employee);
            return Ok(new ApiResponse<EmployeeResponseDto> { Success = true, Data = employeeDto });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting employee with DTO");
            return StatusCode(500, new ApiResponse<EmployeeResponseDto> { Success = false, Message = "Internal server error" });
        }
    }

    // ============================================================
    // GET APPROVERS
    // ============================================================
    [HttpGet("approvers/{companyId}")]
    public async Task<ActionResult<ApiResponse<List<EmployeeApproverDto>>>> GetApprovers(int companyId)
    {
        try
        {
            var approvers = await _context.Employees
                .Where(e => e.IsActive &&
                        e.CompanyId == companyId &&
                        (e.RoleType == "Section Head" ||
                         e.RoleType == "HR Admin" ||
                         e.RoleType == "HR Assistant" ||
                         e.RoleType == "Finance Admin" ||
                         e.RoleType == "Finance Assistant" ||
                         e.RoleType == "Super Admin"))
                .OrderBy(e => e.FirstName + " " + e.LastName)
                .Select(e => new EmployeeApproverDto
                {
                    Id = e.Id,
                    EmployeeNo = e.EmployeeNo ?? string.Empty,
                    EPFNo = e.EPFNo.ToString(),   // ✅ int → string
                    FullName = e.FirstName + " " + e.LastName,
                    RoleType = e.RoleType ?? string.Empty,
                    SystemName = e.SystemName ?? string.Empty
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<EmployeeApproverDto>>
            {
                Success = true,
                Data = approvers,
                Message = $"Found {approvers.Count} approvers"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting approvers");
            return StatusCode(500, new ApiResponse<List<EmployeeApproverDto>> { Success = false, Message = "Internal server error" });
        }
    }

    public class EmployeeApproverDto
    {
        public int Id { get; set; }
        public string EmployeeNo { get; set; } = string.Empty;
        public string EPFNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleType { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
    }

    // ============================================================
    // PRIVATE HELPERS
    // ============================================================
    private void SanitizeQualificationData(Qualification qualification)
    {
        qualification.QualificationName = qualification.QualificationName ?? string.Empty;
        qualification.InstituteName = qualification.InstituteName ?? string.Empty;
        qualification.Month = qualification.Month ?? string.Empty;
        qualification.Description = qualification.Description ?? string.Empty;

        if (qualification.CreatedDate == default(DateTime))
            qualification.CreatedDate = DateTime.UtcNow;
    }

    private string GetCurrentUser()
    {
        try
        {
            if (HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                var userName = HttpContext.User.Identity.Name;
                if (string.IsNullOrEmpty(userName))
                    userName = HttpContext.User.FindFirst("name")?.Value;
                if (string.IsNullOrEmpty(userName))
                    userName = HttpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
                if (string.IsNullOrEmpty(userName))
                    userName = HttpContext.User.FindFirst("email")?.Value;
                if (string.IsNullOrEmpty(userName))
                    userName = HttpContext.User.FindFirst("preferred_username")?.Value;
                if (string.IsNullOrEmpty(userName))
                    userName = HttpContext.User.FindFirst("sub")?.Value;

                if (!string.IsNullOrEmpty(userName))
                    return userName;
            }

            if (Request?.Headers?.ContainsKey("Authorization") == true)
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    var token = authHeader.Substring(7);
                    try
                    {
                        var handler = new JwtSecurityTokenHandler();
                        var jsonToken = handler.ReadJwtToken(token);

                        var userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                        if (string.IsNullOrEmpty(userName))
                            userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")?.Value;
                        if (string.IsNullOrEmpty(userName))
                            userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                        if (string.IsNullOrEmpty(userName))
                            userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
                        if (string.IsNullOrEmpty(userName))
                            userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                        if (string.IsNullOrEmpty(userName))
                            userName = jsonToken.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;

                        if (!string.IsNullOrEmpty(userName))
                        {
                            _logger.LogInformation($"Extracted user '{userName}' from JWT token");
                            return userName;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to decode JWT token: {ex.Message}");
                    }
                }
            }

            if (Request?.Headers?.ContainsKey("X-User-Id") == true)
                return Request.Headers["X-User-Id"].ToString();
            if (Request?.Headers?.ContainsKey("X-Username") == true)
                return Request.Headers["X-Username"].ToString();

            _logger.LogWarning("User is not authenticated.");
            return "System";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return "System";
        }
    }

    private async Task LogEmployeeOperation(int employeeId, string operation, string source, object data)
    {
        try
        {
            if (employeeId <= 0)
            {
                _logger.LogWarning($"Cannot log operation for employee with invalid ID: {employeeId}");
                return;
            }

            var log = new EmployeeAuditLog
            {
                EmployeeId = employeeId,
                Operation = operation,
                Source = source,
                PerformedBy = User.Identity?.Name ?? "System",
                PerformedDate = DateTime.UtcNow,
                RowData = JsonSerializer.Serialize(data)
            };

            _context.EmployeeAuditLogs.Add(log);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Logged {operation} for employee {employeeId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error logging employee operation for employee {employeeId}");
        }
    }

    private Dictionary<string, object> CaptureEmployeeData(Employee employee)
    {
        return new Dictionary<string, object>
        {
            ["Id"] = employee.Id,
            ["EmployeeNo"] = employee.EmployeeNo ?? string.Empty,
            ["EPFNo"] = employee.EPFNo,                              // int
            ["AttendanceId"] = employee.AttendanceId ?? string.Empty, // string?
            ["FirstName"] = employee.FirstName ?? string.Empty,
            ["LastName"] = employee.LastName ?? string.Empty,
            ["SystemName"] = employee.SystemName ?? string.Empty,
            ["NIC"] = employee.NIC ?? string.Empty,
            ["DesignationId"] = employee.DesignationId,
            ["DepartmentId"] = employee.DepartmentId,
            ["ShiftBlockId"] = employee.ShiftBlockId,
            ["BasicSalary"] = employee.BasicSalary,
            ["IsActive"] = employee.IsActive,
            ["RoleType"] = employee.RoleType ?? string.Empty,
            ["Mobile"] = employee.Mobile ?? string.Empty
        };
    }

    private EmployeeResponseDto MapEmployeeToDto(Employee employee)
    {
        if (employee == null) return null!;

        return new EmployeeResponseDto
        {
            Id = employee.Id,
            EmployeeNo = employee.EmployeeNo ?? string.Empty,
            EPFNo = employee.EPFNo.ToString(),                              // int → string
            AttendanceId = employee.AttendanceId ?? string.Empty,           // already string?
            CompanyId = employee.CompanyId,

            Title = employee.Title ?? string.Empty,
            Initial = employee.Initial ?? string.Empty,
            FirstName = employee.FirstName ?? string.Empty,
            LastName = employee.LastName ?? string.Empty,
            SystemName = employee.SystemName ?? string.Empty,
            NIC = employee.NIC ?? string.Empty,
            DOB = employee.DOB,
            Gender = employee.Gender ?? string.Empty,
            MaritalStatus = employee.MaritalStatus ?? string.Empty,
            BloodGroup = employee.BloodGroup ?? string.Empty,
            DrivingLicense = employee.DrivingLicense ?? string.Empty,
            Religion = employee.Religion ?? string.Empty,
            Nationality = employee.Nationality ?? string.Empty,
            Race = employee.Race ?? string.Empty,
            Mobile = employee.Mobile ?? string.Empty,
            LandPhone = employee.LandPhone ?? string.Empty,
            ContactNo = employee.ContactNo ?? string.Empty,
            ResidentialAddress = employee.ResidentialAddress ?? string.Empty,
            PermanentAddress = employee.PermanentAddress ?? string.Empty,

            OccupationNo = employee.OccupationNo,
            OccupationGrade = employee.OccupationGrade,
            RoleType = employee.RoleType ?? "Standard Employee",
            LeaveApproval = employee.LeaveApproval ?? "Super Admin",
            FinanceApproval = employee.FinanceApproval ?? "Super Admin",

            DesignationId = employee.DesignationId,
            DepartmentId = employee.DepartmentId,
            ShiftBlockId = employee.ShiftBlockId,
            DateOfAppointment = employee.DateOfAppointment,

            BasicSalary = employee.BasicSalary,
            BudgetaryAllowance = employee.BudgetaryAllowance,
            BudgetaryAllowance2 = employee.BudgetaryAllowance2,
            AttendanceAllowance = employee.AttendanceAllowance,
            FixedAllowance = employee.FixedAllowance,
            MealAllowance = employee.MealAllowance,
            SpecialAllowance = employee.SpecialAllowance,
            TransportAllowance = employee.TransportAllowance,
            AccommodationAllowance = employee.AccommodationAllowance,
            FuelAllowance = employee.FuelAllowance,
            CostOfLivingAllowance = employee.CostOfLivingAllowance,
            PerformanceAllowance = employee.PerformanceAllowance,
            HealthAllowance = employee.HealthAllowance,

            EPFPay = employee.EPFPay,
            SalaryPayType = employee.SalaryPayType ?? "CashPay",
            AccountNumber = employee.AccountNumber ?? string.Empty,
            BankAccountName = employee.BankAccountName ?? string.Empty,
            BankCode = employee.BankCode ?? string.Empty,
            BankName = employee.BankName ?? string.Empty,
            BranchCode = employee.BranchCode ?? string.Empty,
            BranchName = employee.BranchName ?? string.Empty,

            Probation = employee.Probation,
            ProbationPeriod = employee.ProbationPeriod,
            ProbationEndDate = employee.ProbationEndDate,
            ReviewedBy = employee.ReviewedBy ?? string.Empty,

            Block = employee.Block,
            BlockUntil = employee.BlockUntil ?? string.Empty,
            BlockReason = employee.BlockReason ?? string.Empty,
            BlockRemark = employee.BlockRemark ?? string.Empty,

            Resigned = employee.Resigned,
            ResignedDate = employee.ResignedDate,
            ExitType = employee.ExitType ?? string.Empty,
            ExitReason = employee.ExitReason ?? string.Empty,
            ExitRemark = employee.ExitRemark ?? string.Empty,
            BlockAttendance = employee.BlockAttendance,

            Keen1ContactName = employee.Keen1ContactName ?? string.Empty,
            Keen1ContactNumber = employee.Keen1ContactNumber ?? string.Empty,
            Keen1Relationship = employee.Keen1Relationship ?? string.Empty,
            Keen1Address = employee.Keen1Address ?? string.Empty,
            Keen1Position = employee.Keen1Position ?? string.Empty,
            Keen1WorkPlace = employee.Keen1WorkPlace ?? string.Empty,
            Keen1WorkPlaceContact = employee.Keen1WorkPlaceContact ?? string.Empty,

            Keen2ContactName = employee.Keen2ContactName ?? string.Empty,
            Keen2ContactNumber = employee.Keen2ContactNumber ?? string.Empty,
            Keen2Relationship = employee.Keen2Relationship ?? string.Empty,
            Keen2Address = employee.Keen2Address ?? string.Empty,
            Keen2Position = employee.Keen2Position ?? string.Empty,
            Keen2WorkPlace = employee.Keen2WorkPlace ?? string.Empty,
            Keen2WorkPlaceContact = employee.Keen2WorkPlaceContact ?? string.Empty,

            ProfileImage = employee.ProfileImage ?? string.Empty,
            IsActive = employee.IsActive,

            CreatedBy = employee.CreatedBy ?? string.Empty,
            CreatedDate = employee.CreatedDate,
            ModifiedBy = employee.ModifiedBy ?? string.Empty,
            ModifiedDate = employee.ModifiedDate,

            Qualifications = employee.Qualifications?.Select(q => new QualificationDto
            {
                Id = q.Id,
                QualificationName = q.QualificationName ?? string.Empty,
                InstituteName = q.InstituteName ?? string.Empty,
                Year = q.Year,
                Month = q.Month ?? string.Empty,
                Description = q.Description ?? string.Empty
            }).ToList() ?? new List<QualificationDto>(),

            WorkExperiences = employee.WorkExperiences?.Select(w => new WorkExperienceDto
            {
                Id = w.Id,
                Position = w.Position ?? string.Empty,
                Company = w.Company ?? string.Empty,
                FromYear = w.FromYear,
                FromMonth = w.FromMonth ?? string.Empty,
                ToYear = w.ToYear ?? string.Empty,
                ToMonth = w.ToMonth ?? string.Empty,
                Description = w.Description ?? string.Empty
            }).ToList() ?? new List<WorkExperienceDto>(),

            LeaveCustomApprovers = employee.LeaveCustomApprovers?.Select(l => new LeaveCustomApproverDto
            {
                Id = l.Id,
                EmployeeId = l.EmployeeId,
                ApproverEmployeeId = l.ApproverEmployeeId,
                CompanyId = l.CompanyId,
                CreatedBy = l.CreatedBy ?? string.Empty,
                CreatedDate = l.CreatedDate,
                IsActive = l.IsActive,
                ApproverEmployee = l.ApproverEmployee == null ? null : new ApproverEmployeeDto
                {
                    Id = l.ApproverEmployee.Id,
                    EmployeeNo = l.ApproverEmployee.EmployeeNo ?? string.Empty,
                    EPFNo = l.ApproverEmployee.EPFNo.ToString(),
                    FirstName = l.ApproverEmployee.FirstName ?? string.Empty,
                    LastName = l.ApproverEmployee.LastName ?? string.Empty,
                    SystemName = l.ApproverEmployee.SystemName ?? string.Empty
                }
            }).ToList() ?? new List<LeaveCustomApproverDto>(),

            FinanceCustomApprovers = employee.FinanceCustomApprovers?.Select(f => new FinanceCustomApproverDto
            {
                Id = f.Id,
                EmployeeId = f.EmployeeId,
                ApproverEmployeeId = f.ApproverEmployeeId,
                CompanyId = f.CompanyId,
                CreatedBy = f.CreatedBy ?? string.Empty,
                CreatedDate = (DateTime)f.CreatedDate,
                IsActive = (bool)f.IsActive,
                ApproverEmployee = f.ApproverEmployee == null ? null : new ApproverEmployeeDto
                {
                    Id = f.ApproverEmployee.Id,
                    EmployeeNo = f.ApproverEmployee.EmployeeNo ?? string.Empty,
                    EPFNo = f.ApproverEmployee.EPFNo.ToString(),
                    FirstName = f.ApproverEmployee.FirstName ?? string.Empty,
                    LastName = f.ApproverEmployee.LastName ?? string.Empty,
                    SystemName = f.ApproverEmployee.SystemName ?? string.Empty
                }
            }).ToList() ?? new List<FinanceCustomApproverDto>()
        };
    }

    private void SanitizeWorkExperienceData(WorkExperience workExperience)
    {
        workExperience.Position = workExperience.Position ?? string.Empty;
        workExperience.Company = workExperience.Company ?? string.Empty;
        workExperience.FromMonth = workExperience.FromMonth ?? string.Empty;
        workExperience.ToMonth = workExperience.ToMonth ?? string.Empty;
        workExperience.Description = workExperience.Description ?? string.Empty;

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
            if (employee.EPFNo <= 0)
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
            if (string.IsNullOrWhiteSpace(employee.EmployeeNo))
                employee.EmployeeNo = "0";

            if (employee.EPFNo < 0)
                employee.EPFNo = 0;
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

        if (employee.DOB.HasValue && employee.DOB.Value > DateTime.Today.AddYears(-16))
            return (false, "Employee must be at least 16 years old");

        if (employee.DateOfAppointment.HasValue && employee.DateOfAppointment.Value > DateTime.Today)
            return (false, "Date of appointment cannot be in the future");

        if (employee.Resigned && employee.ResignedDate.HasValue)
        {
            if (employee.ResignedDate.Value > DateTime.Today)
                return (false, "Resigned date cannot be in the future");
            if (employee.DateOfAppointment.HasValue && employee.ResignedDate.Value < employee.DateOfAppointment.Value)
                return (false, "Resigned date cannot be before appointment date");
        }

        return (true, null!);
    }
            // ===== Validation Helper: Weekly Day Off / Half Day Off =====
        
    private void SanitizeEmployeeData(Employee employee)
    {
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
        employee.AttendanceId = employee.AttendanceId ?? string.Empty;  // ✅ string?

        employee.SalaryPayType = employee.SalaryPayType ?? "CashPay";
        employee.AccountNumber = employee.AccountNumber ?? string.Empty;
        employee.BankAccountName = employee.BankAccountName ?? string.Empty;
        employee.BankCode = employee.BankCode ?? string.Empty;
        employee.BankName = employee.BankName ?? string.Empty;
        employee.BranchCode = employee.BranchCode ?? string.Empty;
        employee.BranchName = employee.BranchName ?? string.Empty;

        employee.ReviewedBy = employee.ReviewedBy ?? string.Empty;
        employee.BlockUntil = employee.BlockUntil ?? string.Empty;
        employee.BlockReason = employee.BlockReason ?? string.Empty;
        employee.BlockRemark = employee.BlockRemark ?? string.Empty;
        employee.ExitType = employee.ExitType ?? string.Empty;
        employee.ExitReason = employee.ExitReason ?? string.Empty;
        employee.ExitRemark = employee.ExitRemark ?? string.Empty;

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

        employee.CreatedBy = employee.CreatedBy ?? "System";
        employee.ModifiedBy = employee.ModifiedBy ?? "System";

        if (employee.CreatedDate == default(DateTime))
            employee.CreatedDate = DateTime.UtcNow;

        employee.ModifiedDate = DateTime.UtcNow;
        employee.IsActive = true;
    }

    private async Task TrackChangesForAudit(Employee existing, Employee updated)
    {
        var changedBy = updated.ModifiedBy ?? "System";

        if (existing.BasicSalary != updated.BasicSalary)
            await LogBasicSalaryChange(existing.Id, existing.BasicSalary, updated.BasicSalary, changedBy);

        await TrackStatusChanges(existing, updated, changedBy);
        await TrackBankDetailsChanges(existing, updated, changedBy);
    }

    private async Task LogBasicSalaryChange(int employeeId, decimal? oldSalary, decimal? newSalary, string changedBy)
    {
        var changedByValue = changedBy ?? "System";

        _context.BasicSalaryHistory.Add(new BasicSalaryHistory
        {
            EmployeeId = employeeId,
            OldBasicSalary = oldSalary,
            NewBasicSalary = newSalary,
            ChangedBy = changedByValue,
            ChangedDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

    private async Task TrackStatusChanges(Employee existing, Employee updated, string changedBy)
    {
        var changes = new List<EmployeeStatusHistory>();

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

    // ============================================================
    // DTO CLASSES
    // ============================================================
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
    }

    public class EmployeeResponseDto
    {
        public int Id { get; set; }
        public string EmployeeNo { get; set; } = string.Empty;
        public string EPFNo { get; set; } = string.Empty;
        public string AttendanceId { get; set; } = string.Empty;
        public int CompanyId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Initial { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public DateTime? DOB { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string MaritalStatus { get; set; } = string.Empty;
        public string BloodGroup { get; set; } = string.Empty;
        public string DrivingLicense { get; set; } = string.Empty;
        public string Religion { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public string Race { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string LandPhone { get; set; } = string.Empty;
        public string ContactNo { get; set; } = string.Empty;
        public string ResidentialAddress { get; set; } = string.Empty;
        public string PermanentAddress { get; set; } = string.Empty;
        public int? OccupationNo { get; set; }
        public int? OccupationGrade { get; set; }
        public string RoleType { get; set; } = "Standard Employee";
        public string LeaveApproval { get; set; } = "Super Admin";
        public string FinanceApproval { get; set; } = "Super Admin";
        public int? DesignationId { get; set; }
        public int? DepartmentId { get; set; }
        public int? ShiftBlockId { get; set; }
        public DateTime? DateOfAppointment { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal BudgetaryAllowance { get; set; }
        public decimal BudgetaryAllowance2 { get; set; }
        public decimal AttendanceAllowance { get; set; }
        public decimal FixedAllowance { get; set; }
        public decimal MealAllowance { get; set; }
        public decimal SpecialAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal AccommodationAllowance { get; set; }
        public decimal FuelAllowance { get; set; }
        public decimal CostOfLivingAllowance { get; set; }
        public decimal PerformanceAllowance { get; set; }
        public decimal HealthAllowance { get; set; }
        public bool EPFPay { get; set; }
        public string SalaryPayType { get; set; } = "CashPay";
        public string AccountNumber { get; set; } = string.Empty;
        public string BankAccountName { get; set; } = string.Empty;
        public string BankCode { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string BranchCode { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public bool Probation { get; set; }
        public int? ProbationPeriod { get; set; }
        public DateTime? ProbationEndDate { get; set; }
        public string ReviewedBy { get; set; } = string.Empty;
        public bool Block { get; set; }
        public string BlockUntil { get; set; } = string.Empty;
        public string BlockReason { get; set; } = string.Empty;
        public string BlockRemark { get; set; } = string.Empty;
        public bool Resigned { get; set; }
        public DateTime? ResignedDate { get; set; }
        public string ExitType { get; set; } = string.Empty;
        public string ExitReason { get; set; } = string.Empty;
        public string ExitRemark { get; set; } = string.Empty;
        public bool BlockAttendance { get; set; }
        public string Keen1ContactName { get; set; } = string.Empty;
        public string Keen1ContactNumber { get; set; } = string.Empty;
        public string Keen1Relationship { get; set; } = string.Empty;
        public string Keen1Address { get; set; } = string.Empty;
        public string Keen1Position { get; set; } = string.Empty;
        public string Keen1WorkPlace { get; set; } = string.Empty;
        public string Keen1WorkPlaceContact { get; set; } = string.Empty;
        public string Keen2ContactName { get; set; } = string.Empty;
        public string Keen2ContactNumber { get; set; } = string.Empty;
        public string Keen2Relationship { get; set; } = string.Empty;
        public string Keen2Address { get; set; } = string.Empty;
        public string Keen2Position { get; set; } = string.Empty;
        public string Keen2WorkPlace { get; set; } = string.Empty;
        public string Keen2WorkPlaceContact { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
        public DateTime? ModifiedDate { get; set; }
        public List<QualificationDto> Qualifications { get; set; } = new List<QualificationDto>();
        public List<WorkExperienceDto> WorkExperiences { get; set; } = new List<WorkExperienceDto>();
        public List<LeaveCustomApproverDto> LeaveCustomApprovers { get; set; } = new List<LeaveCustomApproverDto>();
        public List<FinanceCustomApproverDto> FinanceCustomApprovers { get; set; } = new List<FinanceCustomApproverDto>();
    }

    public class LeaveCustomApproverDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int ApproverEmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
        public ApproverEmployeeDto? ApproverEmployee { get; set; }
    }

    public class FinanceCustomApproverDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public int ApproverEmployeeId { get; set; }
        public int CompanyId { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; } = true;
        public ApproverEmployeeDto? ApproverEmployee { get; set; }
    }

    public class ApproverEmployeeDto
    {
        public int Id { get; set; }
        public string EmployeeNo { get; set; } = string.Empty;
        public string EPFNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string SystemName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
    }

    public class QualificationDto
    {
        public int Id { get; set; }
        public string QualificationName { get; set; } = string.Empty;
        public string InstituteName { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Month { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class WorkExperienceDto
    {
        public int Id { get; set; }
        public string Position { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public int FromYear { get; set; }
        public string FromMonth { get; set; } = string.Empty;
        public string ToYear { get; set; } = string.Empty;
        public string ToMonth { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}