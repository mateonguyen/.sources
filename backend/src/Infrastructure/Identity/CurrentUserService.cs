using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Models;

namespace ThucLuc.Infrastructure.Identity;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUserProfile GetCurrentUser()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return new CurrentUserProfile();
        }

        var userId = ParseLong(principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier))
            ?? ParseLong(principal.FindFirstValue(ThucLuc.Infrastructure.Identity.ClaimTypes.UserId))
            ?? 0;

        var donViId = ParseLong(principal.FindFirstValue(ThucLuc.Infrastructure.Identity.ClaimTypes.DonViId)) ?? 0;

        return new CurrentUserProfile
        {
            UserId = userId,
            Username = principal.Identity?.Name ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? string.Empty,
            DonViId = donViId,
            Roles = principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Permissions = principal.FindAll(ThucLuc.Infrastructure.Identity.ClaimTypes.Permission).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static long? ParseLong(string? value)
        => long.TryParse(value, out var parsed) ? parsed : null;
}