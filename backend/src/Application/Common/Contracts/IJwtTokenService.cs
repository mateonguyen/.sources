using ThucLuc.Application.Features.Auth;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Application.Common.Contracts;

public interface IJwtTokenService
{
    Task<AuthTokenDto> GenerateAsync(ApplicationUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default);
}