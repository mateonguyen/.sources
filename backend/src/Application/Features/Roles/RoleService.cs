using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Identity;

namespace ThucLuc.Application.Features.Roles;

public interface IRoleService
{
    Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleDto> UpdateAsync(long id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task UpdatePermissionsAsync(long id, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
}

public sealed class RoleService : IRoleService
{
    private readonly IApplicationDbContext _dbContext;

    public RoleService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.Roles
            .Select(x => new RoleDto
            {
                Id = x.Id,
                RoleCode = x.Name ?? string.Empty,
                TenRole = x.TenRole,
                IsSystem = x.IsSystem,
                MoTa = x.MoTa
            })
            .ToListAsync(cancellationToken);

        var permissionsByRole = await (from rolePermission in _dbContext.RolePermissions
                                       join permission in _dbContext.Permissions on rolePermission.PermissionId equals permission.Id
                                       select new { rolePermission.RoleId, permission.PermCode })
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            role.Permissions = permissionsByRole
                .Where(x => x.RoleId == role.Id)
                .Select(x => x.PermCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return roles;
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedCode = (request.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new BusinessRuleException("ROLE_CODE_REQUIRED", "Mã vai trò là bắt buộc.");
        }

        var duplicateCode = await _dbContext.Roles
            .CountAsync(x => (x.NormalizedName ?? string.Empty) == normalizedCode, cancellationToken) > 0;
        if (duplicateCode)
        {
            throw new BusinessRuleException("ROLE_DUPLICATE", $"Mã vai trò '{normalizedCode}' đã tồn tại.");
        }

        var entity = new ApplicationRole
        {
            Name = normalizedCode,
            NormalizedName = normalizedCode,
            TenRole = (request.TenRole ?? string.Empty).Trim(),
            MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim()
        };

        await _dbContext.Roles.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAllAsync(cancellationToken)).First(x => x.Id == entity.Id);
    }

    public async Task<RoleDto> UpdateAsync(long id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("ROLE_NOT_FOUND", "Không tìm thấy vai trò.", 404);

        if (entity.IsSystem)
        {
            throw new BusinessRuleException("ROLE_SYSTEM_PROTECTED", "Không thể chỉnh sửa vai trò hệ thống.");
        }

        var normalizedCode = (request.RoleCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new BusinessRuleException("ROLE_CODE_REQUIRED", "Mã vai trò là bắt buộc.");
        }

        var duplicateCode = await _dbContext.Roles.CountAsync(
            x => x.Id != id && (x.NormalizedName ?? string.Empty) == normalizedCode,
            cancellationToken) > 0;
        if (duplicateCode)
        {
            throw new BusinessRuleException("ROLE_DUPLICATE", $"Mã vai trò '{normalizedCode}' đã tồn tại.");
        }

        entity.Name = normalizedCode;
        entity.NormalizedName = normalizedCode;
        entity.TenRole = (request.TenRole ?? string.Empty).Trim();
        entity.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAllAsync(cancellationToken)).First(x => x.Id == id);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("ROLE_NOT_FOUND", "Không tìm thấy vai trò.", 404);

        if (entity.IsSystem)
        {
            throw new BusinessRuleException("ROLE_SYSTEM_PROTECTED", "Không thể xóa vai trò hệ thống.");
        }

        var isAssigned = await _dbContext.UserRoleAssignments
            .CountAsync(x => x.RoleId == id, cancellationToken) > 0;
        if (isAssigned)
        {
            throw new BusinessRuleException("ROLE_IN_USE", "Vai trò đang được gán cho người dùng, không thể xóa.");
        }

        var rolePermissions = await _dbContext.RolePermissions.Where(x => x.RoleId == id).ToListAsync(cancellationToken);
        if (rolePermissions.Count > 0)
        {
            _dbContext.RolePermissions.RemoveRange(rolePermissions);
        }

        _dbContext.Roles.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdatePermissionsAsync(long id, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.RolePermissions.Where(x => x.RoleId == id).ToListAsync(cancellationToken);
        _dbContext.RolePermissions.RemoveRange(existing);
        foreach (var permissionId in request.PermissionIds)
        {
            await _dbContext.RolePermissions.AddAsync(new RolePermission { RoleId = id, PermissionId = permissionId }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}