using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Application.Features.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task AssignRolesAsync(long id, AssignRolesRequest request, CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UserService(IApplicationDbContext dbContext, IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyCollection<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Users.Select(x => new UserDto
        {
            Id = x.Id,
            Username = x.UserName ?? string.Empty,
            HoTen = x.HoTen,
            Email = x.Email,
            SoDienThoai = x.SoDienThoai,
            DonViId = x.DonViId,
            IsActive = x.IsActive,
            MustChangePassword = x.MustChangePassword
        }).ToListAsync(cancellationToken);

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedUserName = request.Username.Trim().ToUpperInvariant();
        var duplicateUser = await _dbContext.Users
            .CountAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken) > 0;
        if (duplicateUser)
        {
            throw new AppException("USER_DUPLICATE", "Tên đăng nhập đã tồn tại.", 409);
        }

        if (_dbContext is not DbContext efDbContext)
        {
            throw new InvalidOperationException("Database context does not support transactions.");
        }

        await using var transaction = await efDbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = new ApplicationUser
        {
            UserName = request.Username.Trim().ToLowerInvariant(),
            NormalizedUserName = normalizedUserName,
            Email = request.Email,
            NormalizedEmail = request.Email?.Trim().ToUpperInvariant(),
            HoTen = request.HoTen,
            SoDienThoai = request.SoDienThoai,
            DonViId = request.DonViId,
            PasswordHash = string.Empty
        };

        entity.PasswordHash = _passwordHasher.HashPassword(entity, request.Password);
        await _dbContext.Users.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.RoleIds.Count > 0)
        {
            await AssignRolesInternalAsync(entity.Id, new AssignRolesRequest
            {
                DonViId = request.DonViId,
                RoleIds = request.RoleIds
            }, saveChanges: false, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (await GetAllAsync(cancellationToken)).First(x => x.Id == entity.Id);
    }

    public async Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Users.FirstAsync(x => x.Id == id, cancellationToken);
        entity.HoTen = request.HoTen;
        entity.Email = request.Email;
        entity.SoDienThoai = request.SoDienThoai;
        entity.DonViId = request.DonViId;
        entity.IsActive = request.IsActive;
        entity.MustChangePassword = request.MustChangePassword;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAllAsync(cancellationToken)).First(x => x.Id == entity.Id);
    }

    public async Task AssignRolesAsync(long id, AssignRolesRequest request, CancellationToken cancellationToken = default)
        => await AssignRolesInternalAsync(id, request, saveChanges: true, cancellationToken);

    private async Task AssignRolesInternalAsync(long id, AssignRolesRequest request, bool saveChanges, CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var roleCapDonViConstraints = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["QUAN_LY"] = ["CAP_1"],
            ["LANH_DAO"] = ["CAP_1"],
            ["CAP_TINH"] = ["CAP_1"],
            ["CAP_XA"] = ["CAP_2"]
        };

        var donVi = await _dbContext.DonVis
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DonViId, cancellationToken);

        foreach (var role in roles)
        {
            if (!roleCapDonViConstraints.TryGetValue(role.Name ?? string.Empty, out var allowedCaps))
            {
                continue;
            }

            if (donVi is null || !allowedCaps.Contains(donVi.CapDonVi ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException(
                    "USER_ROLE_DONVI_CAP_MISMATCH",
                    $"Vai tro '{role.TenRole}' chi duoc gan cho don vi cap: {string.Join(", ", allowedCaps)}. Don vi hien tai: {donVi?.CapDonVi ?? "khong xac dinh"}.",
                    400);
            }
        }

        var existing = await _dbContext.UserRoleAssignments.Where(x => x.UserId == id && x.DonViId == request.DonViId).ToListAsync(cancellationToken);
        _dbContext.UserRoleAssignments.RemoveRange(existing);
        foreach (var roleId in request.RoleIds)
        {
            await _dbContext.UserRoleAssignments.AddAsync(new UserRoleAssignment
            {
                UserId = id,
                RoleId = roleId,
                DonViId = request.DonViId
            }, cancellationToken);
        }

        if (saveChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}