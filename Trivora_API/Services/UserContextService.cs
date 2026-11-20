using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Security.Claims;

namespace TRIVORA_API.Services
{
    public class UserContextService : IUserContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetCurrentUserId()
        {
            var user = GetCurrentUser();
            if (user == null) return "System";

            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = user.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            if (string.IsNullOrEmpty(userId))
                userId = user.FindFirst("id")?.Value;

            return userId ?? "System";
        }

        public string GetCurrentUserName()
        {
            var user = GetCurrentUser();
            if (user == null) return "System";

            var userName = user.Identity?.Name;
            if (string.IsNullOrEmpty(userName))
                userName = user.FindFirst("name")?.Value;
            if (string.IsNullOrEmpty(userName))
                userName = user.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userName))
                userName = user.FindFirst("preferred_username")?.Value;
            if (string.IsNullOrEmpty(userName))
                userName = user.FindFirst("email")?.Value;
            if (string.IsNullOrEmpty(userName))
                userName = user.FindFirst(ClaimTypes.Email)?.Value;

            return userName ?? "System";
        }

        public string GetCurrentUserEmail()
        {
            var user = GetCurrentUser();
            if (user == null) return "System";

            var email = user.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
                email = user.FindFirst("email")?.Value;

            return email ?? "System";
        }

        public bool IsAuthenticated()
        {
            var user = GetCurrentUser();
            return user?.Identity?.IsAuthenticated == true;
        }

        public ClaimsPrincipal GetCurrentUser()
        {
            return _httpContextAccessor.HttpContext?.User;
        }

        public Dictionary<string, string> GetAllClaims()
        {
            var user = GetCurrentUser();
            if (user == null) return new Dictionary<string, string>();

            return user.Claims.ToDictionary(c => c.Type, c => c.Value);
        }
    }
}