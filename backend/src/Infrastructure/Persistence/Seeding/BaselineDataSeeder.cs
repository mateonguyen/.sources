using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ThucLuc.Application.Common.Contracts;
using ThucLuc.Application.Security;
using ThucLuc.Domain.Entities.Identity;
using ThucLuc.Domain.Entities.Reporting;
using ThucLuc.Domain.Entities.System;
using ThucLuc.Domain.Enums;
using ThucLuc.Infrastructure.Options;

namespace ThucLuc.Infrastructure.Persistence.Seeding;

public sealed class BaselineDataSeeder : IBaselineDataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly SeedOptions _seedOptions;
    private readonly ILogger<BaselineDataSeeder> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BaselineDataSeeder(
        AppDbContext dbContext,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IOptions<SeedOptions> seedOptions,
        ILogger<BaselineDataSeeder> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _seedOptions = seedOptions.Value;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (_seedOptions.ResetBeforeSeed)
        {
            await ResetAsync(cancellationToken);
        }

        await SeedDonViAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedRolePermissionsAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedUsersAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedUserRoleAssignmentsAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedMauBaoCaoAsync(cancellationToken);
        await SeedKyBaoCaoAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await SeedKyTrangThaiDonViAsync(cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Baseline seed data has been applied successfully.");
    }

    private async Task ResetAsync(CancellationToken cancellationToken)
    {
        _dbContext.RolePermissions.RemoveRange(await _dbContext.RolePermissions.ToListAsync(cancellationToken));
        _dbContext.UserRoleAssignments.RemoveRange(await _dbContext.UserRoleAssignments.ToListAsync(cancellationToken));
        _dbContext.Permissions.RemoveRange(await _dbContext.Permissions.ToListAsync(cancellationToken));
        _dbContext.Roles.RemoveRange(await _dbContext.Roles.ToListAsync(cancellationToken));
        _dbContext.Users.RemoveRange(await _dbContext.Users.ToListAsync(cancellationToken));
        _dbContext.KyTrangThaiDonVis.RemoveRange(await _dbContext.KyTrangThaiDonVis.ToListAsync(cancellationToken));
        _dbContext.KyBaoCaos.RemoveRange(await _dbContext.KyBaoCaos.ToListAsync(cancellationToken));
        _dbContext.MauBaoCaos.RemoveRange(await _dbContext.MauBaoCaos.ToListAsync(cancellationToken));
        _dbContext.DonVis.RemoveRange(await _dbContext.DonVis.ToListAsync(cancellationToken));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedDonViAsync(CancellationToken cancellationToken)
    {
        await UpsertDonViAsync(2001, "H05", "Cuc CNTT H05", "H05", null, "CUC", "http://h05-noibo.local", "https://h05.bocongan.gov.vn", 1200, cancellationToken);
        await UpsertDonViAsync(2002, "CA_HN", "Cong an TP Ha Noi", "CAHN", 2001, "TINH", "http://cahn-noibo.local", "https://congan.hanoi.gov.vn", 850, cancellationToken);
        await UpsertDonViAsync(2003, "CA_DN", "Cong an TP Da Nang", "CADN", 2001, "TINH", "http://cadn-noibo.local", "https://congan.danang.gov.vn", 640, cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var baselinePermissions = new[]
        {
            Permissions.Auth.Me,
            Permissions.DonVi.Read,
            Permissions.DonVi.Create,
            Permissions.DonVi.Update,
            Permissions.DonVi.Delete,
            Permissions.KyBaoCao.Read,
            Permissions.KyBaoCao.Create,
            Permissions.KyBaoCao.Approve,
            Permissions.MauBaoCao.Read,
            Permissions.MauBaoCao.Create,
            Permissions.MauBaoCao.Update,
            Permissions.MauBaoCao.Delete,
            Permissions.BaoCaoSnapshot.Create,
            Permissions.BaoCaoSnapshot.Read,
            Permissions.BaoCaoSnapshot.Update,
            Permissions.BaoCaoSnapshot.Submit,
            Permissions.BaoCaoSnapshot.Export,
            Permissions.YeuCauBoSung.Read,
            Permissions.YeuCauBoSung.Create,
            Permissions.YeuCauBoSung.Approve,
            Permissions.ThongBao.Read,
            Permissions.ThongBao.Update,
            Permissions.Files.Read,
            Permissions.Files.Delete,
            Permissions.NhanLucCntt.Read,
            Permissions.NhanLucCntt.Create,
            Permissions.NhanLucCntt.Update,
            Permissions.NhanLucCntt.Delete,
            Permissions.HeThongThongTin.Read,
            Permissions.HeThongThongTin.Create,
            Permissions.HeThongThongTin.Update,
            Permissions.HeThongThongTin.Delete,
            Permissions.DuAnCntt.Read,
            Permissions.DuAnCntt.Create,
            Permissions.DuAnCntt.Update,
            Permissions.DuAnCntt.Delete,
            Permissions.VanBanQppl.Read,
            Permissions.VanBanQppl.Create,
            Permissions.VanBanQppl.Update,
            Permissions.VanBanQppl.Delete,
            Permissions.DaoTaoBoiDuong.Read,
            Permissions.DaoTaoBoiDuong.Create,
            Permissions.DaoTaoBoiDuong.Update,
            Permissions.DaoTaoBoiDuong.Delete,
            Permissions.DaoTaoHocVien.Read,
            Permissions.DaoTaoHocVien.Create,
            Permissions.DaoTaoHocVien.Update,
            Permissions.DaoTaoHocVien.Delete,
            Permissions.NangLucSo.Read,
            Permissions.NangLucSo.Create,
            Permissions.NangLucSo.Update,
            Permissions.NangLucSo.Delete,
            Permissions.ThietBiCntt.Read,
            Permissions.ThietBiCntt.Create,
            Permissions.ThietBiCntt.Update,
            Permissions.ThietBiCntt.Delete,
            Permissions.HaTangMang.Read,
            Permissions.HaTangMang.Create,
            Permissions.HaTangMang.Update,
            Permissions.HaTangMang.Delete,
            Permissions.GiamSatSoc.Read,
            Permissions.GiamSatSoc.Create,
            Permissions.GiamSatSoc.Update,
            Permissions.GiamSatSoc.Delete,
            Permissions.GiamSatNoc.Read,
            Permissions.GiamSatNoc.Create,
            Permissions.GiamSatNoc.Update,
            Permissions.GiamSatNoc.Delete,
            Permissions.AtttHtttVanHanh.Read,
            Permissions.AtttHtttVanHanh.Create,
            Permissions.AtttHtttVanHanh.Update,
            Permissions.AtttHtttVanHanh.Delete,
            Permissions.AtttHtttDauTu.Read,
            Permissions.AtttHtttDauTu.Create,
            Permissions.AtttHtttDauTu.Update,
            Permissions.AtttHtttDauTu.Delete,
            Permissions.GiaiPhapAttt.Read,
            Permissions.GiaiPhapAttt.Create,
            Permissions.GiaiPhapAttt.Update,
            Permissions.GiaiPhapAttt.Delete,
            Permissions.CameraQuanLy.Read,
            Permissions.CameraQuanLy.Create,
            Permissions.CameraQuanLy.Update,
            Permissions.CameraQuanLy.Delete,
            Permissions.CameraThucTrang.Read,
            Permissions.CameraThucTrang.Create,
            Permissions.CameraThucTrang.Update,
            Permissions.CameraThucTrang.Delete,
            Permissions.Users.Read,
            Permissions.Users.Create,
            Permissions.Users.Update,
            Permissions.Users.Admin,
            Permissions.Roles.Read,
            Permissions.Roles.Create,
            Permissions.Roles.Update,
            Permissions.Codes.Read,
            Permissions.Codes.Create,
            Permissions.Codes.Update,
            Permissions.Codes.Delete,
            Permissions.TongHopTienDo.Read,
            Permissions.TongHopTienDo.XacNhan,
            Permissions.BaoCao.Read,
            Permissions.BaoCao.Export,
            Permissions.Files.Upload,
            Permissions.SystemAdmin
        };

        var existing = await _dbContext.Permissions.ToDictionaryAsync(x => x.PermCode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        long nextId = existing.Values.Select(x => x.Id).DefaultIfEmpty(3000).Max() + 1;

        foreach (var permissionCode in baselinePermissions)
        {
            if (existing.ContainsKey(permissionCode))
            {
                continue;
            }

            var parts = permissionCode.Split(':', 2);
            var module = parts[0];
            var action = parts.Length > 1 ? parts[1] : "read";

            await _dbContext.Permissions.AddAsync(new Permission
            {
                Id = nextId++,
                PermCode = permissionCode,
                Module = module,
                Action = action,
                MoTa = $"Baseline permission {permissionCode}"
            }, cancellationToken);
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        await UpsertRoleAsync(4001, "SYSTEM_ADMIN", "Quan tri he thong", true, cancellationToken);
        await UpsertRoleAsync(4002, "QUAN_LY", "Quan ly", false, cancellationToken);
        await UpsertRoleAsync(4003, "CAP_TINH", "Nguoi dung cap tinh", false, cancellationToken);
        await UpsertRoleAsync(4004, "CAP_XA", "Nguoi dung cap xa", false, cancellationToken);
        await UpsertRoleAsync(4005, "VIEWER", "Nguoi xem", false, cancellationToken);
        await UpsertRoleAsync(4006, "LANH_DAO", "Lanh dao", false, cancellationToken);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles.ToDictionaryAsync(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var permissions = await _dbContext.Permissions.ToDictionaryAsync(x => x.PermCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var mapping = new Dictionary<string, string[]>
        {
            ["SYSTEM_ADMIN"] =
            [
                Permissions.SystemAdmin
            ],
            ["QUAN_LY"] =
            [
                Permissions.Auth.Me,
                Permissions.DonVi.Read,
                Permissions.DonVi.Create,
                Permissions.DonVi.Update,
                Permissions.KyBaoCao.Read,
                Permissions.KyBaoCao.Create,
                Permissions.KyBaoCao.Approve,
                Permissions.MauBaoCao.Read,
                Permissions.MauBaoCao.Create,
                Permissions.MauBaoCao.Update,
                Permissions.MauBaoCao.Delete,
                Permissions.BaoCaoSnapshot.Read,
                Permissions.BaoCaoSnapshot.Create,
                Permissions.BaoCaoSnapshot.Update,
                Permissions.BaoCaoSnapshot.Submit,
                Permissions.BaoCaoSnapshot.Export,
                Permissions.YeuCauBoSung.Read,
                Permissions.YeuCauBoSung.Create,
                Permissions.YeuCauBoSung.Approve,
                Permissions.ThongBao.Read,
                Permissions.ThongBao.Update,
                Permissions.Files.Read,
                Permissions.Files.Delete,
                Permissions.NhanLucCntt.Read,
                Permissions.NhanLucCntt.Create,
                Permissions.NhanLucCntt.Update,
                Permissions.NhanLucCntt.Delete,
                Permissions.HeThongThongTin.Read,
                Permissions.HeThongThongTin.Create,
                Permissions.HeThongThongTin.Update,
                Permissions.HeThongThongTin.Delete,
                Permissions.DuAnCntt.Read,
                Permissions.DuAnCntt.Create,
                Permissions.DuAnCntt.Update,
                Permissions.DuAnCntt.Delete,
                Permissions.VanBanQppl.Read,
                Permissions.VanBanQppl.Create,
                Permissions.VanBanQppl.Update,
                Permissions.VanBanQppl.Delete,
                Permissions.DaoTaoBoiDuong.Read,
                Permissions.DaoTaoBoiDuong.Create,
                Permissions.DaoTaoBoiDuong.Update,
                Permissions.DaoTaoBoiDuong.Delete,
                Permissions.DaoTaoHocVien.Read,
                Permissions.DaoTaoHocVien.Create,
                Permissions.DaoTaoHocVien.Update,
                Permissions.DaoTaoHocVien.Delete,
                Permissions.NangLucSo.Read,
                Permissions.NangLucSo.Create,
                Permissions.NangLucSo.Update,
                Permissions.NangLucSo.Delete,
                Permissions.ThietBiCntt.Read,
                Permissions.ThietBiCntt.Create,
                Permissions.ThietBiCntt.Update,
                Permissions.ThietBiCntt.Delete,
                Permissions.HaTangMang.Read,
                Permissions.HaTangMang.Create,
                Permissions.HaTangMang.Update,
                Permissions.HaTangMang.Delete,
                Permissions.GiamSatSoc.Read,
                Permissions.GiamSatSoc.Create,
                Permissions.GiamSatSoc.Update,
                Permissions.GiamSatSoc.Delete,
                Permissions.GiamSatNoc.Read,
                Permissions.GiamSatNoc.Create,
                Permissions.GiamSatNoc.Update,
                Permissions.GiamSatNoc.Delete,
                Permissions.AtttHtttVanHanh.Read,
                Permissions.AtttHtttVanHanh.Create,
                Permissions.AtttHtttVanHanh.Update,
                Permissions.AtttHtttVanHanh.Delete,
                Permissions.AtttHtttDauTu.Read,
                Permissions.AtttHtttDauTu.Create,
                Permissions.AtttHtttDauTu.Update,
                Permissions.AtttHtttDauTu.Delete,
                Permissions.GiaiPhapAttt.Read,
                Permissions.GiaiPhapAttt.Create,
                Permissions.GiaiPhapAttt.Update,
                Permissions.GiaiPhapAttt.Delete,
                Permissions.CameraQuanLy.Read,
                Permissions.CameraQuanLy.Create,
                Permissions.CameraQuanLy.Update,
                Permissions.CameraQuanLy.Delete,
                Permissions.CameraThucTrang.Read,
                Permissions.CameraThucTrang.Create,
                Permissions.CameraThucTrang.Update,
                Permissions.CameraThucTrang.Delete,
                Permissions.Users.Read,
                Permissions.Users.Create,
                Permissions.Users.Update,
                Permissions.Roles.Read,
                Permissions.Roles.Create,
                Permissions.Roles.Update,
                Permissions.Codes.Read,
                Permissions.Codes.Create,
                Permissions.Codes.Update,
                Permissions.Codes.Delete,
                Permissions.TongHopTienDo.Read,
                Permissions.BaoCao.Read,
                Permissions.BaoCao.Export,
                Permissions.Files.Upload
            ],
            ["CAP_TINH"] =
            [
                Permissions.Auth.Me,
                Permissions.DonVi.Read,
                Permissions.KyBaoCao.Read,
                Permissions.MauBaoCao.Read,
                Permissions.BaoCaoSnapshot.Read,
                Permissions.BaoCaoSnapshot.Create,
                Permissions.BaoCaoSnapshot.Update,
                Permissions.BaoCaoSnapshot.Submit,
                Permissions.BaoCaoSnapshot.Export,
                Permissions.YeuCauBoSung.Read,
                Permissions.ThongBao.Read,
                Permissions.ThongBao.Update,
                Permissions.Files.Read,
                Permissions.Files.Delete,
                Permissions.NhanLucCntt.Read,
                Permissions.NhanLucCntt.Create,
                Permissions.NhanLucCntt.Update,
                Permissions.NhanLucCntt.Delete,
                Permissions.HeThongThongTin.Read,
                Permissions.HeThongThongTin.Create,
                Permissions.HeThongThongTin.Update,
                Permissions.HeThongThongTin.Delete,
                Permissions.DuAnCntt.Read,
                Permissions.DuAnCntt.Create,
                Permissions.DuAnCntt.Update,
                Permissions.DuAnCntt.Delete,
                Permissions.VanBanQppl.Read,
                Permissions.VanBanQppl.Create,
                Permissions.VanBanQppl.Update,
                Permissions.VanBanQppl.Delete,
                Permissions.DaoTaoBoiDuong.Read,
                Permissions.DaoTaoBoiDuong.Create,
                Permissions.DaoTaoBoiDuong.Update,
                Permissions.DaoTaoBoiDuong.Delete,
                Permissions.DaoTaoHocVien.Read,
                Permissions.DaoTaoHocVien.Create,
                Permissions.DaoTaoHocVien.Update,
                Permissions.DaoTaoHocVien.Delete,
                Permissions.NangLucSo.Read,
                Permissions.NangLucSo.Create,
                Permissions.NangLucSo.Update,
                Permissions.NangLucSo.Delete,
                Permissions.ThietBiCntt.Read,
                Permissions.ThietBiCntt.Create,
                Permissions.ThietBiCntt.Update,
                Permissions.ThietBiCntt.Delete,
                Permissions.HaTangMang.Read,
                Permissions.HaTangMang.Create,
                Permissions.HaTangMang.Update,
                Permissions.HaTangMang.Delete,
                Permissions.GiamSatSoc.Read,
                Permissions.GiamSatSoc.Create,
                Permissions.GiamSatSoc.Update,
                Permissions.GiamSatSoc.Delete,
                Permissions.GiamSatNoc.Read,
                Permissions.GiamSatNoc.Create,
                Permissions.GiamSatNoc.Update,
                Permissions.GiamSatNoc.Delete,
                Permissions.AtttHtttVanHanh.Read,
                Permissions.AtttHtttVanHanh.Create,
                Permissions.AtttHtttVanHanh.Update,
                Permissions.AtttHtttVanHanh.Delete,
                Permissions.AtttHtttDauTu.Read,
                Permissions.AtttHtttDauTu.Create,
                Permissions.AtttHtttDauTu.Update,
                Permissions.AtttHtttDauTu.Delete,
                Permissions.GiaiPhapAttt.Read,
                Permissions.GiaiPhapAttt.Create,
                Permissions.GiaiPhapAttt.Update,
                Permissions.GiaiPhapAttt.Delete,
                Permissions.CameraQuanLy.Read,
                Permissions.CameraQuanLy.Create,
                Permissions.CameraQuanLy.Update,
                Permissions.CameraQuanLy.Delete,
                Permissions.CameraThucTrang.Read,
                Permissions.CameraThucTrang.Create,
                Permissions.CameraThucTrang.Update,
                Permissions.CameraThucTrang.Delete,
                Permissions.TongHopTienDo.Read,
                Permissions.YeuCauBoSung.Create,
                Permissions.Users.Read,
                Permissions.Users.Create,
                Permissions.Users.Update,
                Permissions.Files.Upload,
                Permissions.BaoCao.Read,
                Permissions.BaoCao.Export,
                Permissions.Codes.Read
            ],
            ["CAP_XA"] =
            [
                Permissions.Auth.Me,
                Permissions.KyBaoCao.Read,
                Permissions.MauBaoCao.Read,
                Permissions.BaoCaoSnapshot.Read,
                Permissions.BaoCaoSnapshot.Export,
                Permissions.YeuCauBoSung.Read,
                Permissions.ThongBao.Read,
                Permissions.Files.Read,
                Permissions.Files.Upload,
                Permissions.TongHopTienDo.XacNhan,
                Permissions.NhanLucCntt.Read,
                Permissions.NhanLucCntt.Create,
                Permissions.NhanLucCntt.Update,
                Permissions.HeThongThongTin.Read,
                Permissions.HeThongThongTin.Create,
                Permissions.HeThongThongTin.Update,
                Permissions.DuAnCntt.Read,
                Permissions.DuAnCntt.Create,
                Permissions.DuAnCntt.Update,
                Permissions.VanBanQppl.Read,
                Permissions.VanBanQppl.Create,
                Permissions.VanBanQppl.Update,
                Permissions.DaoTaoBoiDuong.Read,
                Permissions.DaoTaoBoiDuong.Create,
                Permissions.DaoTaoBoiDuong.Update,
                Permissions.DaoTaoHocVien.Read,
                Permissions.DaoTaoHocVien.Create,
                Permissions.DaoTaoHocVien.Update,
                Permissions.NangLucSo.Read,
                Permissions.NangLucSo.Create,
                Permissions.NangLucSo.Update,
                Permissions.ThietBiCntt.Read,
                Permissions.ThietBiCntt.Create,
                Permissions.ThietBiCntt.Update,
                Permissions.HaTangMang.Read,
                Permissions.HaTangMang.Create,
                Permissions.HaTangMang.Update,
                Permissions.GiamSatSoc.Read,
                Permissions.GiamSatSoc.Create,
                Permissions.GiamSatSoc.Update,
                Permissions.GiamSatNoc.Read,
                Permissions.GiamSatNoc.Create,
                Permissions.GiamSatNoc.Update,
                Permissions.AtttHtttVanHanh.Read,
                Permissions.AtttHtttVanHanh.Create,
                Permissions.AtttHtttVanHanh.Update,
                Permissions.AtttHtttDauTu.Read,
                Permissions.AtttHtttDauTu.Create,
                Permissions.AtttHtttDauTu.Update,
                Permissions.GiaiPhapAttt.Read,
                Permissions.GiaiPhapAttt.Create,
                Permissions.GiaiPhapAttt.Update,
                Permissions.CameraQuanLy.Read,
                Permissions.CameraQuanLy.Create,
                Permissions.CameraQuanLy.Update,
                Permissions.CameraThucTrang.Read,
                Permissions.CameraThucTrang.Create,
                Permissions.CameraThucTrang.Update
            ],
            ["LANH_DAO"] =
            [
                Permissions.Auth.Me,
                Permissions.DonVi.Read,
                Permissions.KyBaoCao.Read,
                Permissions.MauBaoCao.Read,
                Permissions.BaoCaoSnapshot.Read,
                Permissions.BaoCaoSnapshot.Export,
                Permissions.BaoCao.Read,
                Permissions.BaoCao.Export,
                Permissions.ThongBao.Read,
                Permissions.Files.Read
            ],
            ["VIEWER"] =
            [
                Permissions.Auth.Me,
                Permissions.DonVi.Read,
                Permissions.KyBaoCao.Read,
                Permissions.MauBaoCao.Read,
                Permissions.BaoCaoSnapshot.Read,
                Permissions.BaoCaoSnapshot.Export,
                Permissions.YeuCauBoSung.Read,
                Permissions.ThongBao.Read,
                Permissions.Files.Read
            ]
        };

        var existing = await _dbContext.RolePermissions.ToListAsync(cancellationToken);
        foreach (var item in mapping)
        {
            if (!roles.TryGetValue(item.Key, out var role))
            {
                continue;
            }

            foreach (var permissionCode in item.Value)
            {
                if (!permissions.TryGetValue(permissionCode, out var permission))
                {
                    continue;
                }

                if (existing.Any(x => x.RoleId == role.Id && x.PermissionId == permission.Id))
                {
                    continue;
                }

                await _dbContext.RolePermissions.AddAsync(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                }, cancellationToken);
            }
        }
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        await UpsertUserAsync(
            5001,
            "admin",
            "Admin@123",
            "System Admin",
            "admin@thuc-luc.local",
            "0900000001",
            2001,
            cancellationToken);

        await UpsertUserAsync(
            5002,
            "h05.user",
            "H05User@123",
            "H05 User",
            "h05.user@thuc-luc.local",
            "0900000002",
            2001,
            cancellationToken);

        await UpsertUserAsync(
            5003,
            "donvi.user",
            "DonViUser@123",
            "Don Vi User",
            "donvi.user@thuc-luc.local",
            "0900000003",
            2002,
            cancellationToken);

        await UpsertUserAsync(
            5004,
            "viewer.user",
            "ViewerUser@123",
            "Viewer User",
            "viewer.user@thuc-luc.local",
            "0900000004",
            2003,
            cancellationToken);

        await UpsertUserAsync(
            5005,
            "h05.viewer",
            "H05Viewer@123",
            "H05 Viewer",
            "h05.viewer@thuc-luc.local",
            "0900000005",
            2001,
            cancellationToken);
    }

    private async Task SeedUserRoleAssignmentsAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users.ToDictionaryAsync(x => x.UserName ?? string.Empty, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var roles = await _dbContext.Roles.ToDictionaryAsync(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase, cancellationToken);

        await UpsertUserRoleAssignmentAsync(users["admin"].Id, roles["SYSTEM_ADMIN"].Id, 2001, cancellationToken);
        await UpsertUserRoleAssignmentAsync(users["h05.user"].Id, roles["QUAN_LY"].Id, 2001, cancellationToken);
        await UpsertUserRoleAssignmentAsync(users["donvi.user"].Id, roles["CAP_TINH"].Id, 2002, cancellationToken);
        await UpsertUserRoleAssignmentAsync(users["viewer.user"].Id, roles["VIEWER"].Id, 2003, cancellationToken);
        await UpsertUserRoleAssignmentAsync(users["h05.viewer"].Id, roles["LANH_DAO"].Id, 2001, cancellationToken);
    }

    private async Task SeedMauBaoCaoAsync(CancellationToken cancellationToken)
    {
        await UpsertMauBaoCaoAsync(
            8001,
            "NHAN_LUC",
            "Báo cáo Nhân lực CNTT",
            TanSuat.Quy,
            "[\"NHAN_LUC_CNTT\",\"NANG_LUC_SO\"]",
            "Báo cáo nhân lực và năng lực số theo quý",
            cancellationToken);

        await UpsertMauBaoCaoAsync(
            8005,
            "ATTT",
            "Báo cáo An toàn thông tin",
            TanSuat.Quy,
            "[\"GIAM_SAT_SOC\",\"GIAM_SAT_NOC\",\"ATTT_GIAI_PHAP\",\"ATTT_HTTT_VAN_HANH\",\"ATTT_HTTT_DAU_TU\"]",
            "Báo cáo tổng hợp tình hình an toàn thông tin theo quý",
            cancellationToken);

        await UpsertMauBaoCaoAsync(
            8006,
            "CAMERA",
            "Báo cáo Hệ thống Camera",
            TanSuat.Nam,
            "[\"CAMERA_QUAN_LY\",\"CAMERA_THUC_TRANG\"]",
            "Báo cáo thực trạng hệ thống camera giám sát theo năm",
            cancellationToken);
    }

    private async Task SeedKyBaoCaoAsync(CancellationToken cancellationToken)
    {
        await UpsertKyBaoCaoAsync(
            6001,
            8001,
            "2026Q1_NHAN_LUC",
            2026,
            1,
            null,
            KyBaoCaoStatus.DangMo,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31),
            "Kỳ Nhân lực Q1/2026 - đang mở",
            cancellationToken);

        await UpsertKyBaoCaoAsync(
            6002,
            8005,
            "2025Q4_ATTT",
            2025,
            4,
            null,
            KyBaoCaoStatus.DaDong,
            new DateOnly(2025, 10, 1),
            new DateOnly(2025, 12, 31),
            "Kỳ ATTT Q4/2025 - đã đóng",
            cancellationToken);

        await UpsertKyBaoCaoAsync(
            6003,
            8006,
            "2025_CAMERA",
            2025,
            null,
            null,
            KyBaoCaoStatus.DangMo,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 12, 31),
            "Kỳ Camera năm 2025 - đang mở",
            cancellationToken);
    }

    private async Task SeedKyTrangThaiDonViAsync(CancellationToken cancellationToken)
    {
        await UpsertKyTrangThaiDonViAsync(7001, 6001, 2002, KyTrangThaiDonViStatus.ChuaNhap, cancellationToken);
        await UpsertKyTrangThaiDonViAsync(7002, 6001, 2003, KyTrangThaiDonViStatus.ChuaNhap, cancellationToken);
        await UpsertKyTrangThaiDonViAsync(7003, 6002, 2002, KyTrangThaiDonViStatus.DaXacNhan, cancellationToken);
    }

    private async Task UpsertDonViAsync(
        long id,
        string maDonVi,
        string tenDonVi,
        string? tenVietTat,
        long? parentId,
        string? capDonVi,
        string? websiteNoiBo,
        string? websiteInternet,
        int? tongBienChe,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.DonVis.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id || x.MaDonVi == maDonVi, cancellationToken);
        if (entity is null)
        {
            entity = new DonVi { Id = id };
            await _dbContext.DonVis.AddAsync(entity, cancellationToken);
        }

        entity.MaDonVi = maDonVi;
        entity.TenDonVi = tenDonVi;
        entity.TenVietTat = tenVietTat;
        entity.ParentId = parentId;
        entity.CapDonVi = capDonVi;
        entity.WebsiteNoiBo = websiteNoiBo;
        entity.WebsiteInternet = websiteInternet;
        entity.TongBienChe = tongBienChe;
        entity.IsActive = true;
        entity.DeletedAt = null;
    }

    private async Task UpsertRoleAsync(long id, string roleCode, string tenRole, bool isSystem, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Id == id || x.Name == roleCode, cancellationToken);
        if (entity is null)
        {
            entity = new ApplicationRole { Id = id };
            await _dbContext.Roles.AddAsync(entity, cancellationToken);
        }

        entity.Name = roleCode;
        entity.NormalizedName = roleCode.ToUpperInvariant();
        entity.TenRole = tenRole;
        entity.IsSystem = isSystem;
    }

    private async Task UpsertUserAsync(
        long id,
        string username,
        string password,
        string hoTen,
        string email,
        string phone,
        long donViId,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = username.ToUpperInvariant();
        var entity = await _dbContext.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id || x.NormalizedUserName == normalizedUsername, cancellationToken);

        if (entity is null)
        {
            entity = new ApplicationUser { Id = id };
            await _dbContext.Users.AddAsync(entity, cancellationToken);
        }

        entity.UserName = username;
        entity.NormalizedUserName = normalizedUsername;
        entity.Email = email;
        entity.NormalizedEmail = email.ToUpperInvariant();
        entity.EmailConfirmed = true;
        entity.PhoneNumber = phone;
        entity.PhoneNumberConfirmed = true;
        entity.HoTen = hoTen;
        entity.SoDienThoai = phone;
        entity.DonViId = donViId;
        entity.IsActive = true;
        entity.MustChangePassword = false;
        entity.SecurityStamp = entity.SecurityStamp ?? Guid.NewGuid().ToString("N");
        entity.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        entity.PasswordHash = _passwordHasher.HashPassword(entity, password);
        entity.DeletedAt = null;
    }

    private async Task UpsertUserRoleAssignmentAsync(long userId, long roleId, long donViId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.UserRoleAssignments.FirstOrDefaultAsync(
            x => x.UserId == userId && x.RoleId == roleId && x.DonViId == donViId,
            cancellationToken);

        if (entity is not null)
        {
            return;
        }

        await _dbContext.UserRoleAssignments.AddAsync(new UserRoleAssignment
        {
            UserId = userId,
            RoleId = roleId,
            DonViId = donViId,
            CreatedAt = _dateTimeProvider.Now,
            CreatedBy = 5001
        }, cancellationToken);
    }

    private async Task UpsertMauBaoCaoAsync(
        long id,
        string maMau,
        string tenMau,
        TanSuat tanSuat,
        string danhSachModule,
        string? moTa,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.MauBaoCaos.FirstOrDefaultAsync(
            x => x.Id == id || x.MaMau == maMau,
            cancellationToken);

        if (entity is null)
        {
            entity = new MauBaoCao { Id = id };
            await _dbContext.MauBaoCaos.AddAsync(entity, cancellationToken);
        }

        entity.MaMau = maMau;
        entity.TenMau = tenMau;
        entity.TanSuat = tanSuat;
        entity.DanhSachModule = danhSachModule;
        entity.MoTa = moTa;
        entity.IsActive = true;
    }

    private async Task UpsertKyBaoCaoAsync(
        long id,
        long? mauBaoCaoId,
        string kyCode,
        int nam,
        int? quy,
        int? thang,
        KyBaoCaoStatus status,
        DateOnly? ngayBatDau,
        DateOnly? ngayKetThuc,
        string ghiChu,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.KyBaoCaos.FirstOrDefaultAsync(x => x.Id == id || x.KyCode == kyCode, cancellationToken);
        if (entity is null)
        {
            entity = new KyBaoCao { Id = id };
            await _dbContext.KyBaoCaos.AddAsync(entity, cancellationToken);
        }

        entity.KyCode = kyCode;
        entity.MauBaoCaoId = mauBaoCaoId;
        entity.Nam = nam;
        entity.Quy = quy;
        entity.Thang = thang;
        entity.TrangThai = status;
        entity.NgayBatDau = ngayBatDau;
        entity.NgayKetThuc = ngayKetThuc;
        entity.GhiChu = ghiChu;
    }

    private async Task UpsertKyTrangThaiDonViAsync(long id, long kyBaoCaoId, long donViId, KyTrangThaiDonViStatus status, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.KyTrangThaiDonVis.FirstOrDefaultAsync(
            x => x.Id == id || (x.KyBaoCaoId == kyBaoCaoId && x.DonViId == donViId),
            cancellationToken);

        if (entity is null)
        {
            entity = new KyTrangThaiDonVi { Id = id };
            await _dbContext.KyTrangThaiDonVis.AddAsync(entity, cancellationToken);
        }

        entity.KyBaoCaoId = kyBaoCaoId;
        entity.DonViId = donViId;
        entity.TrangThai = status;
    }
}