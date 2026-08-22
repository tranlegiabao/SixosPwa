using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Co so KHONG co API rieng. Khong goi ra ngoai gi ca, khong ban giao —
/// benh nhan o lai trang benh nhan noi bo cua SixosPwa.
/// </summary>
public class NoApiGateway : IPartnerGateway
{
    public string KieuApi => KieuApiDoiTac.KhongCo;

    public bool CoBanGiao => false;

    public Task<TinhTrangTaiKhoan> TinhTrangTaiKhoanAsync(DoiTacApi cauHinh, string cccd, CancellationToken ct = default)
        // Khong co he ngoai de hoi; tai khoan noi bo do SixosPwa tu quan.
        => Task.FromResult(TinhTrangTaiKhoan.ChuaCo);

    public Task<KetQuaMoTaiKhoan> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default)
        => Task.FromResult(new KetQuaMoTaiKhoan(true, "Tạo tài khoản nội bộ thành công", null));

    public Task<KetQuaThaoTac> GuiMaLienKetAsync(DoiTacApi cauHinh, string cccd, string dienThoai, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, "Cơ sở này không cần liên kết tài khoản"));

    public Task<KetQuaThaoTac> DatLaiMatKhauAsync(DoiTacApi cauHinh, string cccd, string dienThoai, string ma, string matKhauMoi, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, "Cơ sở này không cần liên kết tài khoản"));

    public Task<long?> TimHoSoChinhChuAsync(DoiTacApi cauHinh, string cccd, string matKhau, CancellationToken ct = default)
        => Task.FromResult<long?>(null);

    public ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau) => null;
}
