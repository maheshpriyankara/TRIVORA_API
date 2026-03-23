using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;

namespace TRIVORA_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly string _connectionString;

        public AuthController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserID) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest("UserID and Password are required");
                }

                // Authenticate user
                var user = AuthenticateUser(request.UserID, request.Password);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Get user's companies
                var userCompanies = GetUserCompanies(user.UserID);

                // Generate token
                var token = GenerateToken(user.UserID);

                // Store token in database
                if (StoreToken(user.UserID, token))
                {
                    // Get user settings including inactivity timeout
                    var userSettings = GetUserSettingsFromDB(user.UserID);
                    var inactivityTimeout = userSettings?.InactivityTimeout ?? 5;

                    return Ok(new
                    {
                        success = true,
                        message = "Login successful",
                        token = token,
                        user = new
                        {
                            userID = user.UserID,
                            userName = user.UserName,
                            accessType = user.AccessType,
                            systemName = user.SystemName,
                            inactivityTimeout = inactivityTimeout,
                            companies = userCompanies.Select(c => new
                            {
                                id = c.Id,
                                companyName = c.CompanyName,
                                companyAddress = c.CompanyAddress,
                                emailId = c.EmailId,
                                hrManager = c.HrManager,
                                companySector = c.CompanySector
                            }),
                            companyCount = userCompanies.Count
                        }
                    });
                }

                return StatusCode(500, "Failed to create session");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }


        // New method to get user's companies
        private List<Company> GetUserCompanies(string userID)
        {
            var companies = new List<Company>();

            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
            SELECT c.* 
            FROM Company c
            INNER JOIN Company_Admins ca ON c.Id = ca.CompanyId
            INNER JOIN Admin_Users au ON ca.AdminId = au.Id
            WHERE au.username = @UserID OR au.EmailId = @UserID OR au.NIC = @UserID";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            companies.Add(new Company
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                CompanyName = reader["CompanyName"]?.ToString(),
                                CompanyAddress = reader["CompanyAddress"]?.ToString(),
                                TelephonNuber = reader["TelephonNuber"]?.ToString(),
                                FaxNumber = reader["FaxNumber"]?.ToString(),
                                EmailId = reader["EmailId"]?.ToString(),
                                HrManager = reader["HrManager"]?.ToString(),
                                HrContact = reader["HrContact"]?.ToString(),
                                HrEmail = reader["HrEmail"]?.ToString(),
                                EpfActNo = reader["EpfActNo"]?.ToString(),
                                EtfActNp = reader["EtfActNp"]?.ToString(),
                                CompanySector = reader["CompanySector"]?.ToString(),
                                CompanyRegistrationNumber = reader["CompanyRegistrationNumber"]?.ToString(),
                                CompanyRegisterd = reader["CompanyRegisterd"] as DateTime?,
                                CompanyAbout = reader["CompanyAbout"]?.ToString(),
                                MinBasic = Convert.ToDouble(reader["MinBasic"]),
                                MinBudgetaryAllowanceOne = Convert.ToDouble(reader["MinBudgetaryAllowanceOne"]),
                                MinBudgetaryAllowanceTwo = Convert.ToDouble(reader["MinBudgetaryAllowanceTwo"])
                            });
                        }
                    }
                }
            }
            return companies;
        }
        public class Company
        {
            public int Id { get; set; }
            public string CompanyName { get; set; }
            public string CompanyAddress { get; set; }
            public string TelephonNuber { get; set; }
            public string FaxNumber { get; set; }
            public string EmailId { get; set; }
            public string HrManager { get; set; }
            public string HrContact { get; set; }
            public string HrEmail { get; set; }
            public string EpfActNo { get; set; }
            public string EtfActNp { get; set; }
            public string CompanySector { get; set; }
            public string CompanyRegistrationNumber { get; set; }
            public DateTime? CompanyRegisterd { get; set; }
            public string CompanyAbout { get; set; }
            public double MinBasic { get; set; }
            public double MinBudgetaryAllowanceOne { get; set; }
            public double MinBudgetaryAllowanceTwo { get; set; }
        }


        [HttpPost("request-otp")]
        public IActionResult RequestOTP([FromBody] OTPRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.UserID))
                {
                    return BadRequest("UserID is required");
                }

                // Check if user exists
                if (!UserExists(request.UserID))
                {
                    return NotFound();
                }

                // Generate OTP
                var otp = GenerateOTP();

                // Store OTP in database
                if (StoreOTP(request.UserID, otp))
                {
                    // In real scenario, send OTP via SMS/Email
                    // SendOTP(request.UserID, otp);

                    return Ok(new
                    {
                        success = true,
                        message = "OTP sent successfully",
                        otp = otp, // Remove this in production
                        expiresIn = 10
                    });
                }

                return StatusCode(500, "Failed to generate OTP");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("ValidateToken")]
        public IActionResult ValidateToken([FromBody] TokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest("Token is required");
                }

                var isValid = ValidateTokenInDatabase(request.Token);
                if (isValid)
                {
                    var userID = GetUserIDFromToken(request.Token);
                    return Ok(new
                    {
                        valid = true,
                        userID = userID
                    });
                }

                return Ok(new { valid = false });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout([FromBody] TokenRequest request)
        {
            try
            {
                if (!string.IsNullOrEmpty(request.Token))
                {
                    InvalidateToken(request.Token);
                }

                return Ok(new
                {
                    success = true,
                    message = "Logged out successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("user-settings")]
        public IActionResult GetUserSettings([FromQuery] string token)
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
                var userSettings = GetUserSettingsFromDB(userId);
                if (userSettings == null)
                {
                    return Ok(new { success = false, message = "User not found" });
                }

                return Ok(new
                {
                    success = true,
                    inactivityTimeout = userSettings.InactivityTimeout
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // Helper Methods (keep the same implementation with SqlConnection)
        private User AuthenticateUser(string userID, string password)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"SELECT username, Nic, AccessType, SystemName FROM Admin_Users 
                            WHERE (username = @UserID OR EmailId = @UserID OR NIC = @UserID) 
                            AND (Password = @Password)
                            AND IsActive ='true'";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);
                    cmd.Parameters.AddWithValue("@Password", password);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new User
                            {
                                UserID = reader["username"].ToString(),
                                UserName = reader["Nic"].ToString(),
                                AccessType = reader["accesstype"]?.ToString(),
                                SystemName = reader["SystemName"]?.ToString()
                            };
                        }
                    }
                }
            }
            return null;
        }

        private bool UserExists(string userID)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = "SELECT 1 FROM Admin_users WHERE id = @UserID OR Email = @UserID OR NIC = @UserID";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);

                    con.Open();
                    return cmd.ExecuteScalar() != null;
                }
            }
        }

        private string GenerateToken(string userID)
        {
            return Guid.NewGuid().ToString("N") + DateTime.Now.Ticks.ToString();
        }

        private bool StoreToken(string userID, string token)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"INSERT INTO UserTokens (UserID, Token, CreatedDate, ExpiryDate) 
                               VALUES (@UserID, @Token, @CreatedDate, @ExpiryDate)";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@ExpiryDate", DateTime.Now.AddDays(7));

                    con.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
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

        private void InvalidateToken(string token)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = "UPDATE UserTokens SET ExpiryDate = @ExpiryDate WHERE Token = @Token";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@ExpiryDate", DateTime.Now.AddMinutes(-1));

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private string GenerateOTP()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private bool StoreOTP(string userID, string otp)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"UPDATE Users SET OTP = @OTP, OTPExpiry = @OTPExpiry 
                               WHERE UserID = @UserID OR Email = @UserID OR NIC = @UserID";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);
                    cmd.Parameters.AddWithValue("@OTP", otp);
                    cmd.Parameters.AddWithValue("@OTPExpiry", DateTime.Now.AddMinutes(10));

                    con.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        private UserSettings GetUserSettingsFromDB(string userID)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                string query = @"
                    SELECT 
                        id, 
                        UserName,
                        AccessType,
                        SystemName,
                        CASE 
                            WHEN COL_LENGTH('admin_users', 'InactivityTimeout') IS NOT NULL 
                            THEN ISNULL(InactivityTimeout, 5) 
                            ELSE 5 
                        END as InactivityTimeout
                    FROM admin_users 
                    WHERE id = @UserID";

                using (var cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@UserID", userID);

                    con.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new UserSettings
                            {
                                UserID = reader["Id"].ToString(),
                                UserName = reader["UserName"].ToString(),
                                AccessType = reader["AccessType"]?.ToString(),
                                SystemName = reader["SystemName"]?.ToString(),
                                InactivityTimeout = Convert.ToInt32(reader["InactivityTimeout"])
                            };
                        }
                    }
                }
            }
            return null;
        }
    }

    // Request Models (keep the same)
    public class LoginRequest
    {
        public string UserID { get; set; }
        public string Password { get; set; }
    }

    public class OTPRequest
    {
        public string UserID { get; set; }
    }

    public class TokenRequest
    {
        public string Token { get; set; }
    }

    public class User
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string AccessType { get; set; }
        public string SystemName { get; set; }
    }


    public class UserSettings
    {
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string AccessType { get; set; }
        public string SystemName { get; set; }
        public int InactivityTimeout { get; set; }
    }


}