using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Co so chay HisSoft va noi voi SixosPwa qua mach SPWA (Giai doan 2).
///
/// 🔴 CO API NHUNG VAN DUNG MAN CUA SIXOSPWA.
/// Nhanh ban giao (Ub) chuyen phien sang man cua doi tac; con nhanh HIS: co API,
/// nhung benh nhan o lai trang benh nhan noi bo cua SixosPwa.
///
/// Ban cai nay xu ly giong <see cref="NoApiGateway"/> ve mat DANG NHAP:
/// <c>CoBanGiao = false</c>, <c>DungManDoiTac = false</c>, khong goi ra ngoai
/// trong luong dang nhap/dang ky/quen mat khau.
///
/// <c>BaseUrl</c> tro sang HIS cua co so de phuc vu duong DOC tai lieu/lich hen.
///
/// Chieu NGUOC (HIS day tai lieu len cong) KHONG di qua day: no vao thang khu
/// <c>api/v1</c> voi <c>[KhoaCoSo]</c>.
/// </summary>
public class HisGateway : IPartnerGateway
{
    private const string DungManNoiBo = "Cơ sở này dùng màn hình của SixosPwa";

    public string TenCong => "His";

    /// <summary>Khong ban giao — benh nhan o lai trang benh nhan cua SixosPwa.</summary>
    public bool CoBanGiao => false;

    /// <summary>Dung luong OTP cua chinh SixosPwa, khong phai cua doi tac.</summary>
    public bool DungManDoiTac => false;

    public Task<KetQuaThaoTac> DangNhapAsync(DoiTacApi cauHinh, string cccd, string matKhau, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, DungManNoiBo));

    public Task<KetQuaThaoTac> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(true, "Tạo tài khoản nội bộ thành công"));

    public Task<KetQuaThaoTac> XacThucMaAsync(DoiTacApi cauHinh, string cccd, string? email, string dienThoai, string ma, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, DungManNoiBo));

    public Task<KetQuaThaoTac> QuenMatKhauAsync(DoiTacApi cauHinh, string cccd, string emailHoacSdt, CancellationToken ct = default)
        => Task.FromResult(new KetQuaThaoTac(false, DungManNoiBo));

    public Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhAsync(DoiTacApi cauHinh, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ChiNhanhDoiTac>>(Array.Empty<ChiNhanhDoiTac>());

    public ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau) => null;
}
