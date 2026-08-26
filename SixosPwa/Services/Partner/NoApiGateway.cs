using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Co so KHONG co API rieng. Khong goi ra ngoai gi ca, khong ban giao —
/// benh nhan o lai trang benh nhan noi bo cua SixosPwa va dung luong OTP cua
/// chinh SixosPwa.
/// </summary>
public class NoApiGateway : IPartnerGateway
{
    private const string KhongCoDoiTac = "Cơ sở này không liên kết với hệ thống bên ngoài";

    public string KieuApi => KieuApiDoiTac.KhongCo;

    public bool CoBanGiao => false;

    public bool DungManDoiTac => false;

    public Task<KetQuaThaoTac> DangNhapAsync(DoiTacApi cauHinh, string cccd, string matKhau, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, KhongCoDoiTac));

    public Task<KetQuaThaoTac> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(true, "Tạo tài khoản nội bộ thành công"));

    public Task<KetQuaThaoTac> XacThucMaAsync(DoiTacApi cauHinh, string cccd, string? email, string dienThoai, string ma, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, KhongCoDoiTac));

    public Task<KetQuaThaoTac> QuenMatKhauAsync(DoiTacApi cauHinh, string cccd, string emailHoacSdt, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, KhongCoDoiTac));

    public Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhAsync(DoiTacApi cauHinh, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ChiNhanhDoiTac>>(Array.Empty<ChiNhanhDoiTac>());

    public ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau) => null;
}
