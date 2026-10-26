using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;
using DonViEntity = ThucLuc.Domain.Entities.System.DonVi;

namespace ThucLuc.Application.Features.DonVi;

public interface IDonViChangeService
{
    Task<DonViChangePreviewDto> PreviewMergeAsync(MergeDonViRequest request, CancellationToken cancellationToken = default);
    Task<DonViChangeResultDto> MergeAsync(MergeDonViRequest request, CancellationToken cancellationToken = default);
    Task<DonViChangePreviewDto> PreviewSplitAsync(SplitDonViRequest request, CancellationToken cancellationToken = default);
    Task<DonViChangeResultDto> SplitAsync(SplitDonViRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DonViVersion>> GetVersionsAsync(long unitId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DonViSuccessor>> GetSuccessorsAsync(long unitId, CancellationToken cancellationToken = default);
}

public sealed class DonViChangeService : IDonViChangeService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _currentUser;

    public DonViChangeService(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyCollection<DonViVersion>> GetVersionsAsync(long unitId, CancellationToken cancellationToken = default)
        => GetVersionsCoreAsync(unitId, cancellationToken);

    private async Task<IReadOnlyCollection<DonViVersion>> GetVersionsCoreAsync(long unitId, CancellationToken cancellationToken)
        => await _db.DonViVersions.AsNoTracking().Where(x => x.DonViId == unitId)
            .OrderBy(x => x.ValidFrom).ToListAsync(cancellationToken);

    public Task<IReadOnlyCollection<DonViSuccessor>> GetSuccessorsAsync(long unitId, CancellationToken cancellationToken = default)
        => GetSuccessorsCoreAsync(unitId, cancellationToken);

    private async Task<IReadOnlyCollection<DonViSuccessor>> GetSuccessorsCoreAsync(long unitId, CancellationToken cancellationToken)
        => await _db.DonViSuccessors.AsNoTracking()
            .Where(x => x.SourceDvId == unitId || x.TargetDvId == unitId)
            .OrderBy(x => x.EffectiveAt).ToListAsync(cancellationToken);

    public async Task<DonViChangePreviewDto> PreviewMergeAsync(MergeDonViRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var sourceIds = request.SourceIds?.ToArray() ?? [];
        if (sourceIds.Length == 0) errors.Add("Chưa chọn đơn vị nguồn.");
        if (sourceIds.Distinct().Count() != sourceIds.Length) errors.Add("Đơn vị nguồn bị lặp.");
        if (sourceIds.Contains(request.TargetId)) errors.Add("Đơn vị đích trùng đơn vị nguồn.");
        if (request.Note?.Length > 500) errors.Add("Ghi chú không được quá 500 ký tự.");

        var units = await _db.DonVis.AsNoTracking().Select(x => new { x.Id, x.ParentId, x.IsActive, x.TenDonVi, x.CapSo }).ToListAsync(cancellationToken);
        var byId = units.ToDictionary(x => x.Id);
        var parentMap = byId.ToDictionary(x => x.Key, x => x.Value.ParentId);
        if (!byId.TryGetValue(request.TargetId, out var target) || !target.IsActive)
            errors.Add("Đơn vị đích không tồn tại hoặc đã ngừng hoạt động.");
        foreach (var id in sourceIds)
        {
            if (!byId.TryGetValue(id, out var source) || !source.IsActive)
            {
                errors.Add($"Đơn vị nguồn {id} không tồn tại hoặc đã ngừng hoạt động.");
                continue;
            }
            if (target is not null && source.CapSo.HasValue && target.CapSo.HasValue && source.CapSo != target.CapSo)
                errors.Add($"Đơn vị nguồn {source.TenDonVi} và đơn vị kế thừa phải cùng cấp.");
            if (IsAncestor(id, request.TargetId, parentMap))
                errors.Add($"Đơn vị đích nằm trong cây của đơn vị nguồn {source.TenDonVi}; cần chuyển đích ra ngoài cây trước.");
            if (sourceIds.Any(other => other != id && IsAncestor(id, other, parentMap)))
                errors.Add("Không chọn đồng thời đơn vị cha và con làm nguồn.");
        }

        var children = await _db.DonVis.AsNoTracking().Where(x => sourceIds.Contains(x.ParentId ?? -1) && x.IsActive).ToListAsync(cancellationToken);
        var users = await _db.Users.AsNoTracking().Where(x => sourceIds.Contains(x.DonViId) && x.IsActive).ToListAsync(cancellationToken);
        var draftCount = await _db.BaoCaoSnapshots.AsNoTracking()
            .CountAsync(x => sourceIds.Contains(x.DonViId) && x.TrangThai == SnapshotStatus.Draft, cancellationToken);
        if (draftCount > 0) errors.Add($"Còn {draftCount} bản nháp báo cáo ở đơn vị nguồn; cần xử lý trước khi sáp nhập.");

        if (target is not null && users.Count > 0)
        {
            var userIds = users.Select(x => x.Id).ToArray();
            var assignments = await _db.UserRoleAssignments.AsNoTracking()
                .Where(x => userIds.Contains(x.UserId)).ToListAsync(cancellationToken);
            var roleIds = assignments.Select(x => x.RoleId).Distinct().ToArray();
            var roles = await _db.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            foreach (var user in users)
            {
                var ownRoles = assignments.Where(x => x.UserId == user.Id && x.DonViId == user.DonViId).ToArray();
                if (ownRoles.Length == 0) errors.Add($"Tài khoản {user.UserName} chưa có vai trò tại đơn vị hiện tại.");
                foreach (var assignment in ownRoles)
                    if (!roles.TryGetValue(assignment.RoleId, out var role) || !RoleFitsUnit(role.Name, target.CapSo))
                        errors.Add($"Vai trò của {user.UserName} không phù hợp cấp đơn vị đích.");
            }
        }

        return new DonViChangePreviewDto
        {
            UnitCount = sourceIds.Length,
            ChildCount = children.Count,
            UserCount = users.Count,
            DraftSnapshotCount = draftCount,
            Errors = errors.Distinct().ToArray()
        };
    }

    public async Task<DonViChangeResultDto> MergeAsync(MergeDonViRequest request, CancellationToken cancellationToken = default)
    {
        if (_db is not DbContext efDb) throw new InvalidOperationException("Database context does not support transactions.");
        await using var transaction = await efDb.Database.BeginTransactionAsync(cancellationToken);
        var preview = await PreviewMergeAsync(request, cancellationToken);
        EnsureReady(preview);
        var now = _clock.Now;
        var change = new DonViChange { ChangeType = "MERGE", EffectiveAt = now, Note = request.Note?.Trim(), CreatedAt = now, CreatedBy = _currentUser.GetCurrentUser().UserId };
        await _db.DonViChanges.AddAsync(change, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        var sourceIds = request.SourceIds.ToArray();
        var sources = await _db.DonVis.Where(x => sourceIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var children = await _db.DonVis.Where(x => sourceIds.Contains(x.ParentId ?? -1) && x.IsActive).ToListAsync(cancellationToken);
        foreach (var child in children)
        {
            child.ParentId = request.TargetId;
            await RecordVersionAsync(child, now, cancellationToken);
        }
        var users = await _db.Users.Where(x => sourceIds.Contains(x.DonViId) && x.IsActive).ToListAsync(cancellationToken);
        foreach (var user in users)
            await MoveUserAsync(user, request.TargetId, null, change.Id, now, cancellationToken);
        foreach (var source in sources)
        {
            source.IsActive = false;
            await RecordVersionAsync(source, now, cancellationToken);
            await _db.DonViSuccessors.AddAsync(new DonViSuccessor
            {
                ChangeId = change.Id, SourceDvId = source.Id, TargetDvId = request.TargetId, EffectiveAt = now
            }, cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DonViChangeResultDto { ChangeId = change.Id, ChildCount = children.Count, UserCount = users.Count };
    }

    public async Task<DonViChangePreviewDto> PreviewSplitAsync(SplitDonViRequest request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var children = request.Children?.ToArray() ?? [];
        var userMoves = request.Users?.ToArray() ?? [];
        if (children.Length == 0 && userMoves.Length == 0) errors.Add("Chưa chọn đơn vị con hoặc tài khoản cần phân bổ.");
        if (children.Select(x => x.ChildId).Distinct().Count() != children.Length) errors.Add("Một đơn vị con được phân bổ nhiều lần.");
        if (userMoves.Select(x => x.UserId).Distinct().Count() != userMoves.Length) errors.Add("Một tài khoản được phân bổ nhiều lần.");
        if (request.Note?.Length > 500) errors.Add("Ghi chú không được quá 500 ký tự.");
        var unitIds = children.Select(x => x.TargetId).Concat(userMoves.Select(x => x.TargetId)).Append(request.SourceId).Distinct().ToArray();
        var units = await _db.DonVis.AsNoTracking().Where(x => unitIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (!units.TryGetValue(request.SourceId, out var source) || !source.IsActive)
            errors.Add("Đơn vị nguồn không tồn tại hoặc đã ngừng hoạt động.");
        foreach (var targetId in unitIds.Where(x => x != request.SourceId))
            if (!units.TryGetValue(targetId, out var target) || !target.IsActive)
                errors.Add($"Đơn vị đích {targetId} không tồn tại hoặc đã ngừng hoạt động.");
        if (children.Any(x => x.TargetId == request.SourceId) || userMoves.Any(x => x.TargetId == request.SourceId))
            errors.Add("Đơn vị đích trùng đơn vị nguồn.");

        var childIds = children.Select(x => x.ChildId).ToArray();
        var actualChildren = await _db.DonVis.AsNoTracking().Where(x => childIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var move in children)
            if (!actualChildren.TryGetValue(move.ChildId, out var child) || child.ParentId != request.SourceId || !child.IsActive)
                errors.Add($"Đơn vị con {move.ChildId} không còn trực thuộc nguồn.");
        var parentMap = await _db.DonVis.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.ParentId, cancellationToken);
        foreach (var move in children)
            if (move.ChildId == move.TargetId || IsAncestor(move.ChildId, move.TargetId, parentMap))
                errors.Add($"Chuyển đơn vị con {move.ChildId} sẽ tạo vòng lặp cây.");
        foreach (var move in children)
        {
            parentMap[move.ChildId] = move.TargetId;
            if (units.TryGetValue(move.TargetId, out var target)
                && units.TryGetValue(request.SourceId, out var currentSource)
                && target.CapSo.HasValue && currentSource.CapSo.HasValue && target.CapSo != currentSource.CapSo)
                errors.Add($"Đơn vị đích {target.TenDonVi} phải cùng cấp với đơn vị nguồn.");
        }
        foreach (var move in children)
            if (IsAncestor(move.ChildId, move.ChildId, parentMap))
                errors.Add($"Phân bổ đơn vị con {move.ChildId} sẽ tạo vòng lặp cây.");

        var userIds = userMoves.Select(x => x.UserId).ToArray();
        var users = await _db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var roleIds = userMoves.SelectMany(x => x.RoleIds ?? []).Distinct().ToArray();
        var roles = await _db.Roles.AsNoTracking().Where(x => roleIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var move in userMoves)
        {
            if (!users.TryGetValue(move.UserId, out var user) || user.DonViId != request.SourceId || !user.IsActive)
                errors.Add($"Tài khoản {move.UserId} không còn hoạt động ở đơn vị nguồn.");
            if (move.RoleIds is null || move.RoleIds.Count == 0 || move.RoleIds.Distinct().Count() != move.RoleIds.Count)
                errors.Add($"Vai trò mới của tài khoản {move.UserId} không hợp lệ.");
            if (units.TryGetValue(move.TargetId, out var target))
                foreach (var roleId in move.RoleIds ?? [])
                    if (!roles.TryGetValue(roleId, out var role) || !RoleFitsUnit(role.Name, target.CapSo))
                        errors.Add($"Vai trò {roleId} không phù hợp đơn vị đích {target.TenDonVi}.");
        }
        var draftCount = await _db.BaoCaoSnapshots.AsNoTracking()
            .CountAsync(x => x.DonViId == request.SourceId && x.TrangThai == SnapshotStatus.Draft, cancellationToken);
        if (request.DeactivateSource)
        {
            var remainingChildren = await _db.DonVis.AsNoTracking().CountAsync(x => x.ParentId == request.SourceId && x.IsActive && !childIds.Contains(x.Id), cancellationToken);
            var remainingUsers = await _db.Users.AsNoTracking().CountAsync(x => x.DonViId == request.SourceId && x.IsActive && !userIds.Contains(x.Id), cancellationToken);
            if (remainingChildren > 0) errors.Add($"Còn {remainingChildren} đơn vị con chưa phân bổ.");
            if (remainingUsers > 0) errors.Add($"Còn {remainingUsers} tài khoản đang hoạt động chưa phân bổ.");
            if (draftCount > 0) errors.Add($"Còn {draftCount} bản nháp báo cáo ở đơn vị nguồn.");
        }
        return new DonViChangePreviewDto { UnitCount = 1, ChildCount = children.Length, UserCount = userMoves.Length, DraftSnapshotCount = draftCount, Errors = errors.Distinct().ToArray() };
    }

    public async Task<DonViChangeResultDto> SplitAsync(SplitDonViRequest request, CancellationToken cancellationToken = default)
    {
        if (_db is not DbContext efDb) throw new InvalidOperationException("Database context does not support transactions.");
        await using var transaction = await efDb.Database.BeginTransactionAsync(cancellationToken);
        var preview = await PreviewSplitAsync(request, cancellationToken);
        EnsureReady(preview);
        var now = _clock.Now;
        var change = new DonViChange { ChangeType = "SPLIT", EffectiveAt = now, Note = request.Note?.Trim(), CreatedAt = now, CreatedBy = _currentUser.GetCurrentUser().UserId };
        await _db.DonViChanges.AddAsync(change, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var move in request.Children)
        {
            var child = await _db.DonVis.FirstAsync(x => x.Id == move.ChildId, cancellationToken);
            child.ParentId = move.TargetId;
            await RecordVersionAsync(child, now, cancellationToken);
        }
        foreach (var move in request.Users)
        {
            var user = await _db.Users.FirstAsync(x => x.Id == move.UserId, cancellationToken);
            await MoveUserAsync(user, move.TargetId, move.RoleIds, change.Id, now, cancellationToken);
        }
        var targets = request.Children.Select(x => x.TargetId).Concat(request.Users.Select(x => x.TargetId)).Distinct();
        foreach (var targetId in targets)
            await _db.DonViSuccessors.AddAsync(new DonViSuccessor { ChangeId = change.Id, SourceDvId = request.SourceId, TargetDvId = targetId, EffectiveAt = now }, cancellationToken);
        if (request.DeactivateSource)
        {
            var source = await _db.DonVis.FirstAsync(x => x.Id == request.SourceId, cancellationToken);
            source.IsActive = false;
            await RecordVersionAsync(source, now, cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DonViChangeResultDto { ChangeId = change.Id, ChildCount = request.Children.Count, UserCount = request.Users.Count };
    }

    private async Task MoveUserAsync(ApplicationUser user, long targetId, IReadOnlyCollection<long>? newRoleIds, long changeId, DateTime now, CancellationToken cancellationToken)
    {
        var sourceId = user.DonViId;
        var assignments = await _db.UserRoleAssignments.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken);
        var roleIds = newRoleIds ?? assignments.Where(x => x.DonViId == user.DonViId).Select(x => x.RoleId).Distinct().ToArray();
        _db.UserRoleAssignments.RemoveRange(assignments);
        foreach (var roleId in roleIds)
            await _db.UserRoleAssignments.AddAsync(new UserRoleAssignment { UserId = user.Id, RoleId = roleId, DonViId = targetId }, cancellationToken);
        user.DonViId = targetId;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = now;
        await _db.UserUnitHistories.AddAsync(new UserUnitHistory
        {
            UserId = user.Id, SourceDvId = sourceId, TargetDvId = targetId,
            ChangeId = changeId, TransferredAt = now, TransferredBy = _currentUser.GetCurrentUser().UserId
        }, cancellationToken);
        var sessions = await _db.RefreshTokenSessions.Where(x => x.UserId == user.Id && !x.IsRevoked).ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevocationReason = "UNIT_CHANGE";
            session.UpdatedAt = now;
        }
    }

    private async Task RecordVersionAsync(DonViEntity unit, DateTime now, CancellationToken cancellationToken)
    {
        var version = await _db.DonViVersions.Where(x => x.DonViId == unit.Id && x.ValidTo == null)
            .OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(cancellationToken);
        if (version is not null) version.ValidTo = now;
        await _db.DonViVersions.AddAsync(new DonViVersion
        {
            DonViId = unit.Id, MaDonVi = unit.MaDonVi, TenDonVi = unit.TenDonVi,
            CapDonVi = unit.CapDonVi, CheDoNhapLieu = unit.CheDoNhapLieu,
            ParentId = unit.ParentId, IsActive = unit.IsActive, ValidFrom = now
        }, cancellationToken);
    }

    private static void EnsureReady(DonViChangePreviewDto preview)
    {
        if (preview.Errors.Count > 0)
            throw new AppException("DONVI_CHANGE_INVALID", string.Join(" ", preview.Errors), 400);
    }

    private static bool RoleFitsUnit(string? roleName, int? capSo)
        => roleName?.ToUpperInvariant() switch
        {
            "QUAN_LY" or "LANH_DAO" or "CAP_TINH" => capSo == 1,
            "CAP_XA" => capSo == 2,
            _ => true
        };

    private static bool IsAncestor(long ancestorId, long nodeId, IReadOnlyDictionary<long, long?> parents)
    {
        var seen = new HashSet<long>();
        var current = nodeId;
        while (seen.Add(current) && parents.TryGetValue(current, out var parent) && parent.HasValue)
        {
            if (parent.Value == ancestorId) return true;
            current = parent.Value;
        }
        return false;
    }
}
