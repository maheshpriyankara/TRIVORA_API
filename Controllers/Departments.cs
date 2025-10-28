// Controllers/DesignationsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRIVORA_API.Models;
using TRIVORA_API.Data;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DepartmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DepartmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/departments/company/5
        [HttpGet("company/{companyId}")]
        public async Task<ActionResult<List<CompanyDepartments >>> GetByCompanyId(int companyId)
        {
            var departments = await _context.Company_Departments
                .Where(d => d.CompanyId == companyId)
                .ToListAsync();
            return Ok(departments);
        }

        // GET: api/departments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyDepartments>> GetById(int id)
        {
            var department = await _context.Company_Departments.FindAsync(id);
            if (department == null)
                return NotFound();

            return Ok(department);
        }

        // POST: api/departments
        [HttpPost]
        public async Task<ActionResult<CompanyDepartments>> Create(CompanyDepartments department)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return BadRequest(new { success = false, message = "Department name is required" });

            if (department.CompanyId <= 0)
                return BadRequest(new { success = false, message = "Company ID is required" });

            // IMPORTANT: Reset ID to prevent manual ID insertion
            department.Id = 0;

            // Check for duplicate designation name within the same company
            bool duplicateExists = await _context.Company_Departments
                .AnyAsync(d => d.CompanyId == department.CompanyId
                            && d.DepartmentName.ToLower() == department.DepartmentName.Trim().ToLower());

            if (duplicateExists)
                return Conflict(new { success = false, message = $"Department '{department.DepartmentName}' already exists in this company" });

            try
            {
                _context.Company_Departments.Add(department);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = department, message = "Department added successfully" });
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violation at database level
                if (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(new { success = false, message = $"Designation '{department.DepartmentName}' already exists in this company" });

                return StatusCode(500, new { success = false, message = "An error occurred while saving the department" });
            }
        }

        // PUT: api/departments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, CompanyDepartments department)
        {
            if (id != department.Id)
                return BadRequest(new { success = false, message = "ID mismatch" });

            if (string.IsNullOrWhiteSpace(department.DepartmentName))
                return BadRequest(new { success = false, message = "Department name is required" });

            // Check for duplicate designation name (excluding current record)
            bool duplicateExists = await _context.Company_Departments
                .AnyAsync(d => d.CompanyId == department.CompanyId
                            && d.DepartmentName.ToLower() == department.DepartmentName.Trim().ToLower()
                            && d.Id != id);

            if (duplicateExists)
                return Conflict(new { success = false, message = $"Department '{department.DepartmentName}' already exists in this company" });

            try
            {
                _context.Entry(department).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Department updated successfully" });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DesignationExists(id))
                    return NotFound(new { success = false, message = "Department not found" });
                else
                    throw;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(new { success = false, message = $"Department '{department.DepartmentName}' already exists in this company" });

                return StatusCode(500, new { success = false, message = "An error occurred while updating the department" });
            }
        }

        // DELETE: api/departments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var department = await _context.Company_Departments.FindAsync(id);
            if (department == null)
                return NotFound(new { success = false, message = "Department not found" });

            _context.Company_Departments.Remove(department);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Department deleted successfully" });
        }

        private bool DesignationExists(int id)
        {
            return _context.Company_Departments.Any(e => e.Id == id);
        }
    }
}


public class CompanyDepartments
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }