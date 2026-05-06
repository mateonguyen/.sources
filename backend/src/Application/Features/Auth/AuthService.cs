using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthTokenDto> LoginAsync(LoginRequest request, bool rememberMe = false, CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IValidator<LoginRequest> _validator;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuthService(
        IApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        IJwtTokenService jwtTokenService,
        IValidator<LoginRequest> validator,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _jwtTokenService = jwtTokenService;
        _validator = validator;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AuthTokenDto> LoginAsync(LoginRequest request, bool rememberMe = false, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.UserName == request.Username, cancellationToken)
            ?? throw new AppException("INVALID_CREDENTIALS", "Sai tên đăng nhập hoặc mật khẩu.", 401);

        if (!user.IsActive || user.DeletedAt.HasValue)
        {
            throw new BusinessRuleException("USER_INACTIVE", "Tài khoản đã bị vô hiệu hóa.");
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw new AppException("INVALID_CREDENTIALS", "Sai tên đăng nhập hoặc mật khẩu.", 401);
        }

        var roles = await (from assignment in _dbContext.UserRoleAssignments
                           join role in _dbContext.Roles on assignment.RoleId equals role.Id
                           where assignment.UserId == user.Id
                           select role.Name ?? string.Empty)
            .ToListAsync(cancellationToken);

        var permissions = await (from assignment in _dbContext.UserRoleAssignments
                                 join rolePermission in _dbContext.RolePermissions on assignment.RoleId equals rolePermission.RoleId
                                 join permission in _dbContext.Permissions on rolePermission.PermissionId equals permission.Id
                                 where assignment.UserId == user.Id
                                 select permission.PermCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        var token = await _jwtTokenService.GenerateAsync(user, roles, permissions, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            Domain.Enums.AuditActionType.Login,
            nameof(ApplicationUser),
            user.Id,
            null,
            $"{{\"username\":\"{user.UserName}\",\"rememberMe\":{rememberMe.ToString().ToLower()}}}",
            "/api/v1/auth/login",
            null,
            null,
            cancellationToken);

        return token;
    }

    public async Task<UserProfileDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        var user = await _dbContext.Users.FirstAsync(x => x.Id == currentUser.UserId, cancellationToken);
        return new UserProfileDto
        {
            Id = currentUser.UserId,
            Username = currentUser.Username,
            HoTen = user.HoTen,
            DonViId = currentUser.DonViId,
            Roles = currentUser.Roles,
            Permissions = currentUser.Permissions,
            MustChangePassword = user.MustChangePassword
        };
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}