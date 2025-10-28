// Controllers/DesignationsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRIVORA_API.Models;
using TRIVORA_API.Data;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DesignationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DesignationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/designations/company/5
        [HttpGet("company/{companyId}")]
        public async Task<ActionResult<List<CompanyDesignation>>> GetByCompanyId(int companyId)
        {
            var designations = await _context.Company_Designations
                .Where(d => d.CompanyId == companyId)
                .ToListAsync();
            return Ok(designations);
        }

        // GET: api/designations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyDesignation>> GetById(int id)
        {
            var designation = await _context.Company_Designations.FindAsync(id);
            if (designation == null)
                return NotFound();

            return Ok(designation);
        }

        // POST: api/designations
        [HttpPost]
        public async Task<ActionResult<CompanyDesignation>> Create(CompanyDesignation designation)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(designation.Designation))
                return BadRequest(new { success = false, message = "Designation name is required" });

            if (designation.CompanyId <= 0)
                return BadRequest(new { success = false, message = "Company ID is required" });

            // IMPORTANT: Reset ID to prevent manual ID insertion
            designation.Id = 0;

            // Check for duplicate designation name within the same company
            bool duplicateExists = await _context.Company_Designations
                .AnyAsync(d => d.CompanyId == designation.CompanyId
                            && d.Designation.ToLower() == designation.Designation.Trim().ToLower());

            if (duplicateExists)
                return Conflict(new { success = false, message = $"Designation '{designation.Designation}' already exists in this company" });

            try
            {
                _context.Company_Designations.Add(designation);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = designation, message = "Designation added successfully" });
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violation at database level
                if (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(new { success = false, message = $"Designation '{designation.Designation}' already exists in this company" });

                return StatusCode(500, new { success = false, message = "An error occurred while saving the designation" });
            }
        }

        // PUT: api/designations/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, CompanyDesignation designation)
        {
            if (id != designation.Id)
                return BadRequest(new { success = false, message = "ID mismatch" });

            if (string.IsNullOrWhiteSpace(designation.Designation))
                return BadRequest(new { success = false, message = "Designation name is required" });

            // Check for duplicate designation name (excluding current record)
            bool duplicateExists = await _context.Company_Designations
                .AnyAsync(d => d.CompanyId == designation.CompanyId
                            && d.Designation.ToLower() == designation.Designation.Trim().ToLower()
                            && d.Id != id);

            if (duplicateExists)
                return Conflict(new { success = false, message = $"Designation '{designation.Designation}' already exists in this company" });

            try
            {
                _context.Entry(designation).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Designation updated successfully" });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DesignationExists(id))
                    return NotFound(new { success = false, message = "Designation not found" });
                else
                    throw;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(new { success = false, message = $"Designation '{designation.Designation}' already exists in this company" });

                return StatusCode(500, new { success = false, message = "An error occurred while updating the designation" });
            }
        }

        // DELETE: api/designations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var designation = await _context.Company_Designations.FindAsync(id);
            if (designation == null)
                return NotFound(new { success = false, message = "Designation not found" });

            _context.Company_Designations.Remove(designation);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Designation deleted successfully" });
        }

        private bool DesignationExists(int id)
        {
            return _context.Company_Designations.Any(e => e.Id == id);
        }
    }
}


public class CompanyDesignation
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Designation { get; set; } = string.Empty;
    }