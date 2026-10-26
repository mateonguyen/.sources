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

    Task<TransferUsersPreviewDto> PreviewTransfersAsync(TransferUsersRequest request, CancellationToken cancellationToken = default);

    Task TransferUsersAsync(TransferUsersRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserUnitHistory>> GetTransferHistoryAsync(long userId, CancellationToken cancellationToken = default);
}

public sealed class UserService : IUserService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICurrentUserService _currentUserService;

    public UserService(IApplicationDbContext dbContext, IPasswordHasher<ApplicationUser> passwordHasher, IDateTimeProvider dateTimeProvider, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
        _currentUserService = currentUserService;
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
        // Oracle stores this flag as NUMBER(1). Keeping the boolean predicate inside
        // AnyAsync makes the Oracle provider try to render an Int16 as a SQL boolean.
        // Load the target row by its key, then evaluate the flag in memory instead.
        var targetDonVi = await _dbContext.DonVis
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.DonViId, cancellationToken);
        if (targetDonVi is null || !targetDonVi.IsActive)
            throw new AppException("USER_UNIT_INACTIVE", "Chỉ tạo tài khoản tại đơn vị đang hoạt động.", 400);

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
        if (entity.DonViId != request.DonViId)
        {
            throw new AppException("USER_TRANSFER_REQUIRED", "Đổi đơn vị sử dụng bằng tác vụ chuyển tài khoản để cập nhật vai trò cùng lúc.", 400);
        }
        if (request.IsActive)
        {
            var targetDonVi = await _dbContext.DonVis
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == entity.DonViId, cancellationToken);
            if (targetDonVi is null || !targetDonVi.IsActive)
                throw new AppException("USER_UNIT_INACTIVE", "Cần chuyển tài khoản sang đơn vị đang hoạt động trước khi kích hoạt.", 400);
        }
        entity.HoTen = request.HoTen;
        entity.Email = request.Email;
        entity.SoDienThoai = request.SoDienThoai;
        entity.DonViId = request.DonViId;
        entity.IsActive = request.IsActive;
        entity.MustChangePassword = request.MustChangePassword;
        if (!request.IsActive)
        {
            entity.SecurityStamp = Guid.NewGuid().ToString("N");
            await RevokeSessionsAsync(id, "USER_INACTIVE", cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAllAsync(cancellationToken)).First(x => x.Id == entity.Id);
    }

    public async Task AssignRolesAsync(long id, AssignRolesRequest request, CancellationToken cancellationToken = default)
        => await AssignRolesInternalAsync(id, request, saveChanges: true, cancellationToken);

    public async Task<TransferUsersPreviewDto> PreviewTransfersAsync(TransferUsersRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var items = request.Items?.ToList() ?? [];
        if (items.Count == 0) errors.Add("Chưa chọn tài khoản cần chuyển.");
        if (items.Select(x => x.UserId).Distinct().Count() != items.Count) errors.Add("Một tài khoản xuất hiện nhiều lần.");

        var userIds = items.Select(x => x.UserId).Distinct().ToArray();
        var unitIds = items.Select(x => x.TargetDonViId).Distinct().ToArray();
        var roleIds = items.SelectMany(x => x.RoleIds ?? []).Distinct().ToArray();
        var users = await _dbContext.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var units = await _dbContext.DonVis.AsNoTracking().Where(x => unitIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var roles = await _dbContext.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var assignments = await _dbContext.UserRoleAssignments.AsNoTracking().Where(x => userIds.Contains(x.UserId)).ToListAsync(cancellationToken);
        var previews = new List<TransferUserPreviewItem>();

        foreach (var item in items)
        {
            if (!users.TryGetValue(item.UserId, out var user))
            {
                errors.Add($"Không tìm thấy tài khoản {item.UserId}.");
                continue;
            }
            if (user.DonViId != item.SourceDonViId) errors.Add($"Tài khoản {user.UserName} đã đổi đơn vị; cần xem trước lại.");
            if (user.DonViId == item.TargetDonViId) errors.Add($"Tài khoản {user.UserName} đã ở đơn vị đích.");
            if (!units.TryGetValue(item.TargetDonViId, out var unit) || !unit.IsActive)
            {
                errors.Add($"Đơn vị đích {item.TargetDonViId} không tồn tại hoặc đã ngừng hoạt động.");
                continue;
            }
            if (item.RoleIds is null || item.RoleIds.Count == 0) errors.Add($"Tài khoản {user.UserName} chưa có vai trò ở đơn vị mới.");
            if (item.RoleIds?.Distinct().Count() != item.RoleIds?.Count) errors.Add($"Vai trò của {user.UserName} bị lặp.");
            foreach (var roleId in item.RoleIds ?? [])
            {
                if (!roles.TryGetValue(roleId, out var role))
                {
                    errors.Add($"Không tìm thấy vai trò {roleId}.");
                    continue;
                }
                if (!IsRoleAllowedAtUnit(role.Name, unit.CapSo))
                    errors.Add($"Vai trò {role.Name} không phù hợp cấp đơn vị {unit.TenDonVi}.");
            }
            previews.Add(new TransferUserPreviewItem
            {
                UserId = user.Id,
                HoTen = user.HoTen,
                SourceDonViId = user.DonViId,
                TargetDonViId = unit.Id,
                OldRoleIds = assignments.Where(x => x.UserId == user.Id).Select(x => x.RoleId).Distinct().ToArray(),
                NewRoleIds = item.RoleIds?.ToArray() ?? []
            });
        }

        return new TransferUsersPreviewDto { UserCount = previews.Count, Errors = errors, Users = previews };
    }

    public async Task TransferUsersAsync(TransferUsersRequest request, CancellationToken cancellationToken = default)
    {
        if (_dbContext is not DbContext efDbContext)
            throw new InvalidOperationException("Database context does not support transactions.");
        await using var transaction = await efDbContext.Database.BeginTransactionAsync(cancellationToken);
        var preview = await PreviewTransfersAsync(request, cancellationToken);
        if (preview.Errors.Count > 0)
            throw new AppException("USER_TRANSFER_INVALID", string.Join(" ", preview.Errors), 400);

        var now = _dateTimeProvider.Now;
        foreach (var item in request.Items)
        {
            var user = await _dbContext.Users.FirstAsync(x => x.Id == item.UserId, cancellationToken);
            var oldRoles = await _dbContext.UserRoleAssignments.Where(x => x.UserId == item.UserId).ToListAsync(cancellationToken);
            _dbContext.UserRoleAssignments.RemoveRange(oldRoles);
            user.DonViId = item.TargetDonViId;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = now;
            foreach (var roleId in item.RoleIds)
                await _dbContext.UserRoleAssignments.AddAsync(new UserRoleAssignment { UserId = item.UserId, DonViId = item.TargetDonViId, RoleId = roleId }, cancellationToken);
            await _dbContext.UserUnitHistories.AddAsync(new UserUnitHistory
            {
                UserId = item.UserId,
                SourceDvId = item.SourceDonViId,
                TargetDvId = item.TargetDonViId,
                TransferredAt = now,
                TransferredBy = _currentUserService.GetCurrentUser().UserId
            }, cancellationToken);
            await RevokeSessionsAsync(item.UserId, "UNIT_TRANSFER", cancellationToken);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserUnitHistory>> GetTransferHistoryAsync(long userId, CancellationToken cancellationToken = default)
        => await _dbContext.UserUnitHistories.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.TransferredAt).ToListAsync(cancellationToken);

    private async Task AssignRolesInternalAsync(long id, AssignRolesRequest request, bool saveChanges, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("USER_NOT_FOUND", "Không tìm thấy tài khoản.", 404);
        if (user.DonViId != request.DonViId)
            throw new AppException("USER_ROLE_DONVI_MISMATCH", "Vai trò phải được gán tại đơn vị hiện tại của tài khoản.", 400);
        var roles = await _dbContext.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var roleCapSoConstraints = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["QUAN_LY"] = [1],
            ["LANH_DAO"] = [1],
            ["CAP_TINH"] = [1],
            ["CAP_XA"] = [2]
        };

        var donVi = await _dbContext.DonVis
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DonViId, cancellationToken);
        if (donVi is null || !donVi.IsActive)
            throw new AppException("USER_UNIT_INACTIVE", "Chỉ gán vai trò tại đơn vị đang hoạt động.", 400);

        foreach (var role in roles)
        {
            if (!roleCapSoConstraints.TryGetValue(role.Name ?? string.Empty, out var allowedCaps))
            {
                continue;
            }

            if (donVi is null || !allowedCaps.Contains(donVi.CapSo ?? -1))
            {
                throw new AppException(
                    "USER_ROLE_DONVI_CAP_MISMATCH",
                    $"Vai tro '{role.TenRole}' chi duoc gan cho don vi cap: {string.Join(", ", allowedCaps)}. Don vi hien tai: {donVi?.CapDonVi ?? "khong xac dinh"}.",
                    400);
            }
        }

        var existing = await _dbContext.UserRoleAssignments.Where(x => x.UserId == id).ToListAsync(cancellationToken);
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
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await RevokeSessionsAsync(id, "ROLES_CHANGED", cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsRoleAllowedAtUnit(string? roleName, int? capSo)
        => roleName?.ToUpperInvariant() switch
        {
            "QUAN_LY" or "LANH_DAO" or "CAP_TINH" => capSo == 1,
            "CAP_XA" => capSo == 2,
            _ => true
        };

    private async Task RevokeSessionsAsync(long userId, string reason, CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.RefreshTokenSessions
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = _dateTimeProvider.Now;
            session.RevocationReason = reason;
            session.UpdatedAt = _dateTimeProvider.Now;
        }
    }
}
