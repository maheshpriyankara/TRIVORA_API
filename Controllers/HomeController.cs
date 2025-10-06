using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : Controller
    {
        private readonly string _connectionString;

        public HomeController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        [HttpGet("company-settings")]
        public IActionResult GetCompanySettings([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    return BadRequest("Token is required");
                }

                // Validate token first
                var isValid = ValidateTokenInDatabase(token);
                if (!isValid)
                {
                    return Unauthorized();
                }

                // Get user ID from token
                var userId = GetUserIDFromToken(token);
                if (string.IsNullOrEmpty(userId))
                {
                    return NotFound();
                }

                // Fetch user settings from database
                var companySettings = GetCompanySettingsFromDB(userId);
                if (companySettings == null)
                {
                    return Ok(new { success = false, message = "Companies not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = companySettings  // Make sure this property is named "data"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("company-departments")]
        public IActionResult GetCompanyDepartments([FromQuery] string companyID)
        {
            try
            {
                if (string.IsNullOrEmpty(companyID))
                {
                    return BadRequest("Token is required");
                }

                // Fetch user settings from database
                var companySettings = GetCompanySettingsFromDB(companyID);
                if (companySettings == null)
                {
                    return Ok(new { success = false, message = "Companies not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = companySettings  // Make sure this property is named "data"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        private List<CompanySettings> GetCompanySettingsFromDB(string userID)
        {
            var companyList = new List<CompanySettings>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
            SELECT 
                c.[Id] AS CompanyId,
                c.[CompanyName]
            FROM [trivora_hris].[dbo].[Company_Admins] ca
            INNER JOIN [trivora_hris].[dbo].[Company] c ON ca.[CompanyId] = c.[Id]
            WHERE ca.[AdminId] = @UserId";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            companyList.Add(new CompanySettings
                            {
                                CompanyID = reader["CompanyId"].ToString(),
                                CompanyName = reader["CompanyName"].ToString()
                            });
                        }
                    }
                }
            }
            return companyList;
        }
        private List<CompanyDepartments> GetDepartmentsSettingsFromDB(string companyID)
        {
            var departmentsList = new List<CompanyDepartments>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
            SELECT 
                [DepartmentName]
            FROM [trivora_hris].[dbo].[Company_Departments] 
           
            WHERE [CompanyId] = @CompanyId";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@CompanyId", companyID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            departmentsList.Add(new CompanyDepartments
                            {
                                DepartmentName = reader["DepartmentName"].ToString()
                            });
                        }
                    }
                }
            }
            return departmentsList;
        }
        private bool ValidateTokenInDatabase(string token)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = "SELECT 1 FROM UserTokens WHERE Token = @Token AND ExpiryDate > @CurrentDate";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@CurrentDate", DateTime.Now);

                    con.Open();
                    return cmd.ExecuteScalar() != null;
                }
            }
        }
        private string GetUserIDFromToken(string token)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = "SELECT UserID FROM UserTokens WHERE Token = @Token AND ExpiryDate > @CurrentDate";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@CurrentDate", DateTime.Now);

                    con.Open();
                    var result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        public class CompanySettings
        {
            public string CompanyID { get; set; }
            public string CompanyName { get; set; }
        }
        public class CompanyDepartments
        {
            public string DepartmentName { get; set; }
        }
    }
}
