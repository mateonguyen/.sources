using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Features.Auth;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Identity;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IDateTimeProvider _dateTimeProvider;

    public JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider)
    {
        _options = options.Value;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<AuthTokenDto> GenerateAsync(ApplicationUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default)
    {
        var now = _dateTimeProvider.Now;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.UserId, user.Id.ToString()),
            new(ClaimTypes.DonViId, user.DonViId.ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(System.Security.Claims.ClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim(ClaimTypes.Permission, permission)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken);

        return Task.FromResult(new AuthTokenDto
        {
            AccessToken = handler.WriteToken(token),
            ExpiresIn = (int)TimeSpan.FromMinutes(_options.AccessTokenMinutes).TotalSeconds,
            RefreshToken = refreshToken,
            User = new UserProfileDto
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                HoTen = user.HoTen,
                DonViId = user.DonViId,
                Roles = roles,
                Permissions = permissions,
                MustChangePassword = user.MustChangePassword
            }
        });
    }
}