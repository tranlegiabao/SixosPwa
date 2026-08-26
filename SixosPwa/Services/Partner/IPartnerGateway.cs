using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Cho duy nhat trong SixosPwa biet mot he doi tac cu the noi chuyen the nao.
/// Man hinh goi giao dien nay chu KHONG goi thang API cua ai.
/// Them mot doi tac = them mot ban cai + mot dong trong DM_DoiTacApi.
///
/// Doc ADR 0014 (docs/adr/) truoc khi sua. 0014 THAY 0003: voi co so dung man
/// cua doi tac, SixosPwa thoi dung luong OTP cua minh — benh nhan go MAT KHAU
/// THAT cua ho va nhan ma xac thuc do CHINH doi tac gui.
/// </summary>
public interface IPartnerGateway
{
    /// <summary>Kieu API ma ban cai nay phuc vu (khop <see cref="DoiTacApi.KieuApi"/>).</summary>
    string KieuApi { get; }

    /// <summary>Co so nay co ban giao phien sang he ngoai khong.</summary>
    bool CoBanGiao { get; }

    /// <summary>
    /// Co so nay dung BO MAN cua doi tac (Dang nhap / Dang ky / Quen mat khau)
    /// thay cho luong OTP cua SixosPwa. Dat true thi
    /// <see cref="DangNhapAsync"/>, <see cref="XacThucMaAsync"/> va
    /// <see cref="QuenMatKhauAsync"/> phai chay that.
    /// </summary>
    bool DungManDoiTac { get; }

    /// <summary>
    /// Kiem CCCD + mat khau ben doi tac. Doi tac la NGUON SU THAT cua mat khau:
    /// SixosPwa khong tu phan xu dung/sai bao gio (ADR 0014).
    /// </summary>
    Task<KetQuaThaoTac> DangNhapAsync(DoiTacApi cauHinh, string cccd, string matKhau, CancellationToken ct = default);

    /// <summary>
    /// Mo tai khoan moi ben doi tac. Doi tac tu gui ma xac thuc cho benh nhan
    /// (SMS), benh nhan tu go ma o buoc sau — giong het luong tren trang cua ho.
    /// </summary>
    Task<KetQuaThaoTac> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default);

    /// <summary>
    /// Doi ma xac thuc benh nhan vua go lay tai khoan da xac thuc ben doi tac.
    /// Goi o TANG MAY CHU de con bao loi "ma sai" ngay trong app.
    /// </summary>
    Task<KetQuaThaoTac> XacThucMaAsync(DoiTacApi cauHinh, string cccd, string? email, string dienThoai, string ma, CancellationToken ct = default);

    /// <summary>
    /// Xin doi tac gui duong dan dat lai mat khau. Doi tac tu gui Email/SMS, va
    /// duong dan do tro ve TRANG CUA HO — benh nhan dat mat khau moi ben do roi
    /// quay lai app dang nhap (ADR 0014).
    /// </summary>
    Task<KetQuaThaoTac> QuenMatKhauAsync(DoiTacApi cauHinh, string cccd, string emailHoacSdt, CancellationToken ct = default);

    /// <summary>
    /// Danh sach chi nhanh cua doi tac, de man dang nhap hien dung thong tin cua
    /// ho (dia chi, hotline, gio lam viec) o kho may tinh. Rong = khong hien.
    /// </summary>
    Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhAsync(DoiTacApi cauHinh, CancellationToken ct = default);

    /// <summary>
    /// Dung du lieu cho form ban giao. KHONG goi HTTP o day: cookie phai duoc dat
    /// tren TRINH DUYET benh nhan, nen buoc cuoi bat buoc la form POST top-level.
    /// Xem ADR 0003 (phan SameSite van con hieu luc).
    /// </summary>
    ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau);
}

/// <param name="Kenh">
/// Kenh doi tac gui ma xac thuc cho benh nhan. Y nghia do BAN CAI cua tung doi
/// tac dinh nghia (voi Ung Buou: 1 = Zalo, 3 = SMS) — tang tren chi chuyen tiep
/// lua chon cua benh nhan, khong dien giai.
/// </param>
public record YeuCauMoTaiKhoan(string HoTen, string Cccd, string DienThoai, string? Email, string MatKhau, int Kenh);

public record KetQuaThaoTac(bool ThanhCong, string ThongBao);

/// <summary>Mot chi nhanh ben he doi tac — chi nhung truong man hinh dung toi.</summary>
public record ChiNhanhDoiTac(string Ten, string? DiaChi, string? Hotline, string? GioLamViec);

/// <param name="YDinh">
/// Benh nhan bam nut gi de toi day: "dat-goi-kham", "lich-su-hen",
/// "ho-so-kham"... Moi doi tac tu biet man tuong ung cua minh nam o dau.
/// </param>
public record YeuCauBanGiao(string Cccd, string DienThoai, string? Email, string? MaXacNhan, string? MatKhau, string? YDinh = null);

/// <summary>Mot lan POST sang he doi tac.</summary>
public record BuocBanGiao(string Action, IReadOnlyDictionary<string, string> Truong);

/// <param name="CacBuoc">
/// Cac lan POST TUAN TU trong cung mot cua so. Thuc te chi con MOT buoc —
/// SameSite=Lax khong cho POST lien site mang cookie (ADR 0003).
/// </param>
/// <param name="DichCuoi">
/// Noi benh nhan can den sau khi cookie da du — man tuong ung voi nut ho bam,
/// khong phai luc nao cung la trang chu cua doi tac.
/// </param>
public record ThongTinBanGiao(IReadOnlyList<BuocBanGiao> CacBuoc, string DichCuoi);
