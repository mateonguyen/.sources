namespace ThucLuc.Application.Security;

public static class Permissions
{
    private static readonly string[] BusinessPermissionPrefixes =
    [
        "nhan_luc_cntt:",
        "dao_tao_boi_duong:",
        "dao_tao_hoc_vien:",
        "nang_luc_so:",
        "thiet_bi_cntt:",
        "he_thong_thong_tin:",
        "ha_tang_mang:",
        "giam_sat_noc:",
        "camera_quan_ly:",
        "camera_thuc_trang:",
        "du_an_cntt:",
        "van_ban_qppl:",
        "giam_sat_soc:",
        "attt_httt_van_hanh:",
        "attt_httt_dau_tu:",
        "giai_phap_attt:"
    ];

    public static bool IsBusinessPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        return BusinessPermissionPrefixes.Any(prefix =>
            permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // Module Bao cao - SYSTEM_ADMIN khong duoc bypass vao day (thuan quan
    // tri he thong), nhung KHONG dua vao BusinessPermissionPrefixes vi
    // QUAN_LY van duoc cap quyen ro rang cho module nay (KyBaoCao/MauBaoCao).
    private static readonly string[] SystemAdminExcludedPrefixes =
    [
        "tong_hop_tien_do:",
        "snapshot:",
        "yeu_cau_bo_sung:",
        "tien_do_bao_cao:",
        "ky_bao_cao:",
        "mau_bao_cao:"
    ];

    public static bool IsSystemAdminExcluded(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        return SystemAdminExcludedPrefixes.Any(prefix =>
            permission.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static class Auth
    {
        public const string Me = "auth:me";
    }

    public static class DonVi
    {
        public const string Read = "don_vi:read";
        public const string Create = "don_vi:create";
        public const string Update = "don_vi:update";
        public const string Delete = "don_vi:delete";
    }

    public static class Users
    {
        public const string Read = "users:read";
        public const string Create = "users:create";
        public const string Update = "users:update";
        public const string Admin = "users:admin";
    }

    public static class Roles
    {
        public const string Read = "roles:read";
        public const string Create = "roles:create";
        public const string Update = "roles:update";
        public const string Admin = "roles:admin";
    }

    public static class KyBaoCao
    {
        public const string Read = "ky_bao_cao:read";
        public const string Create = "ky_bao_cao:create";
        public const string Update = "ky_bao_cao:update";
        public const string Delete = "ky_bao_cao:delete";
        public const string Approve = "ky_bao_cao:approve";
    }

    // Tach rieng khoi KyBaoCao.Approve: chi gate menu "Tien do bao cao" (xem tien do nop theo don vi).
    // ky_bao_cao:approve van giu nguyen nghia cu -- duyet/mo/dong ky bao cao (PATCH status) va
    // duoc tai su dung o ~18 Service.cs khac nhu quyen "xem toan bo khong phan biet don vi", khong dung.
    public static class TienDoBaoCao
    {
        public const string Read = "tien_do_bao_cao:read";
    }

    public static class MauBaoCao
    {
        public const string Read = "mau_bao_cao:read";
        public const string Create = "mau_bao_cao:create";
        public const string Update = "mau_bao_cao:update";
        public const string Delete = "mau_bao_cao:delete";
    }

    public static class BaoCaoSnapshot
    {
        public const string Read = "snapshot:read";
        public const string Create = "snapshot:create";
        public const string Update = "snapshot:update";
        public const string Submit = "snapshot:submit";
        public const string Export = "snapshot:pdf";
    }

    public static class YeuCauBoSung
    {
        public const string Read = "yeu_cau_bo_sung:read";
        public const string Create = "yeu_cau_bo_sung:create";
        public const string Approve = "yeu_cau_bo_sung:approve";
    }

    public static class Files
    {
        public const string Upload = "files:upload";
        public const string Read = "files:read";
        public const string Delete = "files:delete";
    }

    public static class ThongBao
    {
        public const string Read = "thong_bao:read";
        public const string Update = "thong_bao:update";
    }

    public static class TongHopTienDo
    {
        public const string Read = "tong_hop_tien_do:read";
        public const string XacNhan = "tong_hop_tien_do:xac_nhan";
    }

    public static class NhanLucCntt
    {
        public const string Read = "nhan_luc_cntt:read";
        public const string Create = "nhan_luc_cntt:create";
        public const string Update = "nhan_luc_cntt:update";
        public const string Delete = "nhan_luc_cntt:delete";
    }

    public static class HeThongThongTin
    {
        public const string Read = "he_thong_thong_tin:read";
        public const string Create = "he_thong_thong_tin:create";
        public const string Update = "he_thong_thong_tin:update";
        public const string Delete = "he_thong_thong_tin:delete";
    }

    public static class DuAnCntt
    {
        public const string Read = "du_an_cntt:read";
        public const string Create = "du_an_cntt:create";
        public const string Update = "du_an_cntt:update";
        public const string Delete = "du_an_cntt:delete";
    }

    public static class VanBanQppl
    {
        public const string Read = "van_ban_qppl:read";
        public const string Create = "van_ban_qppl:create";
        public const string Update = "van_ban_qppl:update";
        public const string Delete = "van_ban_qppl:delete";
    }

    public static class DaoTaoBoiDuong
    {
        public const string Read = "dao_tao_boi_duong:read";
        public const string Create = "dao_tao_boi_duong:create";
        public const string Update = "dao_tao_boi_duong:update";
        public const string Delete = "dao_tao_boi_duong:delete";
    }

    public static class DaoTaoHocVien
    {
        public const string Read = "dao_tao_hoc_vien:read";
        public const string Create = "dao_tao_hoc_vien:create";
        public const string Update = "dao_tao_hoc_vien:update";
        public const string Delete = "dao_tao_hoc_vien:delete";
    }

    public static class NangLucSo
    {
        public const string Read = "nang_luc_so:read";
        public const string Create = "nang_luc_so:create";
        public const string Update = "nang_luc_so:update";
        public const string Delete = "nang_luc_so:delete";
    }

    public static class ThietBiCntt
    {
        public const string Read = "thiet_bi_cntt:read";
        public const string Create = "thiet_bi_cntt:create";
        public const string Update = "thiet_bi_cntt:update";
        public const string Delete = "thiet_bi_cntt:delete";
    }

    public static class HaTangMang
    {
        public const string Read = "ha_tang_mang:read";
        public const string Create = "ha_tang_mang:create";
        public const string Update = "ha_tang_mang:update";
        public const string Delete = "ha_tang_mang:delete";
    }

    public static class GiamSatSoc
    {
        public const string Read = "giam_sat_soc:read";
        public const string Create = "giam_sat_soc:create";
        public const string Update = "giam_sat_soc:update";
        public const string Delete = "giam_sat_soc:delete";
    }

    public static class GiamSatNoc
    {
        public const string Read = "giam_sat_noc:read";
        public const string Create = "giam_sat_noc:create";
        public const string Update = "giam_sat_noc:update";
        public const string Delete = "giam_sat_noc:delete";
    }

    public static class AtttHtttVanHanh
    {
        public const string Read = "attt_httt_van_hanh:read";
        public const string Create = "attt_httt_van_hanh:create";
        public const string Update = "attt_httt_van_hanh:update";
        public const string Delete = "attt_httt_van_hanh:delete";
    }

    public static class AtttHtttDauTu
    {
        public const string Read = "attt_httt_dau_tu:read";
        public const string Create = "attt_httt_dau_tu:create";
        public const string Update = "attt_httt_dau_tu:update";
        public const string Delete = "attt_httt_dau_tu:delete";
    }

    public static class GiaiPhapAttt
    {
        public const string Read = "giai_phap_attt:read";
        public const string Create = "giai_phap_attt:create";
        public const string Update = "giai_phap_attt:update";
        public const string Delete = "giai_phap_attt:delete";
    }

    public static class CameraQuanLy
    {
        public const string Read = "camera_quan_ly:read";
        public const string Create = "camera_quan_ly:create";
        public const string Update = "camera_quan_ly:update";
        public const string Delete = "camera_quan_ly:delete";
    }

    public static class CameraThucTrang
    {
        public const string Read = "camera_thuc_trang:read";
        public const string Create = "camera_thuc_trang:create";
        public const string Update = "camera_thuc_trang:update";
        public const string Delete = "camera_thuc_trang:delete";
    }

    public const string SystemAdmin = "system:admin";

    public static class Codes
    {
        public const string Read = "codes:read";
        public const string Create = "codes:create";
        public const string Update = "codes:update";
        public const string Delete = "codes:delete";
    }

    public static class RefLoaiThietBi
    {
        public const string Read = "ref_loai_thiet_bi:read";
        public const string Create = "ref_loai_thiet_bi:create";
        public const string Update = "ref_loai_thiet_bi:update";
        public const string Delete = "ref_loai_thiet_bi:delete";
    }
}