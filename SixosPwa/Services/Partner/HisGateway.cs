using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Co so chay HisSoft va noi voi SixosPwa qua mach SPWA (Giai doan 2).
///
/// 🔴 CO API NHUNG VAN DUNG MAN CUA SIXOSPWA — day la cho de hieu nham nhat cua
/// ca truc <c>KieuApi</c>. Giai doan 1 hieu "co API" = *doi tac dung man rieng,
/// ta ban giao phien sang cho ho* (do la <c>UB</c>). Kieu <c>HIS</c> thi nguoc
/// lai: co API that, nhung benh nhan o LAI trang benh nhan noi bo cua SixosPwa.
/// Vi the noi *nhanh ban giao* / *nhanh man chung*, cam noi "nhanh co API" tran.
///
/// Nen ban cai nay cu xu y het <see cref="NoApiGateway"/> ve mat DANG NHAP:
/// <c>CoBanGiao = false</c>, <c>DungManDoiTac = false</c>, khong goi ra ngoai
/// mot cuoc nao trong luong dang nhap/dang ky/quen mat khau.
///
/// Vay no ton tai de lam gi? Hai viec:
///   1. <c>BaseUrl</c> tro sang HIS cua co so, va o kieu nay no CO NGUOI DOC
///      (khac han <c>UB</c> — xem chu thich tren <see cref="DoiTacApi.BaseUrl"/>):
///      duong DOC cua cong goi vao HIS di qua dia chi nay.
///   2. Khong co ban cai nay thi <see cref="PartnerGatewayFactory"/> khong tim
///      duoc cua cho <c>KieuApi='HIS'</c>, ghi canh bao roi rot ve noi bo — chay
///      dung nhung mo mot dong log canh bao moi lan, va che mat loi that neu sau
///      nay co ai cau hinh sai.
///
/// Chieu NGUOC (HIS day tai lieu len cong) KHONG di qua day: no vao thang khu
/// <c>api/v1</c> voi <c>[KhoaCoSo]</c>.
/// </summary>
public class HisGateway : IPartnerGateway
{
    private const string DungManNoiBo = "Cơ sở này dùng màn hình của SixosPwa";

    public string KieuApi => KieuApiDoiTac.His;

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
