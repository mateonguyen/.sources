using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Domain.Common.Interfaces;
using ThucLuc.Domain.Entities.Business;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, long>, IApplicationDbContext
{
    private static readonly ValueConverter<bool, short> BoolToShortConverter =
        new(
            value => value ? (short)1 : (short)0,
            value => value == 1);
    private static readonly ValueConverter<bool?, short?> NullableBoolToShortConverter =
        new(
            value => value.HasValue ? (value.Value ? (short)1 : (short)0) : null,
            value => value.HasValue ? value.Value == 1 : null);
    private static readonly ValueConverter<DateOnly, DateTime> DateOnlyConverter =
        new(
            value => value.ToDateTime(TimeOnly.MinValue),
            value => DateOnly.FromDateTime(value));
    private static readonly ValueConverter<DateOnly?, DateTime?> NullableDateOnlyConverter =
        new(
            value => value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : null,
            value => value.HasValue ? DateOnly.FromDateTime(value.Value) : null);

    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DatabaseOptions _databaseOptions;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IOptions<DatabaseOptions> databaseOptions)
        : base(options)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _databaseOptions = databaseOptions.Value;
    }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<RefreshTokenSession> RefreshTokenSessions => Set<RefreshTokenSession>();
    public DbSet<DonVi> DonVis => Set<DonVi>();
    public DbSet<Code> Codes => Set<Code>();
    public DbSet<CodeValue> CodeValues => Set<CodeValue>();
    public DbSet<RefLoaiThietBi> RefLoaiThietBis => Set<RefLoaiThietBi>();
    public DbSet<MauBaoCao> MauBaoCaos => Set<MauBaoCao>();
    public DbSet<KyBaoCao> KyBaoCaos => Set<KyBaoCao>();
    public DbSet<KyTrangThaiDonVi> KyTrangThaiDonVis => Set<KyTrangThaiDonVi>();
    public DbSet<BaoCaoSnapshot> BaoCaoSnapshots => Set<BaoCaoSnapshot>();
    public DbSet<BaoCaoSnapshotXacNhan> BaoCaoSnapshotXacNhans => Set<BaoCaoSnapshotXacNhan>();
    public DbSet<SnapshotBatch> SnapshotBatches => Set<SnapshotBatch>();
    public DbSet<BaoCaoFile> BaoCaoFiles => Set<BaoCaoFile>();
    public DbSet<FileDinhKem> FileDinhKems => Set<FileDinhKem>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<YeuCauBoSung> YeuCauBoSungs => Set<YeuCauBoSung>();
    public DbSet<NhanLucCntt> NhanLucCntts => Set<NhanLucCntt>();
    public DbSet<NhanLucCnttHis> NhanLucCnttHis => Set<NhanLucCnttHis>();
    public DbSet<HeThongThongTin> HeThongThongTins => Set<HeThongThongTin>();
    public DbSet<HeThongThongTinHis> HeThongThongTinHis => Set<HeThongThongTinHis>();
    public DbSet<HtttTieuChuan> HtttTieuChuans => Set<HtttTieuChuan>();
    public DbSet<HtttTieuChuanHis> HtttTieuChuanHis => Set<HtttTieuChuanHis>();
    public DbSet<DuAnCntt> DuAnCntts => Set<DuAnCntt>();
    public DbSet<DuAnCnttHis> DuAnCnttHis => Set<DuAnCnttHis>();
    public DbSet<VanBanQppl> VanBanQppls => Set<VanBanQppl>();
    public DbSet<VanBanQpplHis> VanBanQpplHis => Set<VanBanQpplHis>();
    public DbSet<DaoTaoBoiDuong> DaoTaoBoiDuongs => Set<DaoTaoBoiDuong>();
    public DbSet<DaoTaoBoiDuongHis> DaoTaoBoiDuongHis => Set<DaoTaoBoiDuongHis>();
    public DbSet<DaoTaoHocVien> DaoTaoHocViens => Set<DaoTaoHocVien>();
    public DbSet<DaoTaoHocVienHis> DaoTaoHocVienHis => Set<DaoTaoHocVienHis>();
    public DbSet<NangLucSo> NangLucSos => Set<NangLucSo>();
    public DbSet<NangLucSoHis> NangLucSoHis => Set<NangLucSoHis>();
    public DbSet<ThietBiCntt> ThietBiCntts => Set<ThietBiCntt>();
    public DbSet<ThietBiUngDung> ThietBiUngDungs => Set<ThietBiUngDung>();
    public DbSet<ThietBiCnttHis> ThietBiCnttHis => Set<ThietBiCnttHis>();
    public DbSet<ThietBiUngDungHis> ThietBiUngDungHis => Set<ThietBiUngDungHis>();
    public DbSet<HaTangMang> HaTangMangs => Set<HaTangMang>();
    public DbSet<HaTangMangHis> HaTangMangHis => Set<HaTangMangHis>();
    public DbSet<GiamSatSoc> GiamSatSocs => Set<GiamSatSoc>();
    public DbSet<GiamSatSocHis> GiamSatSocHis => Set<GiamSatSocHis>();
    public DbSet<GiamSatNoc> GiamSatNocs => Set<GiamSatNoc>();
    public DbSet<GiamSatNocHis> GiamSatNocHis => Set<GiamSatNocHis>();
    public DbSet<AtttHtttVanHanh> AtttHtttVanHanhs => Set<AtttHtttVanHanh>();
    public DbSet<AtttHtttVanHanhHis> AtttHtttVanHanhHis => Set<AtttHtttVanHanhHis>();
    public DbSet<AtttHtttDauTu> AtttHtttDauTus => Set<AtttHtttDauTu>();
    public DbSet<AtttHtttDauTuHis> AtttHtttDauTuHis => Set<AtttHtttDauTuHis>();
    public DbSet<GiaiPhapAttt> GiaiPhapAttts => Set<GiaiPhapAttt>();
    public DbSet<GiaiPhapAtttHis> GiaiPhapAtttHis => Set<GiaiPhapAtttHis>();
    public DbSet<CameraQuanLy> CameraQuanLies => Set<CameraQuanLy>();
    public DbSet<CameraQuanLyHis> CameraQuanLyHis => Set<CameraQuanLyHis>();
    public DbSet<CameraThucTrang> CameraThucTrangs => Set<CameraThucTrang>();
    public DbSet<CameraThucTrangHis> CameraThucTrangHis => Set<CameraThucTrangHis>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(_databaseOptions.Schema);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Oracle does not have a native BOOLEAN type; map all bool properties to NUMBER(1) with int conversion
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(bool))
                {
                    property.SetColumnType("NUMBER(1)");
                    property.SetValueConverter(BoolToShortConverter);
                }
                else if (property.ClrType == typeof(bool?))
                {
                    property.SetColumnType("NUMBER(1)");
                    property.SetValueConverter(NullableBoolToShortConverter);
                }
                else if (property.ClrType == typeof(DateOnly))
                {
                    property.SetColumnType("DATE");
                    property.SetProviderClrType(typeof(DateTime));
                    property.SetValueConverter(DateOnlyConverter);
                }
                else if (property.ClrType == typeof(DateOnly?))
                {
                    property.SetColumnType("DATE");
                    property.SetProviderClrType(typeof(DateTime?));
                    property.SetValueConverter(NullableDateOnlyConverter);
                }
            }
        }

        ApplySoftDeleteFilters(builder);
        ConfigureFallbackEntities(builder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditing();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditing()
    {
        var now = _dateTimeProvider.Now;
        var currentUserId = _currentUserService.GetCurrentUser().UserId;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditableEntity)
            {
                if (entry.State == EntityState.Added)
                {
                    auditableEntity.CreatedAt = now;
                    auditableEntity.UpdatedAt = now;
                    auditableEntity.CreatedBy ??= currentUserId > 0 ? currentUserId : null;
                    auditableEntity.UpdatedBy = currentUserId > 0 ? currentUserId : null;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditableEntity.UpdatedAt = now;
                    auditableEntity.UpdatedBy = currentUserId > 0 ? currentUserId : null;
                }
            }

            if (entry.Entity is ISoftDelete softDeleteEntity && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDeleteEntity.DeletedAt = now;
                if (entry.Entity is IAuditableEntity deletedAuditable)
                {
                    deletedAuditable.UpdatedAt = now;
                    deletedAuditable.UpdatedBy = currentUserId > 0 ? currentUserId : null;
                }
            }
        }
    }

    private static void ApplySoftDeleteFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(AppDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, new object[] { builder });
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ISoftDelete
    {
        builder.Entity<TEntity>().HasQueryFilter(x => x.DeletedAt == null);
    }

    private static void ConfigureFallbackEntities(ModelBuilder builder)
    {
        builder.Entity<YeuCauBoSung>(entity =>
        {
            entity.ToTable("RPT_YEU_CAU_BO_SUNG");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LyDo).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.TuChoiLyDo).HasMaxLength(2000);
            entity.Property(x => x.CapGui).HasMaxLength(20).HasColumnName("CAP_GUI").HasDefaultValue("BO_XUONG_TINH");
        });

        builder.Entity<NhanLucCntt>(entity =>
        {
            entity.ToTable("BIZ_NHAN_LUC_CNTT");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NhanSuKey).HasMaxLength(36).IsRequired();
            entity.Property(x => x.ValidFrom).IsRequired();
            entity.Property(x => x.VersionNo).HasDefaultValue(1);
            entity.Property(x => x.HoTen).HasMaxLength(200).IsRequired();
            entity.Property(x => x.GioiTinh).HasMaxLength(10);
            entity.Property(x => x.CapBac).HasMaxLength(50);
            entity.Property(x => x.ChucVu).HasMaxLength(200);
            entity.Property(x => x.DienThoai).HasMaxLength(20);
            entity.Property(x => x.LoaiNhanLuc).HasMaxLength(20);
            entity.Property(x => x.TrinhDoCntt).HasMaxLength(50).HasColumnName("TRINH_DO_CNTT");
            entity.Property(x => x.TrinhDoLlct).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.NhanSuKey });
            entity.HasOne(x => x.DonViCongTac)
                .WithMany()
                .HasForeignKey(x => x.DonViCongTacId)
                .HasConstraintName("FK_BIZ_NLC_DV_CONG_TAC")
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NhanLucCnttHis>(entity =>
        {
            entity.ToTable("BIZ_NHAN_LUC_CNTT_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NhanSuKey).HasMaxLength(36).IsRequired();
            entity.Property(x => x.ValidFrom).IsRequired();
            entity.Property(x => x.ValidTo).IsRequired();
            entity.Property(x => x.HoTen).HasMaxLength(200).IsRequired();
            entity.Property(x => x.GioiTinh).HasMaxLength(10);
            entity.Property(x => x.CapBac).HasMaxLength(50);
            entity.Property(x => x.ChucVu).HasMaxLength(200);
            entity.Property(x => x.DienThoai).HasMaxLength(20);
            entity.Property(x => x.LoaiNhanLuc).HasMaxLength(20);
            entity.Property(x => x.TrinhDoCntt).HasMaxLength(50);
            entity.Property(x => x.TrinhDoLlct).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.NhanSuKey, x.ValidFrom, x.ValidTo });
            entity.HasIndex(x => new { x.DonViId, x.ValidTo });
        });

        builder.Entity<HeThongThongTin>(entity =>
        {
            entity.ToTable("BIZ_HE_THONG_THONG_TIN");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenPhanMem).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DonViPhatTrien).HasMaxLength(200);
            entity.Property(x => x.DonViQuanLy).HasMaxLength(300);
            entity.Property(x => x.PhamViHoatDong).HasMaxLength(500);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.Property(x => x.ValidFrom).HasColumnName("VALID_FROM");
            entity.Property(x => x.VersionNo).HasColumnName("VERSION_NO");
        });

        builder.Entity<HeThongThongTinHis>(entity =>
        {
            entity.ToTable("BIZ_HE_THONG_THONG_TIN_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenPhanMem).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DonViPhatTrien).HasMaxLength(200);
            entity.Property(x => x.DonViQuanLy).HasMaxLength(300);
            entity.Property(x => x.PhamViHoatDong).HasMaxLength(500);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.ValidFrom, x.ValidTo });
            entity.HasIndex(x => x.SourceId);
        });

        builder.Entity<HtttTieuChuan>(entity =>
        {
            entity.ToTable("BIZ_HTTT_TIEU_CHUAN");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenHeThong)
                .HasColumnName("TEN_HE_THONG")
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(x => x.Dvt).HasMaxLength(20);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.Property(x => x.ValidFrom).HasColumnName("VALID_FROM");
            entity.Property(x => x.VersionNo).HasColumnName("VERSION_NO");
        });

        builder.Entity<HtttTieuChuanHis>(entity =>
        {
            entity.ToTable("BIZ_HTTT_TIEU_CHUAN_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenHeThong)
                .HasColumnName("TEN_HE_THONG")
                .HasMaxLength(2000)
                .IsRequired();
            entity.Property(x => x.Dvt).HasMaxLength(20);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.ValidFrom, x.ValidTo });
            entity.HasIndex(x => x.SourceId);
        });

        builder.Entity<RefLoaiThietBi>(entity =>
        {
            entity.ToTable("REF_LOAI_THIET_BI");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MaLoai).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenLoai).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DuAnCntt>(entity =>
        {
            entity.ToTable("BIZ_DU_AN_CNTT");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenDuAn).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DonViChuTri).HasMaxLength(200).HasColumnName("DON_VI_CHU_TRI");
            entity.Property(x => x.TongKinhPhi).HasPrecision(18, 3).HasColumnName("TONG_KINH_PHI");
            entity.Property(x => x.NguonVon).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<DuAnCnttHis>(entity =>
        {
            entity.ToTable("BIZ_DU_AN_CNTT_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenDuAn).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DonViChuTri).HasMaxLength(200);
            entity.Property(x => x.TongKinhPhi).HasPrecision(18, 3);
            entity.Property(x => x.NguonVon).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<VanBanQppl>(entity =>
        {
            entity.ToTable("BIZ_VAN_BAN_QPPL");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SoHieu).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TenVanBan).HasMaxLength(500);
            entity.Property(x => x.LoaiVanBan).HasMaxLength(100);
            entity.Property(x => x.CoQuanBanHanh).HasMaxLength(200);
            entity.Property(x => x.LinhVuc).HasMaxLength(100);
            entity.Property(x => x.TrichYeu).HasMaxLength(2000).HasColumnName("TRICH_YEU");
            entity.Property(x => x.TinhTrangTrienKhai).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<VanBanQpplHis>(entity =>
        {
            entity.ToTable("BIZ_VAN_BAN_QPPL_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SoHieu).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TenVanBan).HasMaxLength(500);
            entity.Property(x => x.LoaiVanBan).HasMaxLength(100);
            entity.Property(x => x.CoQuanBanHanh).HasMaxLength(200);
            entity.Property(x => x.LinhVuc).HasMaxLength(100);
            entity.Property(x => x.TrichYeu).HasMaxLength(2000);
            entity.Property(x => x.TinhTrangTrienKhai).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<DaoTaoBoiDuong>(entity =>
        {
            entity.ToTable("BIZ_DAO_TAO_BOI_DUONG");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenKhoaHoc).HasMaxLength(300).IsRequired();
            entity.Property(x => x.DonViToChuc).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HinhThuc).HasMaxLength(100);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<DaoTaoBoiDuongHis>(entity =>
        {
            entity.ToTable("BIZ_DAO_TAO_BOI_DUONG_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TenKhoaHoc).HasMaxLength(300).IsRequired();
            entity.Property(x => x.DonViToChuc).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HinhThuc).HasMaxLength(100);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<DaoTaoHocVien>(entity =>
        {
            entity.ToTable("BIZ_DAO_TAO_HOC_VIEN");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nam).IsRequired();
            entity.Property(x => x.NoiDungDaoTao).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.NoiDungDaoTao });
        });

        builder.Entity<DaoTaoHocVienHis>(entity =>
        {
            entity.ToTable("BIZ_DAO_TAO_HOC_VIEN_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NoiDungDaoTao).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<NangLucSo>(entity =>
        {
            entity.ToTable("BIZ_NANG_LUC_SO");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NhomViTri).HasMaxLength(20).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.NhomViTri });
        });

        builder.Entity<NangLucSoHis>(entity =>
        {
            entity.ToTable("BIZ_NANG_LUC_SO_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NhomViTri).HasMaxLength(20).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<ThietBiCntt>(entity =>
        {
            entity.ToTable("BIZ_THIET_BI_CNTT");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenThietBi).HasMaxLength(200);
            entity.Property(x => x.HangSanXuat).HasMaxLength(100);
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.CauHinh).HasMaxLength(500);
            entity.Property(x => x.HeDieuHanh).HasMaxLength(100);
            entity.Property(x => x.DonViSuDung).HasMaxLength(200);
            entity.Property(x => x.TinhTrang).HasMaxLength(100);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.Property(x => x.ValidFrom).HasColumnName("VALID_FROM");
            entity.Property(x => x.VersionNo).HasColumnName("VERSION_NO");
            entity.HasOne(x => x.LoaiThietBi)
                .WithMany()
                .HasForeignKey(x => x.LoaiThietBiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ThietBiUngDung>(entity =>
        {
            entity.ToTable("BIZ_THIET_BI_UNG_DUNG");
            entity.HasKey(x => new { x.ThietBiId, x.HeThongId });
            entity.HasOne(x => x.ThietBi)
                .WithMany(x => x.UngDungs)
                .HasForeignKey(x => x.ThietBiId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.HeThong)
                .WithMany()
                .HasForeignKey(x => x.HeThongId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ThietBiCnttHis>(entity =>
        {
            entity.ToTable("BIZ_THIET_BI_CNTT_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenThietBi).HasMaxLength(200);
            entity.Property(x => x.HangSanXuat).HasMaxLength(100);
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.CauHinh).HasMaxLength(500);
            entity.Property(x => x.HeDieuHanh).HasMaxLength(100);
            entity.Property(x => x.DonViSuDung).HasMaxLength(200);
            entity.Property(x => x.TinhTrang).HasMaxLength(100);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.ValidFrom, x.ValidTo });
            entity.HasIndex(x => x.SourceId);
        });

        builder.Entity<ThietBiUngDungHis>(entity =>
        {
            entity.ToTable("BIZ_THIET_BI_UNG_DUNG_HIS");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SourceThietBiId, x.ValidFrom });
        });

        builder.Entity<HaTangMang>(entity =>
        {
            entity.ToTable("BIZ_HA_TANG_MANG");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<HaTangMangHis>(entity =>
        {
            entity.ToTable("BIZ_HA_TANG_MANG_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
            entity.HasIndex(x => x.SnapshotBatchId);
        });

        builder.Entity<GiamSatSoc>(entity =>
        {
            entity.ToTable("BIZ_GIAM_SAT_SOC");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LoaiMang).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LopGiamSat).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ThucTrang).HasMaxLength(50);
            entity.Property(x => x.LucLuongUngCuu).HasMaxLength(500);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<GiamSatSocHis>(entity =>
        {
            entity.ToTable("BIZ_GIAM_SAT_SOC_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LoaiMang).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LopGiamSat).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ThucTrang).HasMaxLength(50);
            entity.Property(x => x.LucLuongUngCuu).HasMaxLength(500);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<GiamSatNoc>(entity =>
        {
            entity.ToTable("BIZ_GIAM_SAT_NOC");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LopGiamSat).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ThucTrang).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<GiamSatNocHis>(entity =>
        {
            entity.ToTable("BIZ_GIAM_SAT_NOC_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LopGiamSat).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ThucTrang).HasMaxLength(50);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<AtttHtttVanHanh>(entity =>
        {
            entity.ToTable("BIZ_ATTT_HTTT_VAN_HANH");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LoaiHaTang).HasMaxLength(20);
            entity.Property(x => x.ChuQuan).HasMaxLength(200);
            entity.Property(x => x.DonViVanHanh).HasMaxLength(200);
            entity.Property(x => x.CapDoDeXuat).HasMaxLength(20);
            entity.Property(x => x.TinhTrangPheDuyet).HasMaxLength(50);
            entity.Property(x => x.QuyetDinhPheDuyet).HasMaxLength(200);
            entity.Property(x => x.QuyCheAttt).HasMaxLength(200).HasColumnName("QUY_CHE_ATTT");
            entity.Property(x => x.KiemTraDanhGia).HasMaxLength(500);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<AtttHtttVanHanhHis>(entity =>
        {
            entity.ToTable("BIZ_ATTT_HTTT_VAN_HANH_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LoaiHaTang).HasMaxLength(20);
            entity.Property(x => x.ChuQuan).HasMaxLength(200);
            entity.Property(x => x.DonViVanHanh).HasMaxLength(200);
            entity.Property(x => x.CapDoDeXuat).HasMaxLength(20);
            entity.Property(x => x.TinhTrangPheDuyet).HasMaxLength(50);
            entity.Property(x => x.QuyetDinhPheDuyet).HasMaxLength(200);
            entity.Property(x => x.QuyCheAttt).HasMaxLength(200);
            entity.Property(x => x.KiemTraDanhGia).HasMaxLength(500);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<AtttHtttDauTu>(entity =>
        {
            entity.ToTable("BIZ_ATTT_HTTT_DAU_TU");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChuQuan).HasMaxLength(200);
            entity.Property(x => x.DonViVanHanh).HasMaxLength(200);
            entity.Property(x => x.CapDoDeXuat).HasMaxLength(20);
            entity.Property(x => x.QuyetDinhPheDuyet).HasMaxLength(200);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<AtttHtttDauTuHis>(entity =>
        {
            entity.ToTable("BIZ_ATTT_HTTT_DAU_TU_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ChuQuan).HasMaxLength(200);
            entity.Property(x => x.DonViVanHanh).HasMaxLength(200);
            entity.Property(x => x.CapDoDeXuat).HasMaxLength(20);
            entity.Property(x => x.QuyetDinhPheDuyet).HasMaxLength(200);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<GiaiPhapAttt>(entity =>
        {
            entity.ToTable("BIZ_ATTT_GIAI_PHAP");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenGiaiPhap).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.TenGiaiPhap }).IsUnique();
        });

        builder.Entity<GiaiPhapAtttHis>(entity =>
        {
            entity.ToTable("BIZ_ATTT_GIAI_PHAP_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenGiaiPhap).HasMaxLength(50).IsRequired();
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
            entity.HasIndex(x => x.SnapshotBatchId);
        });

        builder.Entity<CameraQuanLy>(entity =>
        {
            entity.ToTable("BIZ_CAMERA_QUAN_LY");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NhomCamera).HasMaxLength(50);
            entity.Property(x => x.TenDonViDiaChi).HasMaxLength(300).IsRequired();
            entity.Property(x => x.KetNoiChiaSe).HasMaxLength(200);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<CameraQuanLyHis>(entity =>
        {
            entity.ToTable("BIZ_CAMERA_QUAN_LY_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NhomCamera).HasMaxLength(50);
            entity.Property(x => x.TenDonViDiaChi).HasMaxLength(300).IsRequired();
            entity.Property(x => x.KetNoiChiaSe).HasMaxLength(200);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<CameraThucTrang>(entity =>
        {
            entity.ToTable("BIZ_CAMERA_THUC_TRANG");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NhomCamera).HasMaxLength(50);
            entity.Property(x => x.TenHeThong).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ChuDauTu).HasMaxLength(200);
            entity.Property(x => x.DuongTruyen).HasMaxLength(50);
            entity.Property(x => x.PhanMem).HasMaxLength(200);
            entity.Property(x => x.LuuTru).HasMaxLength(200);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
        });

        builder.Entity<CameraThucTrangHis>(entity =>
        {
            entity.ToTable("BIZ_CAMERA_THUC_TRANG_HIS");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KyBaoCaoCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.NhomCamera).HasMaxLength(50);
            entity.Property(x => x.TenHeThong).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ChuDauTu).HasMaxLength(200);
            entity.Property(x => x.DuongTruyen).HasMaxLength(50);
            entity.Property(x => x.PhanMem).HasMaxLength(200);
            entity.Property(x => x.LuuTru).HasMaxLength(200);
            entity.Property(x => x.GhiChu).HasMaxLength(2000);
            entity.HasIndex(x => new { x.DonViId, x.KyBaoCaoCode });
        });

        builder.Entity<SnapshotBatch>(entity =>
        {
            entity.ToTable("RPT_SNAPSHOT_BATCH");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(x => new { x.KyBaoCaoId, x.DonViId, x.StartedAt });
            entity.HasOne(x => x.KyBaoCao)
                .WithMany()
                .HasForeignKey(x => x.KyBaoCaoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
