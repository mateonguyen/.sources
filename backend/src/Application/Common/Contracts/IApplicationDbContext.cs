using Microsoft.EntityFrameworkCore;
using ThucLuc.Domain.Entities.Business;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Entities.System;

namespace ThucLuc.Application.Common.Contracts;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<ApplicationRole> Roles { get; }

    DbSet<Permission> Permissions { get; }

    DbSet<RolePermission> RolePermissions { get; }

    DbSet<UserRoleAssignment> UserRoleAssignments { get; }

    DbSet<RefreshTokenSession> RefreshTokenSessions { get; }

    DbSet<DonVi> DonVis { get; }

    DbSet<Code> Codes { get; }

    DbSet<CodeValue> CodeValues { get; }

    DbSet<RefLoaiThietBi> RefLoaiThietBis { get; }

    DbSet<MauBaoCao> MauBaoCaos { get; }

    DbSet<KyBaoCao> KyBaoCaos { get; }

    DbSet<KyTrangThaiDonVi> KyTrangThaiDonVis { get; }

    DbSet<BaoCaoSnapshot> BaoCaoSnapshots { get; }

    DbSet<SnapshotBatch> SnapshotBatches { get; }

    DbSet<BaoCaoFile> BaoCaoFiles { get; }

    DbSet<FileDinhKem> FileDinhKems { get; }

    DbSet<ThongBao> ThongBaos { get; }

    DbSet<SystemLog> SystemLogs { get; }

    DbSet<YeuCauBoSung> YeuCauBoSungs { get; }

    DbSet<NhanLucCntt> NhanLucCntts { get; }

    DbSet<NhanLucCnttHis> NhanLucCnttHis { get; }

    DbSet<HeThongThongTin> HeThongThongTins { get; }

    DbSet<HeThongThongTinHis> HeThongThongTinHis { get; }

    DbSet<HtttTieuChuan> HtttTieuChuans { get; }

    DbSet<HtttTieuChuanHis> HtttTieuChuanHis { get; }

    DbSet<DuAnCntt> DuAnCntts { get; }

    DbSet<DuAnCnttHis> DuAnCnttHis { get; }

    DbSet<VanBanQppl> VanBanQppls { get; }

    DbSet<VanBanQpplHis> VanBanQpplHis { get; }

    DbSet<DaoTaoBoiDuong> DaoTaoBoiDuongs { get; }

    DbSet<DaoTaoBoiDuongHis> DaoTaoBoiDuongHis { get; }

    DbSet<DaoTaoHocVien> DaoTaoHocViens { get; }

    DbSet<DaoTaoHocVienHis> DaoTaoHocVienHis { get; }

    DbSet<NangLucSo> NangLucSos { get; }

    DbSet<NangLucSoHis> NangLucSoHis { get; }

    DbSet<ThietBiCntt> ThietBiCntts { get; }

    DbSet<ThietBiUngDung> ThietBiUngDungs { get; }

    DbSet<ThietBiCnttHis> ThietBiCnttHis { get; }

    DbSet<ThietBiUngDungHis> ThietBiUngDungHis { get; }

    DbSet<HaTangMang> HaTangMangs { get; }

    DbSet<HaTangMangHis> HaTangMangHis { get; }

    DbSet<GiamSatSoc> GiamSatSocs { get; }

    DbSet<GiamSatSocHis> GiamSatSocHis { get; }

    DbSet<GiamSatNoc> GiamSatNocs { get; }

    DbSet<GiamSatNocHis> GiamSatNocHis { get; }

    DbSet<AtttHtttVanHanh> AtttHtttVanHanhs { get; }

    DbSet<AtttHtttVanHanhHis> AtttHtttVanHanhHis { get; }

    DbSet<AtttHtttDauTu> AtttHtttDauTus { get; }

    DbSet<AtttHtttDauTuHis> AtttHtttDauTuHis { get; }

    DbSet<GiaiPhapAttt> GiaiPhapAttts { get; }

    DbSet<GiaiPhapAtttHis> GiaiPhapAtttHis { get; }

    DbSet<CameraQuanLy> CameraQuanLies { get; }

    DbSet<CameraQuanLyHis> CameraQuanLyHis { get; }

    DbSet<CameraThucTrang> CameraThucTrangs { get; }

    DbSet<CameraThucTrangHis> CameraThucTrangHis { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}