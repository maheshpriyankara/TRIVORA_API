using System.Security.Claims;

namespace TRIVORA_API.Services
{
    public interface IUserContextService
    {
        string GetCurrentUserId();
        string GetCurrentUserName();
        string GetCurrentUserEmail();
        bool IsAuthenticated();
        ClaimsPrincipal GetCurrentUser();
        Dictionary<string, string> GetAllClaims();
    }
}