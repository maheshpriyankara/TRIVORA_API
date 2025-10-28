using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRIVORA_API.Models;
using TRIVORA_API.Data;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShiftBlocksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ShiftBlocksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/shiftblocks/company/5
        [HttpGet("company/{companyId}")]
        public async Task<ActionResult<List<ShiftBlock>>> GetByCompanyId(int companyId)
        {
            var shiftBlocks = await _context.ShiftBlocks
                .Where(s => s.CompanyId == companyId && s.IsActive)
                .ToListAsync();
            return Ok(new { success = true, data = shiftBlocks });
        }

        // GET: api/shiftblocks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ShiftBlock>> GetById(int id)
        {
            var shiftBlock = await _context.ShiftBlocks
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
                
            if (shiftBlock == null)
                return NotFound(new { success = false, message = "Shift block not found" });

            return Ok(new { success = true, data = shiftBlock });
        }

        // POST: api/shiftblocks
        [HttpPost]
        public async Task<ActionResult<ShiftBlock>> Create(ShiftBlock shiftBlock)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(shiftBlock.ShiftName))
                return BadRequest(new { success = false, message = "Shift name is required" });

            if (shiftBlock.CompanyId <= 0)
                return BadRequest(new { success = false, message = "Company ID is required" });

            // IMPORTANT: Reset ID to prevent manual ID insertion
            shiftBlock.Id = 0;
            shiftBlock.IsActive = true;
            shiftBlock.CreatedDate = DateTime.Now;

            // Check for duplicate shift name within the same company
            bool duplicateExists = await _context.ShiftBlocks
                .AnyAsync(s => s.CompanyId == shiftBlock.CompanyId
                            && s.ShiftName.ToLower() == shiftBlock.ShiftName.Trim().ToLower()
                            && s.IsActive);

            if (duplicateExists)
                return Conflict(new { success = false, message = $"Shift block '{shiftBlock.ShiftName}' already exists in this company" });

            try
            {
                _context.ShiftBlocks.Add(shiftBlock);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, data = new { id = shiftBlock.Id }, message = "Shift block created successfully" });
            }
            catch (DbUpdateException ex)
            {
                // Handle unique constraint violation at database level
                if (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(new { success = false, message = $"Shift block '{shiftBlock.ShiftName}' already exists in this company" });

                return StatusCode(500, new { success = false, message = "An error occurred while saving the shift block" });
            }
        }

        // PUT: api/shiftblocks/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, ShiftBlock shiftBlock)
        {
            if (id != shiftBlock.Id)
                return BadRequest(new { success = false, message = "ID mismatch" });

            if (string.IsNullOrWhiteSpace(shiftBlock.ShiftName))
                return BadRequest(new { success = false, message = "Shift name is required" });

            // Check if shift block exists and is active
            var existingShiftBlock = await _context.ShiftBlocks
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
                
            if (existingShiftBlock == null)
                return NotFound(new { success = false, message = "Shift block not found" });

            // Check for duplicate shift name (excluding current record)
            bool duplicateExists = await _context.ShiftBlocks
                .AnyAsync(s => s.CompanyId == shiftBlock.CompanyId
                            && s.ShiftName.ToLower() == shiftBlock.ShiftName.Trim().ToLower()
                            && s.Id != id
                            && s.IsActive);

            if (duplicateExists)
                return Conflict(new { success = false, message = $"Shift block '{shiftBlock.ShiftName}' already exists in this company" });

            try
            {
                // Update properties
                existingShiftBlock.ShiftName = shiftBlock.ShiftName;
                existingShiftBlock.IsAttendance = shiftBlock.IsAttendance;
                existingShiftBlock.ModifiedDate = DateTime.Now;

                // Single Shift Settings
                existingShiftBlock.DefaultInTime = shiftBlock.DefaultInTime;
                existingShiftBlock.DefaultOutTime = shiftBlock.DefaultOutTime;
                existingShiftBlock.MaxLateCutOffCheckIn = shiftBlock.MaxLateCutOffCheckIn;
                existingShiftBlock.MaxOtCutOffCheckOut = shiftBlock.MaxOtCutOffCheckOut;
                existingShiftBlock.DayType1 = shiftBlock.DayType1;
                existingShiftBlock.DayType2 = shiftBlock.DayType2;

                // Half Day Settings
                existingShiftBlock.HalfDayInTime = shiftBlock.HalfDayInTime;
                existingShiftBlock.HalfDayOutTime = shiftBlock.HalfDayOutTime;
                existingShiftBlock.HalfDayMaxLateCutOffCheckIn = shiftBlock.HalfDayMaxLateCutOffCheckIn;
                existingShiftBlock.HalfDayMaxOtCutOffCheckIn = shiftBlock.HalfDayMaxOtCutOffCheckIn;
                existingShiftBlock.HalfDayEveInTime = shiftBlock.HalfDayEveInTime;
                existingShiftBlock.HalfDayEveOutTime = shiftBlock.HalfDayEveOutTime;
                existingShiftBlock.HalfDayEveMaxLateCutOffCheckOut = shiftBlock.HalfDayEveMaxLateCutOffCheckOut;
                existingShiftBlock.HalfDayEveMaxOtCutOffCheckOut = shiftBlock.HalfDayEveMaxOtCutOffCheckOut;

                // Multi Shift Settings
                existingShiftBlock.FirstShiftInTime = shiftBlock.FirstShiftInTime;
                existingShiftBlock.FirstShiftOutTime = shiftBlock.FirstShiftOutTime;
                existingShiftBlock.FirstShiftMaxLate = shiftBlock.FirstShiftMaxLate;
                existingShiftBlock.FirstShiftMaxOT = shiftBlock.FirstShiftMaxOT;
                existingShiftBlock.FirstShiftDayType1 = shiftBlock.FirstShiftDayType1;
                existingShiftBlock.FirstShiftDayType2 = shiftBlock.FirstShiftDayType2;
                existingShiftBlock.SecondShiftInTime = shiftBlock.SecondShiftInTime;
                existingShiftBlock.SecondShiftOutTime = shiftBlock.SecondShiftOutTime;
                existingShiftBlock.SecondShiftMaxLate = shiftBlock.SecondShiftMaxLate;
                existingShiftBlock.SecondShiftMaxOT = shiftBlock.SecondShiftMaxOT;
                existingShiftBlock.SecondShiftDayType1 = shiftBlock.SecondShiftDayType1;
                existingShiftBlock.SecondShiftDayType2 = shiftBlock.SecondShiftDayType2;

                // Payment Settings
                existingShiftBlock.DayOffRate = shiftBlock.DayOffRate;
                existingShiftBlock.IsAttendanceAllowance = shiftBlock.IsAttendanceAllowance;

                // Attendance Allowance Settings
                existingShiftBlock.LeaveCount = shiftBlock.LeaveCount;
                existingShiftBlock.LeaveCount2 = shiftBlock.LeaveCount2;
                existingShiftBlock.NoPayCount = shiftBlock.NoPayCount;
                existingShiftBlock.NoPayCount2 = shiftBlock.NoPayCount2;
                existingShiftBlock.LateHoursThreshold = shiftBlock.LateHoursThreshold;
                existingShiftBlock.AttendanceAllowancePercent = shiftBlock.AttendanceAllowancePercent;
                existingShiftBlock.AttendanceAllowancePercent2 = shiftBlock.AttendanceAllowancePercent2;
                existingShiftBlock.AttendanceAllowancePercent3 = shiftBlock.AttendanceAllowancePercent3;
                existingShiftBlock.AttendanceAllowancePercent4 = shiftBlock.AttendanceAllowancePercent4;
                existingShiftBlock.AttendanceAllowancePercent5 = shiftBlock.AttendanceAllowancePercent5;

                // Late Deduction Settings
                existingShiftBlock.IsLateDeduction = shiftBlock.IsLateDeduction;
                existingShiftBlock.LateDeductionType = shiftBlock.LateDeductionType;
                existingShiftBlock.LateDeductRate = shiftBlock.LateDeductRate;
                existingShiftBlock.LateGracePeriod = shiftBlock.LateGracePeriod;

                // Overtime Settings
                existingShiftBlock.IsOvertimePay = shiftBlock.IsOvertimePay;
                existingShiftBlock.OvertimeType = shiftBlock.OvertimeType;
                existingShiftBlock.OvertimeRate = shiftBlock.OvertimeRate;
                existingShiftBlock.OtMinimumHours = shiftBlock.OtMinimumHours;

                // Shift Type
                existingShiftBlock.ShiftType = shiftBlock.ShiftType;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Shift block updated successfully" });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ShiftBlockExists(id))
                    return NotFound(new { success = false, message = "Shift block not found" });
                else
                    throw;
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException?.Message?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(new { success = false, message = $"Shift block '{shiftBlock.ShiftName}' already exists in this company" });

                return StatusCode(500, new { success = false, message = "An error occurred while updating the shift block" });
            }
        }

        // DELETE: api/shiftblocks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var shiftBlock = await _context.ShiftBlocks
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
                
            if (shiftBlock == null)
                return NotFound(new { success = false, message = "Shift block not found" });

            // Soft delete
            shiftBlock.IsActive = false;
            shiftBlock.ModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Shift block deleted successfully" });
        }

        private bool ShiftBlockExists(int id)
        {
            return _context.ShiftBlocks.Any(e => e.Id == id && e.IsActive);
        }
    }
}