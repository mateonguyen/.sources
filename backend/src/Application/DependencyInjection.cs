using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ThucLuc.Application.Features.Auth;
using ThucLuc.Application.Features.AtttHtttDauTu;
using ThucLuc.Application.Features.AtttHtttVanHanh;
using ThucLuc.Application.Features.BaoCaoSnapshot;
using ThucLuc.Application.Features.CameraQuanLy;
using ThucLuc.Application.Features.CameraThucTrang;
using ThucLuc.Application.Features.Codes;
using ThucLuc.Application.Features.DaoTaoBoiDuong;
using ThucLuc.Application.Features.DaoTaoHocVien;
using ThucLuc.Application.Features.DonVi;
using ThucLuc.Application.Features.DuAnCntt;
using ThucLuc.Application.Features.Files;
using ThucLuc.Application.Features.GiaiPhapAttt;
using ThucLuc.Application.Features.GiamSatNoc;
using ThucLuc.Application.Features.GiamSatSoc;
using ThucLuc.Application.Features.HaTangMang;
using ThucLuc.Application.Features.HeThongThongTin;
using ThucLuc.Application.Features.HtttTieuChuan;
using ThucLuc.Application.Features.KyBaoCao;
using ThucLuc.Application.Features.MauBaoCao;
using ThucLuc.Application.Features.NangLucSo;
using ThucLuc.Application.Features.NhanLucCntt;
using ThucLuc.Application.Features.TongHopTienDo;
using ThucLuc.Application.Features.RefLoaiThietBi;
using ThucLuc.Application.Features.Roles;
using ThucLuc.Application.Features.ThietBiCntt;
using ThucLuc.Application.Features.ThongBao;
using ThucLuc.Application.Features.Users;
using ThucLuc.Application.Features.VanBanQppl;
using ThucLuc.Application.Features.YeuCauBoSung;
using ThucLuc.Application.Security;

namespace ThucLuc.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(DependencyInjection).Assembly);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDonViService, DonViService>();
        services.AddScoped<IDonViInputModeService, DonViInputModeService>();
        services.AddScoped<IRefLoaiThietBiService, RefLoaiThietBiService>();
        services.AddScoped<IKyBaoCaoService, KyBaoCaoService>();
        services.AddScoped<IMauBaoCaoService, MauBaoCaoService>();
        services.AddScoped<IBaoCaoSnapshotService, BaoCaoSnapshotService>();
        services.AddScoped<IBaoCaoExportService, BaoCaoExportService>();
        services.AddScoped<IYeuCauBoSungService, YeuCauBoSungService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IThongBaoService, ThongBaoService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<INhanLucCnttService, NhanLucCnttService>();
        services.AddScoped<IHeThongThongTinService, HeThongThongTinService>();
        services.AddScoped<IHtttTieuChuanService, HtttTieuChuanService>();
        services.AddScoped<IDuAnCnttService, DuAnCnttService>();
        services.AddScoped<ICodeService, CodeService>();
        services.AddScoped<IVanBanQpplService, VanBanQpplService>();
        services.AddScoped<IDaoTaoBoiDuongService, DaoTaoBoiDuongService>();
        services.AddScoped<IDaoTaoHocVienService, DaoTaoHocVienService>();
        services.AddScoped<INangLucSoService, NangLucSoService>();
        services.AddScoped<IThietBiCnttService, ThietBiCnttService>();
        services.AddScoped<IHaTangMangService, HaTangMangService>();
        services.AddScoped<IGiamSatSocService, GiamSatSocService>();
        services.AddScoped<IGiamSatNocService, GiamSatNocService>();
        services.AddScoped<IAtttHtttVanHanhService, AtttHtttVanHanhService>();
        services.AddScoped<IAtttHtttDauTuService, AtttHtttDauTuService>();
        services.AddScoped<IGiaiPhapAtttService, GiaiPhapAtttService>();
        services.AddScoped<ICameraQuanLyService, CameraQuanLyService>();
        services.AddScoped<ICameraThucTrangService, CameraThucTrangService>();
        services.AddScoped<ITongHopTienDoService, TongHopTienDoService>();
        services.AddScoped<IDonViDataScopeService, DonViDataScopeService>();

        return services;
    }
}