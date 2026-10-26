using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Enums;
using KyBaoCaoEntity = ThucLuc.Domain.Entities.Reporting.KyBaoCao;

namespace ThucLuc.Application.Features.BaoCaoBienDong;

public interface IBaoCaoBienDongService
{
    Task<BaoCaoBienDongOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<BaoCaoHienTrangOverviewDto> GetCurrentAsync(long kyBaoCaoId, long? donViId, string? moduleCode, CancellationToken cancellationToken = default);
    Task<BaoCaoBienDongOverviewDto> GetOverviewAsync(long toKyBaoCaoId, long? fromKyBaoCaoId, long? donViId, string? moduleCode, CancellationToken cancellationToken = default);
    Task<BaoCaoBienDongTrendDto> GetTrendAsync(long donViId, string moduleCode, string metricCode, int limit = 8, CancellationToken cancellationToken = default);
}

public static class BaoCaoChangeStatuses
{
    public const string Improving = "IMPROVING";
    public const string Worsening = "WORSENING";
    public const string Stable = "STABLE";
    public const string Changed = "CHANGED";
    public const string Missing = "MISSING";

    public static string Classify(decimal? from, decimal? to, string direction)
    {
        if (!from.HasValue || !to.HasValue) return Missing;
        var delta = to.Value - from.Value;
        if (delta == 0) return Stable;
        if (direction == BaoCaoMetricDirections.Neutral) return Changed;
        var isGood = direction == BaoCaoMetricDirections.UpIsGood ? delta > 0 : delta < 0;
        return isGood ? Improving : Worsening;
    }
}

public sealed class BaoCaoBienDongService : IBaoCaoBienDongService
{
    private const int ManagementUnitLevel = 1;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDonViDataScopeService _scopeService;

    public BaoCaoBienDongService(IApplicationDbContext db, ICurrentUserService currentUserService, IDonViDataScopeService scopeService)
    {
        _db = db;
        _currentUserService = currentUserService;
        _scopeService = scopeService;
    }

    public async Task<BaoCaoBienDongOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var (canViewScope, allowedIds) = await ResolveAllowedUnitIdsAsync(cancellationToken);
        var current = _currentUserService.GetCurrentUser();
        var activeFlag = true;
        var unitsQuery = _db.DonVis.AsNoTracking().Where(x => x.IsActive == activeFlag);
        if (allowedIds is not null) unitsQuery = unitsQuery.Where(x => allowedIds.Contains(x.Id));
        var scopedUnits = await unitsQuery.Select(x => new BaoCaoBienDongDonViOptionDto
        {
            Id = x.Id, ParentId = x.ParentId, MaDonVi = x.MaDonVi, TenDonVi = x.TenDonVi
        }).ToListAsync(cancellationToken);

        var participantQuery = _db.BaoCaoSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.TrangThai == SnapshotStatus.Locked || snapshot.TrangThai == SnapshotStatus.Submitted);
        if (allowedIds is not null) participantQuery = participantQuery.Where(snapshot => allowedIds.Contains(snapshot.DonViId));
        if (canViewScope) participantQuery = participantQuery.Where(snapshot => _db.DonVis.Any(unit =>
            unit.Id == snapshot.DonViId && unit.CapSo == ManagementUnitLevel));
        var participantIds = (await participantQuery.Select(snapshot => snapshot.DonViId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

        // Chỉ trả đơn vị có dữ liệu và chuỗi tổ tiên cần thiết để dựng cây; không đẩy hàng nghìn
        // đơn vị không liên quan xuống trình duyệt. Tổ tiên chỉ dùng điều hướng, không được chọn
        // nếu bản thân chưa từng có báo cáo.
        var unitById = scopedUnits.ToDictionary(x => x.Id);
        var visibleUnitIds = participantIds.ToHashSet();
        visibleUnitIds.Add(current.DonViId);
        foreach (var unitId in visibleUnitIds.ToArray())
        {
            var cursor = unitById.GetValueOrDefault(unitId);
            while (cursor?.ParentId is long parentId && unitById.TryGetValue(parentId, out var parent))
            {
                if (!visibleUnitIds.Add(parentId)) break;
                cursor = parent;
            }
        }
        var units = scopedUnits.Where(x => visibleUnitIds.Contains(x.Id)).OrderBy(x => x.TenDonVi).ToList();
        foreach (var unit in units) unit.HasReportData = participantIds.Contains(unit.Id);

        var periodQuery =
            from ky in _db.KyBaoCaos.AsNoTracking()
            join mau in _db.MauBaoCaos.AsNoTracking() on ky.MauBaoCaoId equals mau.Id into mauJoin
            from mau in mauJoin.DefaultIfEmpty()
            where _db.BaoCaoSnapshots.Any(s => s.KyBaoCaoId == ky.Id && (s.TrangThai == SnapshotStatus.Locked || s.TrangThai == SnapshotStatus.Submitted))
            select new { Ky = ky, TenMau = mau == null ? string.Empty : mau.TenMau, Modules = mau == null ? "[]" : mau.DanhSachModule };
        if (allowedIds is not null)
        {
            var scopedUnitIds = allowedIds.ToArray();
            periodQuery = periodQuery.Where(row => _db.BaoCaoSnapshots.Any(snapshot =>
                snapshot.KyBaoCaoId == row.Ky.Id
                && scopedUnitIds.Contains(snapshot.DonViId)
                && (snapshot.TrangThai == SnapshotStatus.Locked || snapshot.TrangThai == SnapshotStatus.Submitted)));
        }
        if (canViewScope)
        {
            periodQuery = periodQuery.Where(row => _db.BaoCaoSnapshots.Any(snapshot =>
                snapshot.KyBaoCaoId == row.Ky.Id
                && (snapshot.TrangThai == SnapshotStatus.Locked || snapshot.TrangThai == SnapshotStatus.Submitted)
                && _db.DonVis.Any(unit => unit.Id == snapshot.DonViId && unit.CapSo == ManagementUnitLevel)));
        }
        var periodRows = await periodQuery
            .OrderByDescending(x => x.Ky.Nam).ThenByDescending(x => x.Ky.Quy).ThenByDescending(x => x.Ky.Thang)
            .ToListAsync(cancellationToken);

        var periods = periodRows.Select(x => new BaoCaoBienDongKyOptionDto
        {
            Id = x.Ky.Id,
            KyCode = x.Ky.KyCode,
            TenKy = string.IsNullOrWhiteSpace(x.Ky.TenKy) ? x.Ky.KyCode : x.Ky.TenKy!,
            MauBaoCaoId = x.Ky.MauBaoCaoId,
            TenMau = x.TenMau,
            Nam = x.Ky.Nam,
            Quy = x.Ky.Quy,
            Thang = x.Ky.Thang,
            ModuleCodes = ParseModules(x.Modules),
        }).ToArray();

        var moduleCodes = periods.SelectMany(x => x.ModuleCodes).Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modules = BaoCaoMetricCatalog.All.Where(x => moduleCodes.Contains(x.ModuleCode))
            .GroupBy(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BaoCaoBienDongModuleOptionDto { Code = x.Key, Name = x.First().ModuleName })
            .OrderBy(x => x.Name).ToArray();

        return new BaoCaoBienDongOptionsDto
        {
            CanViewScope = canViewScope,
            CurrentDonViId = current.DonViId,
            DonVis = units,
            KyBaoCaos = periods,
            Modules = modules,
        };
    }

    public async Task<BaoCaoHienTrangOverviewDto> GetCurrentAsync(
        long kyBaoCaoId,
        long? donViId,
        string? moduleCode,
        CancellationToken cancellationToken = default)
    {
        var (canViewScope, allowedIds) = await ResolveAllowedUnitIdsAsync(cancellationToken);
        if (donViId.HasValue && allowedIds is not null && !allowedIds.Contains(donViId.Value))
            throw new AppException("BIEN_DONG_SCOPE_DENIED", "Không có quyền xem hiện trạng của đơn vị ngoài phạm vi.", 403);
        if (donViId.HasValue && canViewScope && !await IsManagementUnitAsync(donViId.Value, cancellationToken))
            throw new AppException("BIEN_DONG_LEVEL_DENIED", "Màn hình quản lý chỉ theo dõi đơn vị cấp tỉnh (CAP_1).", 403);

        var period = await _db.KyBaoCaos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ báo cáo.", 404);
        var selectedUnits = await ResolveSelectedUnitsAsync(
            canViewScope,
            allowedIds,
            donViId,
            period.Id,
            period.Id,
            cancellationToken);
        var selectedUnitIds = selectedUnits.Select(x => x.Id).ToArray();
        var facts = await LoadPeriodFactsAsync(period.Id, selectedUnitIds, moduleCode, cancellationToken);

        var units = selectedUnits.Select(unit =>
        {
            var unitFacts = facts.Where(x => x.SourceDonViId == unit.Id).ToArray();
            var moduleCodes = unitFacts.Select(x => x.ModuleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToArray();
            var modules = moduleCodes.Select(code => BuildCurrentModule(code, unitFacts)).ToArray();
            return new BaoCaoHienTrangUnitDto
            {
                DonViId = unit.Id,
                MaDonVi = unit.MaDonVi,
                TenDonVi = unit.TenDonVi,
                ModuleCount = modules.Length,
                MetricCount = modules.Sum(x => x.Metrics.Count(metric => metric.Value.HasValue)),
                Modules = modules,
            };
        }).ToArray();

        return new BaoCaoHienTrangOverviewDto
        {
            KyBaoCaoId = period.Id,
            KyCode = period.KyCode,
            TenKy = string.IsNullOrWhiteSpace(period.TenKy) ? period.KyCode : period.TenKy!,
            TotalUnits = units.Length,
            TotalModules = units.SelectMany(x => x.Modules).Select(x => x.ModuleCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            TotalMetrics = units.Sum(x => x.MetricCount),
            Units = units,
        };
    }

    public async Task<BaoCaoBienDongOverviewDto> GetOverviewAsync(long toKyBaoCaoId, long? fromKyBaoCaoId, long? donViId, string? moduleCode, CancellationToken cancellationToken = default)
    {
        var (canViewScope, allowedIds) = await ResolveAllowedUnitIdsAsync(cancellationToken);
        if (donViId.HasValue && allowedIds is not null && !allowedIds.Contains(donViId.Value))
            throw new AppException("BIEN_DONG_SCOPE_DENIED", "Không có quyền xem biến động của đơn vị ngoài phạm vi.", 403);
        if (donViId.HasValue && canViewScope && !await IsManagementUnitAsync(donViId.Value, cancellationToken))
            throw new AppException("BIEN_DONG_LEVEL_DENIED", "Màn hình quản lý chỉ theo dõi đơn vị cấp tỉnh (CAP_1).", 403);

        var toKy = await _db.KyBaoCaos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == toKyBaoCaoId, cancellationToken)
            ?? throw new AppException("KY_BAO_CAO_NOT_FOUND", "Không tìm thấy kỳ đích.", 404);
        var fromKy = fromKyBaoCaoId.HasValue
            ? await _db.KyBaoCaos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fromKyBaoCaoId.Value, cancellationToken)
            : await FindPreviousCompatiblePeriodAsync(toKy, cancellationToken);
        if (fromKy is null) throw new AppException("BIEN_DONG_BASELINE_NOT_FOUND", "Chưa có kỳ tương thích trước kỳ đích để đối chiếu.", 422);
        if (toKy.MauBaoCaoId != fromKy.MauBaoCaoId)
            throw new AppException("BIEN_DONG_INCOMPATIBLE_PERIODS", "Chỉ có thể so sánh hai kỳ sử dụng cùng một mẫu báo cáo.", 422);

        var selectedUnits = await ResolveSelectedUnitsAsync(canViewScope, allowedIds, donViId, fromKy.Id, toKy.Id, cancellationToken);
        var selectedUnitIds = selectedUnits.Select(x => x.Id).ToArray();
        var fromFacts = await LoadPeriodFactsAsync(fromKy.Id, selectedUnitIds, moduleCode, cancellationToken);
        var toFacts = await LoadPeriodFactsAsync(toKy.Id, selectedUnitIds, moduleCode, cancellationToken);

        var unitDtos = new List<BaoCaoBienDongUnitDto>();
        foreach (var unit in selectedUnits)
        {
            var fromByUnit = fromFacts.Where(x => x.SourceDonViId == unit.Id).ToArray();
            var toByUnit = toFacts.Where(x => x.SourceDonViId == unit.Id).ToArray();
            var moduleKeys = fromByUnit.Select(x => x.ModuleCode).Concat(toByUnit.Select(x => x.ModuleCode))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
            var modules = moduleKeys.Select(code => BuildModule(code, fromByUnit, toByUnit)).ToArray();
            var statuses = modules.SelectMany(x => x.Metrics).Select(x => x.Status).ToArray();
            unitDtos.Add(new BaoCaoBienDongUnitDto
            {
                DonViId = unit.Id,
                MaDonVi = unit.MaDonVi,
                TenDonVi = unit.TenDonVi,
                ImprovingCount = statuses.Count(x => x == BaoCaoChangeStatuses.Improving),
                WorseningCount = statuses.Count(x => x == BaoCaoChangeStatuses.Worsening),
                ChangedCount = statuses.Count(x => x == BaoCaoChangeStatuses.Changed),
                MissingCount = statuses.Count(x => x == BaoCaoChangeStatuses.Missing),
                Status = ResolveRollupStatus(statuses),
                Modules = modules,
            });
        }

        return new BaoCaoBienDongOverviewDto
        {
            FromKyBaoCaoId = fromKy.Id,
            FromKyCode = fromKy.KyCode,
            ToKyBaoCaoId = toKy.Id,
            ToKyCode = toKy.KyCode,
            TotalUnits = unitDtos.Count,
            ImprovingCount = unitDtos.Count(x => x.Status == BaoCaoChangeStatuses.Improving),
            WorseningCount = unitDtos.Count(x => x.Status == BaoCaoChangeStatuses.Worsening),
            StableCount = unitDtos.Count(x => x.Status == BaoCaoChangeStatuses.Stable),
            ChangedCount = unitDtos.Count(x => x.Status == BaoCaoChangeStatuses.Changed),
            MissingDataCount = unitDtos.Count(x => x.Status == BaoCaoChangeStatuses.Missing),
            Units = unitDtos,
        };
    }

    public async Task<BaoCaoBienDongTrendDto> GetTrendAsync(long donViId, string moduleCode, string metricCode, int limit = 8, CancellationToken cancellationToken = default)
    {
        var (canViewScope, allowedIds) = await ResolveAllowedUnitIdsAsync(cancellationToken);
        if (allowedIds is not null && !allowedIds.Contains(donViId))
            throw new AppException("BIEN_DONG_SCOPE_DENIED", "Không có quyền xem biến động của đơn vị ngoài phạm vi.", 403);
        if (canViewScope && !await IsManagementUnitAsync(donViId, cancellationToken))
            throw new AppException("BIEN_DONG_LEVEL_DENIED", "Màn hình quản lý chỉ theo dõi đơn vị cấp tỉnh (CAP_1).", 403);
        var definition = BaoCaoMetricCatalog.Find(moduleCode, metricCode)
            ?? throw new AppException("BIEN_DONG_METRIC_NOT_FOUND", "Chỉ số theo dõi không hợp lệ.", 422);
        var unitName = await _db.DonVis.AsNoTracking().Where(x => x.Id == donViId).Select(x => x.TenDonVi).FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("DON_VI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        limit = Math.Clamp(limit, 2, 24);

        var rows = await (
            from metric in _db.BaoCaoSnapshotMetrics.AsNoTracking()
            join ky in _db.KyBaoCaos.AsNoTracking() on metric.KyBaoCaoId equals ky.Id
            where metric.SourceDonViId == donViId && metric.ModuleCode == moduleCode && metric.MetricCode == metricCode
            select new { metric, ky })
            .ToListAsync(cancellationToken);
        var points = rows.GroupBy(x => x.ky.Id).Select(g =>
        {
            var values = g.Select(x => x.metric).ToArray();
            var aggregate = AggregateFacts(values);
            var ky = g.First().ky;
            return new { ky, Value = aggregate.Value };
        }).OrderByDescending(x => x.ky.Nam).ThenByDescending(x => x.ky.Quy).ThenByDescending(x => x.ky.Thang)
          .Take(limit).OrderBy(x => x.ky.Nam).ThenBy(x => x.ky.Quy).ThenBy(x => x.ky.Thang)
          .Select(x => new BaoCaoBienDongTrendPointDto { KyBaoCaoId = x.ky.Id, KyCode = x.ky.KyCode, TenKy = x.ky.TenKy ?? x.ky.KyCode, Value = x.Value }).ToArray();

        return new BaoCaoBienDongTrendDto { DonViId = donViId, TenDonVi = unitName, ModuleCode = moduleCode, ModuleName = definition.ModuleName, MetricCode = metricCode, MetricName = definition.MetricName, Unit = definition.Unit, Points = points };
    }

    private async Task<(bool CanViewScope, HashSet<long>? AllowedIds)> ResolveAllowedUnitIdsAsync(CancellationToken cancellationToken)
    {
        var current = _currentUserService.GetCurrentUser();
        var canViewScope = current.HasPermission(Permissions.BaoCaoBienDong.ReadScope);
        if (!canViewScope) return (false, new HashSet<long> { current.DonViId });
        var scope = await _scopeService.GetScopeAsync(cancellationToken);
        return scope.HasFullAccess ? (true, null) : (true, scope.AllowedDonViIds.ToHashSet());
    }

    private async Task<List<UnitRow>> ResolveSelectedUnitsAsync(
        bool managementScope,
        HashSet<long>? allowedIds,
        long? donViId,
        long fromKyBaoCaoId,
        long toKyBaoCaoId,
        CancellationToken cancellationToken)
    {
        var activeFlag = true;
        var query = _db.DonVis.AsNoTracking().Where(x => x.IsActive == activeFlag);
        if (allowedIds is not null) query = query.Where(x => allowedIds.Contains(x.Id));
        if (managementScope) query = query.Where(x => x.CapSo == ManagementUnitLevel);
        if (donViId.HasValue)
        {
            query = query.Where(x => x.Id == donViId.Value);
        }
        else
        {
            query = query.Where(x => _db.BaoCaoSnapshots.Any(snapshot =>
                snapshot.DonViId == x.Id
                && (snapshot.KyBaoCaoId == fromKyBaoCaoId || snapshot.KyBaoCaoId == toKyBaoCaoId)
                && (snapshot.TrangThai == SnapshotStatus.Locked || snapshot.TrangThai == SnapshotStatus.Submitted)));
        }
        return await query.OrderBy(x => x.TenDonVi).Select(x => new UnitRow(x.Id, x.MaDonVi, x.TenDonVi)).ToListAsync(cancellationToken);
    }

    private Task<bool> IsManagementUnitAsync(long donViId, CancellationToken cancellationToken)
    {
        var activeFlag = true;
        return _db.DonVis.AsNoTracking().AnyAsync(x =>
            x.Id == donViId && x.IsActive == activeFlag && x.CapSo == ManagementUnitLevel,
            cancellationToken);
    }

    private async Task<KyBaoCaoEntity?> FindPreviousCompatiblePeriodAsync(KyBaoCaoEntity target, CancellationToken cancellationToken)
        => await _db.KyBaoCaos.AsNoTracking().Where(x => x.MauBaoCaoId == target.MauBaoCaoId && x.Id != target.Id
                && (x.Nam < target.Nam || (x.Nam == target.Nam && (x.Quy ?? x.Thang ?? 0) < (target.Quy ?? target.Thang ?? 0))))
            .OrderByDescending(x => x.Nam).ThenByDescending(x => x.Quy).ThenByDescending(x => x.Thang).FirstOrDefaultAsync(cancellationToken);

    private async Task<List<BaoCaoSnapshotMetric>> LoadPeriodFactsAsync(long kyId, long[] selectedUnitIds, string? moduleCode, CancellationToken cancellationToken)
    {
        var query = _db.BaoCaoSnapshotMetrics.AsNoTracking().Where(x => x.KyBaoCaoId == kyId
            && (selectedUnitIds.Contains(x.SourceDonViId) || selectedUnitIds.Contains(x.ReportDonViId)));
        if (!string.IsNullOrWhiteSpace(moduleCode)) query = query.Where(x => x.ModuleCode == moduleCode);
        var rows = await query.ToListAsync(cancellationToken);
        // Một đơn vị nguồn có thể xuất hiện trong snapshot tự nộp và snapshot tổng hợp.
        // Chỉ lấy bộ chỉ số mới nhất cho mỗi nguồn/module/chỉ số để tránh cộng trùng.
        var result = new List<BaoCaoSnapshotMetric>();
        foreach (var unitId in selectedUnitIds)
        {
            var direct = rows.Where(x => x.SourceDonViId == unitId).ToArray();
            if (direct.Length > 0)
            {
                result.AddRange(direct.GroupBy(x => new { x.ModuleCode, x.MetricCode })
                    .Select(g => g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.SnapshotId).First()));
                continue;
            }

            // Đơn vị tổng hợp không nhập trực tiếp: các facts nằm theo đơn vị
            // nguồn cấp dưới. Gắn về đơn vị báo cáo để màn tự xem vẫn nhận được
            // số tổng hợp; tỷ lệ sẽ được tính lại từ tổng tử số/mẫu số.
            foreach (var fact in rows.Where(x => x.ReportDonViId == unitId))
            {
                result.Add(new BaoCaoSnapshotMetric
                {
                    SnapshotId = fact.SnapshotId,
                    SnapshotBatchId = fact.SnapshotBatchId,
                    KyBaoCaoId = fact.KyBaoCaoId,
                    ReportDonViId = fact.ReportDonViId,
                    SourceDonViId = unitId,
                    ModuleCode = fact.ModuleCode,
                    MetricCode = fact.MetricCode,
                    Value = fact.Value,
                    Numerator = fact.Numerator,
                    Denominator = fact.Denominator,
                    MetricVersion = fact.MetricVersion,
                    CreatedAt = fact.CreatedAt,
                });
            }
        }
        return result;
    }

    private static BaoCaoBienDongModuleDto BuildModule(string moduleCode, BaoCaoSnapshotMetric[] fromFacts, BaoCaoSnapshotMetric[] toFacts)
    {
        var metrics = BaoCaoMetricCatalog.ForModule(moduleCode).Select(definition =>
        {
            var from = AggregateFacts(fromFacts.Where(x => x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase) && x.MetricCode.Equals(definition.MetricCode, StringComparison.OrdinalIgnoreCase)).ToArray());
            var to = AggregateFacts(toFacts.Where(x => x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase) && x.MetricCode.Equals(definition.MetricCode, StringComparison.OrdinalIgnoreCase)).ToArray());
            decimal? delta = from.HasValue && to.HasValue ? to.Value - from.Value : null;
            decimal? deltaPercent = from.HasValue && to.HasValue && from.Value != 0 ? Math.Round(delta!.Value * 100m / Math.Abs(from.Value), 2) : null;
            return new BaoCaoBienDongMetricDto
            {
                MetricCode = definition.MetricCode, MetricName = definition.MetricName, Unit = definition.Unit,
                Direction = definition.Direction, IsPrimary = definition.IsPrimary,
                FromValue = from.HasValue ? from.Value : null, ToValue = to.HasValue ? to.Value : null,
                Delta = delta, DeltaPercent = deltaPercent,
                Status = BaoCaoChangeStatuses.Classify(from.HasValue ? from.Value : null, to.HasValue ? to.Value : null, definition.Direction),
            };
        }).ToArray();
        var moduleDefinition = BaoCaoMetricCatalog.All.First(x => x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase));
        return new BaoCaoBienDongModuleDto { ModuleCode = moduleCode, ModuleName = moduleDefinition.ModuleName, Status = ResolveRollupStatus(metrics.Select(x => x.Status)), Metrics = metrics };
    }

    private static BaoCaoHienTrangModuleDto BuildCurrentModule(string moduleCode, BaoCaoSnapshotMetric[] facts)
    {
        var definition = BaoCaoMetricCatalog.All.First(x => x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase));
        var metrics = BaoCaoMetricCatalog.ForModule(moduleCode).Select(metricDefinition =>
        {
            var aggregate = AggregateFacts(facts.Where(x =>
                x.ModuleCode.Equals(moduleCode, StringComparison.OrdinalIgnoreCase)
                && x.MetricCode.Equals(metricDefinition.MetricCode, StringComparison.OrdinalIgnoreCase)).ToArray());
            return new BaoCaoHienTrangMetricDto
            {
                MetricCode = metricDefinition.MetricCode,
                MetricName = metricDefinition.MetricName,
                Unit = metricDefinition.Unit,
                IsPrimary = metricDefinition.IsPrimary,
                Value = aggregate.HasValue ? aggregate.Value : null,
            };
        }).ToArray();
        return new BaoCaoHienTrangModuleDto
        {
            ModuleCode = moduleCode,
            ModuleName = definition.ModuleName,
            Metrics = metrics,
        };
    }

    private static (bool HasValue, decimal Value) AggregateFacts(IReadOnlyCollection<BaoCaoSnapshotMetric> facts)
    {
        if (facts.Count == 0) return (false, 0);
        if (facts.Any(x => x.Numerator.HasValue && x.Denominator.HasValue))
        {
            var numerator = facts.Sum(x => x.Numerator ?? 0); var denominator = facts.Sum(x => x.Denominator ?? 0);
            return (true, denominator <= 0 ? 0 : Math.Round(numerator * 100m / denominator, 2));
        }
        return (true, facts.Sum(x => x.Value));
    }

    private static string ResolveRollupStatus(IEnumerable<string> statuses)
    {
        var values = statuses.ToArray();
        if (values.Any(x => x == BaoCaoChangeStatuses.Worsening)) return BaoCaoChangeStatuses.Worsening;
        if (values.Any(x => x == BaoCaoChangeStatuses.Improving)) return BaoCaoChangeStatuses.Improving;
        if (values.Any(x => x == BaoCaoChangeStatuses.Changed)) return BaoCaoChangeStatuses.Changed;
        if (values.Length == 0 || values.All(x => x == BaoCaoChangeStatuses.Missing)) return BaoCaoChangeStatuses.Missing;
        return BaoCaoChangeStatuses.Stable;
    }

    private static IReadOnlyCollection<string> ParseModules(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private sealed record UnitRow(long Id, string MaDonVi, string TenDonVi);
}
