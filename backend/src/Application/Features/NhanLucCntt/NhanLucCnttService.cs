using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Common.Exceptions;
using ThucLuc.Application.Common.Models;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Business;
using NhanLucCnttEntity = ThucLuc.Domain.Entities.Business.NhanLucCntt;

namespace ThucLuc.Application.Features.NhanLucCntt;

public interface INhanLucCnttService
{
    Task<PagedResult<NhanLucCnttDto>> GetPagedAsync(NhanLucCnttQuery query, CancellationToken cancellationToken = default);

    Task<NhanLucCnttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NhanLucCnttDto>> GetTimelineAsync(string nhanSuKey, CancellationToken cancellationToken = default);

    Task<NhanLucCnttDto> UpsertAsync(long? id, UpsertNhanLucCnttRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class NhanLucCnttService : INhanLucCnttService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpsertNhanLucCnttRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public NhanLucCnttService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IValidator<UpsertNhanLucCnttRequest> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PagedResult<NhanLucCnttDto>> GetPagedAsync(NhanLucCnttQuery query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeQuery(query);

        // For as-of queries we need both live and HIS tables; for current view only live is needed.
        if (normalizedQuery.AsOfDate.HasValue)
        {
            return await GetPagedAsOfAsync(normalizedQuery, cancellationToken);
        }

        var scopedQuery =
            from nhanLuc in ApplyReadScope(_dbContext.NhanLucCntts).AsNoTracking()
            join donVi in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViId equals donVi.Id into donViGroup
            from donVi in donViGroup.DefaultIfEmpty()
            join donViCongTac in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViCongTacId equals (long?)donViCongTac.Id into donViCongTacGroup
            from donViCongTac in donViCongTacGroup.DefaultIfEmpty()
            select new
            {
                NhanLuc = nhanLuc,
                DonViTen = donVi != null ? donVi.TenDonVi : null,
                DonViCongTacTen = donViCongTac != null ? donViCongTac.TenDonVi : null
            };

        if (!string.IsNullOrWhiteSpace(normalizedQuery.TuKhoa))
        {
            var keyword = $"%{normalizedQuery.TuKhoa.Trim()}%";
            scopedQuery = scopedQuery.Where(x =>
                EF.Functions.Like(x.NhanLuc.HoTen, keyword) ||
                (x.NhanLuc.DienThoai != null && EF.Functions.Like(x.NhanLuc.DienThoai, keyword)) ||
                (x.NhanLuc.ChucVu != null && EF.Functions.Like(x.NhanLuc.ChucVu, keyword)) ||
                (x.DonViTen != null && EF.Functions.Like(x.DonViTen, keyword)) ||
                (x.DonViCongTacTen != null && EF.Functions.Like(x.DonViCongTacTen, keyword)));
        }

        if (normalizedQuery.DonViCongTacId.HasValue)
        {
            scopedQuery = scopedQuery.Where(x =>
                x.NhanLuc.DonViId == normalizedQuery.DonViCongTacId.Value ||
                x.NhanLuc.DonViCongTacId == normalizedQuery.DonViCongTacId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.GioiTinh))
        {
            scopedQuery = scopedQuery.Where(x => x.NhanLuc.GioiTinh == normalizedQuery.GioiTinh);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.CapBac))
        {
            scopedQuery = scopedQuery.Where(x => x.NhanLuc.CapBac == normalizedQuery.CapBac);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.LoaiNhanLuc))
        {
            scopedQuery = scopedQuery.Where(x => x.NhanLuc.LoaiNhanLuc == normalizedQuery.LoaiNhanLuc);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery.TrinhDoCntt))
        {
            scopedQuery = scopedQuery.Where(x => x.NhanLuc.TrinhDoCntt == normalizedQuery.TrinhDoCntt);
        }

        var totalItems = await scopedQuery.CountAsync(cancellationToken);
        var rows = await scopedQuery
            .OrderBy(x => x.NhanLuc.HoTen)
            .ThenBy(x => x.NhanLuc.Id)
            .Skip((normalizedQuery.Page - 1) * normalizedQuery.PageSize)
            .Take(normalizedQuery.PageSize)
            .Select(x => new
            {
                x.NhanLuc.Id,
                x.NhanLuc.NhanSuKey,
                x.NhanLuc.DonViId,
                x.DonViTen,
                x.NhanLuc.DonViCongTacId,
                x.DonViCongTacTen,
                x.NhanLuc.HoTen,
                x.NhanLuc.NgaySinh,
                x.NhanLuc.GioiTinh,
                x.NhanLuc.CapBac,
                x.NhanLuc.ChucVu,
                x.NhanLuc.DienThoai,
                x.NhanLuc.LoaiNhanLuc,
                x.NhanLuc.TrinhDoCntt,
                x.NhanLuc.TrinhDoLlct,
                x.NhanLuc.GhiChu,
                x.NhanLuc.ValidFrom,
                x.NhanLuc.VersionNo
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new NhanLucCnttDto
        {
            Id = x.Id,
            NhanSuKey = x.NhanSuKey,
            DonViId = x.DonViId,
            DonViTen = x.DonViTen,
            DonViCongTacId = x.DonViCongTacId,
            DonViCongTacTen = x.DonViCongTacTen,
            HoTen = x.HoTen,
            NgaySinh = x.NgaySinh,
            GioiTinh = x.GioiTinh,
            CapBac = x.CapBac,
            ChucVu = x.ChucVu,
            DienThoai = x.DienThoai,
            LoaiNhanLuc = x.LoaiNhanLuc,
            TrinhDoCntt = x.TrinhDoCntt,
            TrinhDoLlct = x.TrinhDoLlct,
            GhiChu = x.GhiChu,
            NamBaoCao = null,
            ValidFrom = x.ValidFrom,
            ValidTo = null,
            IsCurrent = true,
            VersionNo = x.VersionNo
        }).ToList();

        return new PagedResult<NhanLucCnttDto>
        {
            Items = items,
            Page = normalizedQuery.Page,
            PageSize = normalizedQuery.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<NhanLucCnttDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => await (
            from nhanLuc in ApplyReadScope(_dbContext.NhanLucCntts).AsNoTracking()
            join donVi in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViId equals donVi.Id into donViGroup
            from donVi in donViGroup.DefaultIfEmpty()
            join donViCongTac in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViCongTacId equals (long?)donViCongTac.Id into donViCongTacGroup
            from donViCongTac in donViCongTacGroup.DefaultIfEmpty()
            where nhanLuc.Id == id
            select new NhanLucCnttDto
            {
                Id = nhanLuc.Id,
                NhanSuKey = nhanLuc.NhanSuKey,
                DonViId = nhanLuc.DonViId,
                DonViTen = donVi != null ? donVi.TenDonVi : null,
                DonViCongTacId = nhanLuc.DonViCongTacId,
                DonViCongTacTen = donViCongTac != null ? donViCongTac.TenDonVi : null,
                HoTen = nhanLuc.HoTen,
                NgaySinh = nhanLuc.NgaySinh,
                GioiTinh = nhanLuc.GioiTinh,
                CapBac = nhanLuc.CapBac,
                ChucVu = nhanLuc.ChucVu,
                DienThoai = nhanLuc.DienThoai,
                LoaiNhanLuc = nhanLuc.LoaiNhanLuc,
                TrinhDoCntt = nhanLuc.TrinhDoCntt,
                TrinhDoLlct = nhanLuc.TrinhDoLlct,
                GhiChu = nhanLuc.GhiChu,
                NamBaoCao = null,
                ValidFrom = nhanLuc.ValidFrom,
                ValidTo = null,
                IsCurrent = true,
                VersionNo = nhanLuc.VersionNo
            })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<NhanLucCnttDto>> GetTimelineAsync(string nhanSuKey, CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeRequiredKey(nhanSuKey);

        // Current version from live table
        var liveItems = await (
                from nhanLuc in ApplyReadScope(_dbContext.NhanLucCntts).AsNoTracking()
                join donVi in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViId equals donVi.Id into donViGroup
                from donVi in donViGroup.DefaultIfEmpty()
                join donViCongTac in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViCongTacId equals (long?)donViCongTac.Id into donViCongTacGroup
                from donViCongTac in donViCongTacGroup.DefaultIfEmpty()
                where (nhanLuc.NhanSuKey ?? string.Empty).ToUpper() == normalizedKey
                select new NhanLucCnttDto
                {
                    Id = nhanLuc.Id,
                    NhanSuKey = nhanLuc.NhanSuKey,
                    DonViId = nhanLuc.DonViId,
                    DonViTen = donVi != null ? donVi.TenDonVi : null,
                    DonViCongTacId = nhanLuc.DonViCongTacId,
                    DonViCongTacTen = donViCongTac != null ? donViCongTac.TenDonVi : null,
                    HoTen = nhanLuc.HoTen,
                    NgaySinh = nhanLuc.NgaySinh,
                    GioiTinh = nhanLuc.GioiTinh,
                    CapBac = nhanLuc.CapBac,
                    ChucVu = nhanLuc.ChucVu,
                    DienThoai = nhanLuc.DienThoai,
                    LoaiNhanLuc = nhanLuc.LoaiNhanLuc,
                    TrinhDoCntt = nhanLuc.TrinhDoCntt,
                    TrinhDoLlct = nhanLuc.TrinhDoLlct,
                    GhiChu = nhanLuc.GhiChu,
                    NamBaoCao = null,
                    ValidFrom = nhanLuc.ValidFrom,
                    ValidTo = null,
                    IsCurrent = true,
                    VersionNo = nhanLuc.VersionNo
                })
            .ToListAsync(cancellationToken);

        // Historical versions from _HIS table
        var hisItems = await (
                from his in _dbContext.NhanLucCnttHis.AsNoTracking()
                join donVi in _dbContext.DonVis.AsNoTracking() on his.DonViId equals donVi.Id into donViGroup
                from donVi in donViGroup.DefaultIfEmpty()
                join donViCongTac in _dbContext.DonVis.AsNoTracking() on his.DonViCongTacId equals (long?)donViCongTac.Id into donViCongTacGroup
                from donViCongTac in donViCongTacGroup.DefaultIfEmpty()
                where (his.NhanSuKey ?? string.Empty).ToUpper() == normalizedKey
                select new NhanLucCnttDto
                {
                    Id = his.Id,
                    NhanSuKey = his.NhanSuKey,
                    DonViId = his.DonViId,
                    DonViTen = donVi != null ? donVi.TenDonVi : null,
                    DonViCongTacId = his.DonViCongTacId,
                    DonViCongTacTen = donViCongTac != null ? donViCongTac.TenDonVi : null,
                    HoTen = his.HoTen,
                    NgaySinh = his.NgaySinh,
                    GioiTinh = his.GioiTinh,
                    CapBac = his.CapBac,
                    ChucVu = his.ChucVu,
                    DienThoai = his.DienThoai,
                    LoaiNhanLuc = his.LoaiNhanLuc,
                    TrinhDoCntt = his.TrinhDoCntt,
                    TrinhDoLlct = his.TrinhDoLlct,
                    GhiChu = his.GhiChu,
                    NamBaoCao = null,
                    ValidFrom = his.ValidFrom,
                    ValidTo = his.ValidTo,
                    IsCurrent = false,
                    VersionNo = his.VersionNo
                })
            .ToListAsync(cancellationToken);

        return liveItems
            .Concat(hisItems)
            .OrderByDescending(x => x.VersionNo)
            .ThenByDescending(x => x.ValidFrom)
            .ToList();
    }

    public async Task<NhanLucCnttDto> UpsertAsync(long? id, UpsertNhanLucCnttRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureValidScopeAsync(request.DonViId, request.DonViCongTacId, cancellationToken);

        var now = _dateTimeProvider.Now;
        NhanLucCnttEntity targetEntity;
        if (id.HasValue)
        {
            var currentEntity = await ApplyReadScope(_dbContext.NhanLucCntts)
                .FirstOrDefaultAsync(x => x.Id == id.Value, cancellationToken)
                ?? throw new AppException("NHANLUC_NOT_FOUND", "Không tìm thấy nhân lực CNTT.", 404);

            if (!HasAnyChange(currentEntity, request))
            {
                return await GetByIdAsync(currentEntity.Id, cancellationToken) ?? throw new InvalidOperationException();
            }

            if (ShouldCreateNewVersion(currentEntity, request))
            {
                // Archive current version to _HIS before updating
                var hisEntry = new NhanLucCnttHis
                {
                    SourceId = currentEntity.Id,
                    NhanSuKey = currentEntity.NhanSuKey,
                    DonViId = currentEntity.DonViId,
                    DonViCongTacId = currentEntity.DonViCongTacId,
                    HoTen = currentEntity.HoTen,
                    NgaySinh = currentEntity.NgaySinh,
                    GioiTinh = currentEntity.GioiTinh,
                    CapBac = currentEntity.CapBac,
                    ChucVu = currentEntity.ChucVu,
                    DienThoai = currentEntity.DienThoai,
                    LoaiNhanLuc = currentEntity.LoaiNhanLuc,
                    TrinhDoCntt = currentEntity.TrinhDoCntt,
                    TrinhDoLlct = currentEntity.TrinhDoLlct,
                    GhiChu = currentEntity.GhiChu,
                    ValidFrom = currentEntity.ValidFrom,
                    ValidTo = now,
                    VersionNo = currentEntity.VersionNo
                };
                await _dbContext.NhanLucCnttHis.AddAsync(hisEntry, cancellationToken);

                // Update live record in-place with the new version
                currentEntity.DonViId = request.DonViId;
                currentEntity.DonViCongTacId = request.DonViCongTacId;
                currentEntity.HoTen = request.HoTen;
                currentEntity.NgaySinh = request.NgaySinh;
                currentEntity.GioiTinh = request.GioiTinh;
                currentEntity.CapBac = request.CapBac;
                currentEntity.ChucVu = request.ChucVu;
                currentEntity.DienThoai = request.DienThoai;
                currentEntity.LoaiNhanLuc = request.LoaiNhanLuc;
                currentEntity.TrinhDoCntt = request.TrinhDoCntt;
                currentEntity.TrinhDoLlct = request.TrinhDoLlct;
                currentEntity.GhiChu = request.GhiChu;
                currentEntity.ValidFrom = now;
                currentEntity.VersionNo = currentEntity.VersionNo + 1;
            }
            else
            {
                ApplyInPlaceChanges(currentEntity, request);
            }

            targetEntity = currentEntity;
        }
        else
        {
            targetEntity = CreateNewRecord(request);
            targetEntity.NhanSuKey = Guid.NewGuid().ToString("N");
            targetEntity.VersionNo = 1;
            targetEntity.ValidFrom = now;
            await _dbContext.NhanLucCntts.AddAsync(targetEntity, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(targetEntity.Id, cancellationToken) ?? throw new InvalidOperationException();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await ApplyReadScope(_dbContext.NhanLucCntts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new AppException("NHANLUC_NOT_FOUND", "Không tìm thấy nhân lực CNTT.", 404);

        var now = _dateTimeProvider.Now;
        var hisEntry = new NhanLucCnttHis
        {
            SourceId = entity.Id,
            NhanSuKey = entity.NhanSuKey,
            DonViId = entity.DonViId,
            DonViCongTacId = entity.DonViCongTacId,
            HoTen = entity.HoTen,
            NgaySinh = entity.NgaySinh,
            GioiTinh = entity.GioiTinh,
            CapBac = entity.CapBac,
            ChucVu = entity.ChucVu,
            DienThoai = entity.DienThoai,
            LoaiNhanLuc = entity.LoaiNhanLuc,
            TrinhDoCntt = entity.TrinhDoCntt,
            TrinhDoLlct = entity.TrinhDoLlct,
            GhiChu = entity.GhiChu,
            ValidFrom = entity.ValidFrom,
            ValidTo = now,
            VersionNo = entity.VersionNo
        };

        await _dbContext.NhanLucCnttHis.AddAsync(hisEntry, cancellationToken);

        _dbContext.NhanLucCntts.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<NhanLucCnttEntity> ApplyReadScope(IQueryable<NhanLucCnttEntity> query)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0)
        {
            query = query.Where(x => x.DonViId == currentUser.DonViId);
        }

        return query;
    }

    private static NhanLucCnttQuery NormalizeQuery(NhanLucCnttQuery? query)
    {
        var page = query?.Page ?? 1;
        var pageSize = query?.PageSize ?? 10;

        return new NhanLucCnttQuery
        {
            TuKhoa = query?.TuKhoa,
            NamBaoCao = query?.NamBaoCao,
            AsOfDate = query?.AsOfDate,
            DonViCongTacId = query?.DonViCongTacId,
            GioiTinh = query?.GioiTinh,
            CapBac = query?.CapBac,
            LoaiNhanLuc = query?.LoaiNhanLuc,
            TrinhDoCntt = query?.TrinhDoCntt,
            Page = page > 0 ? page : 1,
            PageSize = pageSize > 0 ? Math.Min(pageSize, 100) : 10
        };
    }

    private async Task<PagedResult<NhanLucCnttDto>> GetPagedAsOfAsync(NhanLucCnttQuery normalizedQuery, CancellationToken cancellationToken)
    {
        var asOf = normalizedQuery.AsOfDate!.Value;

        // Live records whose current version was established before or on asOf
        var liveQuery =
            from nhanLuc in ApplyReadScope(_dbContext.NhanLucCntts).AsNoTracking()
            where nhanLuc.ValidFrom <= asOf
            join donVi in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViId equals donVi.Id into donViGroup
            from donVi in donViGroup.DefaultIfEmpty()
            join donViCongTac in _dbContext.DonVis.AsNoTracking() on nhanLuc.DonViCongTacId equals (long?)donViCongTac.Id into donViCongTacGroup
            from donViCongTac in donViCongTacGroup.DefaultIfEmpty()
            select new NhanLucCnttDto
            {
                Id = nhanLuc.Id,
                NhanSuKey = nhanLuc.NhanSuKey,
                DonViId = nhanLuc.DonViId,
                DonViTen = donVi != null ? donVi.TenDonVi : null,
                DonViCongTacId = nhanLuc.DonViCongTacId,
                DonViCongTacTen = donViCongTac != null ? donViCongTac.TenDonVi : null,
                HoTen = nhanLuc.HoTen,
                NgaySinh = nhanLuc.NgaySinh,
                GioiTinh = nhanLuc.GioiTinh,
                CapBac = nhanLuc.CapBac,
                ChucVu = nhanLuc.ChucVu,
                DienThoai = nhanLuc.DienThoai,
                LoaiNhanLuc = nhanLuc.LoaiNhanLuc,
                TrinhDoCntt = nhanLuc.TrinhDoCntt,
                TrinhDoLlct = nhanLuc.TrinhDoLlct,
                GhiChu = nhanLuc.GhiChu,
                NamBaoCao = null,
                ValidFrom = nhanLuc.ValidFrom,
                ValidTo = null,
                IsCurrent = true,
                VersionNo = nhanLuc.VersionNo
            };

        var liveItems = await liveQuery.ToListAsync(cancellationToken);
        var liveKeys = liveItems.Select(x => x.NhanSuKey).ToHashSet();

        // HIS records active at asOf for personnel whose current version was bumped after asOf
        var hisItems = await (
                from his in _dbContext.NhanLucCnttHis.AsNoTracking()
                where his.ValidFrom <= asOf && his.ValidTo > asOf && !liveKeys.Contains(his.NhanSuKey)
                join donVi in _dbContext.DonVis.AsNoTracking() on his.DonViId equals donVi.Id into donViGroup
                from donVi in donViGroup.DefaultIfEmpty()
                join donViCongTac in _dbContext.DonVis.AsNoTracking() on his.DonViCongTacId equals (long?)donViCongTac.Id into donViCongTacGroup
                from donViCongTac in donViCongTacGroup.DefaultIfEmpty()
                select new NhanLucCnttDto
                {
                    Id = his.Id,
                    NhanSuKey = his.NhanSuKey,
                    DonViId = his.DonViId,
                    DonViTen = donVi != null ? donVi.TenDonVi : null,
                    DonViCongTacId = his.DonViCongTacId,
                    DonViCongTacTen = donViCongTac != null ? donViCongTac.TenDonVi : null,
                    HoTen = his.HoTen,
                    NgaySinh = his.NgaySinh,
                    GioiTinh = his.GioiTinh,
                    CapBac = his.CapBac,
                    ChucVu = his.ChucVu,
                    DienThoai = his.DienThoai,
                    LoaiNhanLuc = his.LoaiNhanLuc,
                    TrinhDoCntt = his.TrinhDoCntt,
                    TrinhDoLlct = his.TrinhDoLlct,
                    GhiChu = his.GhiChu,
                    NamBaoCao = null,
                    ValidFrom = his.ValidFrom,
                    ValidTo = his.ValidTo,
                    IsCurrent = false,
                    VersionNo = his.VersionNo
                })
            .ToListAsync(cancellationToken);

        var allItems = liveItems.Concat(hisItems).AsQueryable();
        allItems = ApplyInMemoryFilters(allItems, normalizedQuery);

        var ordered = allItems
            .OrderBy(x => x.HoTen)
            .ThenBy(x => x.Id)
            .ToList();

        return new PagedResult<NhanLucCnttDto>
        {
            Items = ordered
                .Skip((normalizedQuery.Page - 1) * normalizedQuery.PageSize)
                .Take(normalizedQuery.PageSize)
                .ToList(),
            Page = normalizedQuery.Page,
            PageSize = normalizedQuery.PageSize,
            TotalItems = ordered.Count
        };
    }

    private static IQueryable<NhanLucCnttDto> ApplyInMemoryFilters(IQueryable<NhanLucCnttDto> query, NhanLucCnttQuery filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.TuKhoa))
        {
            var keyword = filter.TuKhoa.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.HoTen.ToUpperInvariant().Contains(keyword) ||
                (x.DienThoai != null && x.DienThoai.Contains(keyword)) ||
                (x.ChucVu != null && x.ChucVu.ToUpperInvariant().Contains(keyword)) ||
                (x.DonViTen != null && x.DonViTen.ToUpperInvariant().Contains(keyword)) ||
                (x.DonViCongTacTen != null && x.DonViCongTacTen.ToUpperInvariant().Contains(keyword)));
        }

        if (filter.DonViCongTacId.HasValue)
        {
            query = query.Where(x =>
                x.DonViId == filter.DonViCongTacId.Value ||
                x.DonViCongTacId == filter.DonViCongTacId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.GioiTinh))
            query = query.Where(x => x.GioiTinh == filter.GioiTinh);

        if (!string.IsNullOrWhiteSpace(filter.CapBac))
            query = query.Where(x => x.CapBac == filter.CapBac);

        if (!string.IsNullOrWhiteSpace(filter.LoaiNhanLuc))
            query = query.Where(x => x.LoaiNhanLuc == filter.LoaiNhanLuc);

        if (!string.IsNullOrWhiteSpace(filter.TrinhDoCntt))
            query = query.Where(x => x.TrinhDoCntt == filter.TrinhDoCntt);

        return query;
    }

    private static NhanLucCnttEntity CreateNewRecord(UpsertNhanLucCnttRequest request)
        => new()
        {
            DonViId = request.DonViId,
            DonViCongTacId = request.DonViCongTacId,
            HoTen = request.HoTen,
            NgaySinh = request.NgaySinh,
            GioiTinh = request.GioiTinh,
            CapBac = request.CapBac,
            ChucVu = request.ChucVu,
            DienThoai = request.DienThoai,
            LoaiNhanLuc = request.LoaiNhanLuc,
            TrinhDoCntt = request.TrinhDoCntt,
            TrinhDoLlct = request.TrinhDoLlct,
            GhiChu = request.GhiChu
        };

    private static void ApplyInPlaceChanges(NhanLucCnttEntity target, UpsertNhanLucCnttRequest request)
    {
        target.DonViId = request.DonViId;
        target.DonViCongTacId = request.DonViCongTacId;
        target.HoTen = request.HoTen;
        target.NgaySinh = request.NgaySinh;
        target.GioiTinh = request.GioiTinh;
        target.CapBac = request.CapBac;
        target.ChucVu = request.ChucVu;
        target.DienThoai = request.DienThoai;
        target.LoaiNhanLuc = request.LoaiNhanLuc;
        target.TrinhDoCntt = request.TrinhDoCntt;
        target.TrinhDoLlct = request.TrinhDoLlct;
        target.GhiChu = request.GhiChu;
    }

    private static bool ShouldCreateNewVersion(NhanLucCnttEntity current, UpsertNhanLucCnttRequest request)
        => current.DonViCongTacId != request.DonViCongTacId
           || !string.Equals(current.ChucVu, request.ChucVu, StringComparison.Ordinal)
           || !string.Equals(current.CapBac, request.CapBac, StringComparison.Ordinal)
           || !string.Equals(current.LoaiNhanLuc, request.LoaiNhanLuc, StringComparison.Ordinal)
           || !string.Equals(current.TrinhDoCntt, request.TrinhDoCntt, StringComparison.Ordinal);

    private static bool HasAnyChange(NhanLucCnttEntity current, UpsertNhanLucCnttRequest request)
        => current.DonViId != request.DonViId
           || current.DonViCongTacId != request.DonViCongTacId
           || !string.Equals(current.HoTen, request.HoTen, StringComparison.Ordinal)
           || current.NgaySinh != request.NgaySinh
           || !string.Equals(current.GioiTinh, request.GioiTinh, StringComparison.Ordinal)
           || !string.Equals(current.CapBac, request.CapBac, StringComparison.Ordinal)
           || !string.Equals(current.ChucVu, request.ChucVu, StringComparison.Ordinal)
           || !string.Equals(current.DienThoai, request.DienThoai, StringComparison.Ordinal)
           || !string.Equals(current.LoaiNhanLuc, request.LoaiNhanLuc, StringComparison.Ordinal)
           || !string.Equals(current.TrinhDoCntt, request.TrinhDoCntt, StringComparison.Ordinal)
           || !string.Equals(current.TrinhDoLlct, request.TrinhDoLlct, StringComparison.Ordinal)
           || !string.Equals(current.GhiChu, request.GhiChu, StringComparison.Ordinal);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeRequiredKey(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppException("NHAN_SU_KEY_REQUIRED", "NhanSuKey là bắt buộc.", 400);
        }

        return normalized.Trim().ToUpperInvariant();
    }

    private async Task EnsureValidScopeAsync(long donViId, long? donViCongTacId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (!HasCrossDonViPermission(currentUser) && currentUser.DonViId > 0 && currentUser.DonViId != donViId)
        {
            throw new AppException("NHANLUC_SCOPE_DENIED", "Không có quyền thao tác dữ liệu nhân lực của đơn vị khác.", 403);
        }

        var donViExists = await _dbContext.DonVis.CountAsync(x => x.Id == donViId, cancellationToken) > 0;
        if (!donViExists)
        {
            throw new AppException("DONVI_NOT_FOUND", "Không tìm thấy đơn vị.", 404);
        }

        if (donViCongTacId.HasValue)
        {
            var donViCongTac = await _dbContext.DonVis
                .Where(x => x.Id == donViCongTacId.Value)
                .Select(x => new { x.Id, x.ParentId })
                .FirstOrDefaultAsync(cancellationToken);

            if (donViCongTac is null)
            {
                throw new AppException("DONVI_CONG_TAC_NOT_FOUND", "Không tìm thấy đơn vị công tác.", 404);
            }

            if (donViCongTac.Id != donViId && donViCongTac.ParentId != donViId)
            {
                throw new AppException("DONVI_CONG_TAC_INVALID", "Đơn vị công tác không thuộc phạm vi đơn vị báo cáo.", 400);
            }
        }
    }

    private static bool HasCrossDonViPermission(CurrentUserProfile currentUser)
        => currentUser.HasPermission(Permissions.SystemAdmin) || currentUser.HasPermission(Permissions.KyBaoCao.Approve);
}

public sealed class UpsertNhanLucCnttRequestValidator : AbstractValidator<UpsertNhanLucCnttRequest>
{
    public UpsertNhanLucCnttRequestValidator()
    {
        RuleFor(x => x.DonViId).GreaterThan(0);
        RuleFor(x => x.DonViCongTacId).GreaterThan(0).When(x => x.DonViCongTacId.HasValue);
        RuleFor(x => x.HoTen).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GioiTinh).MaximumLength(10);
        RuleFor(x => x.CapBac).MaximumLength(50);
        RuleFor(x => x.ChucVu).MaximumLength(200);
        RuleFor(x => x.DienThoai).MaximumLength(20);
        RuleFor(x => x.LoaiNhanLuc).MaximumLength(20);
        RuleFor(x => x.TrinhDoCntt).MaximumLength(50);
        RuleFor(x => x.TrinhDoLlct).MaximumLength(50);
        RuleFor(x => x.GhiChu).MaximumLength(2000);
    }
}
