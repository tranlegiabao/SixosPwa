using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Cho duy nhat trong SixosPwa biet mot he doi tac cu the noi chuyen the nao.
/// Man hinh goi giao dien nay chu KHONG goi thang API cua ai.
/// Giai doan 2 = them mot ban cai + mot dong trong DM_DoiTacApi. Xem ADR 0003.
/// </summary>
public interface IPartnerGateway
{
    /// <summary>Kieu API ma ban cai nay phuc vu (khop <see cref="DoiTacApi.KieuApi"/>).</summary>
    string KieuApi { get; }

    /// <summary>Co so nay co ban giao phien sang he ngoai khong.</summary>
    bool CoBanGiao { get; }

    /// <summary>Ben doi tac da co tai khoan cho so CCCD nay chua.</summary>
    Task<TinhTrangTaiKhoan> TinhTrangTaiKhoanAsync(DoiTacApi cauHinh, string cccd, CancellationToken ct = default);

    /// <summary>
    /// Mo tai khoan moi ben doi tac. Voi Ung Buou: goi register o TANG MAY CHU va
    /// doc thang truong "code" trong than phan hoi, nen benh nhan KHONG bao gio
    /// phai go ma cua doi tac.
    /// </summary>
    Task<KetQuaMoTaiKhoan> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default);

    /// <summary>Gui ma dat lai mat khau cho benh nhan da co tai khoan tu truoc (man Lien ket).</summary>
    Task<KetQuaThaoTac> GuiMaLienKetAsync(DoiTacApi cauHinh, string cccd, string dienThoai, CancellationToken ct = default);

    /// <summary>Dat lai mat khau ben doi tac bang ma vua nhan.</summary>
    Task<KetQuaThaoTac> DatLaiMatKhauAsync(DoiTacApi cauHinh, string cccd, string dienThoai, string ma, string matKhauMoi, CancellationToken ct = default);


    /// <summary>
    /// Dung du lieu cho form ban giao. KHONG goi HTTP o day: cookie phai duoc dat
    /// tren TRINH DUYET benh nhan, nen buoc cuoi bat buoc la form POST top-level
    /// trong popup. Xem ADR 0003.
    /// </summary>
    ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau);
}

public enum TinhTrangTaiKhoan
{
    /// <summary>Ben doi tac chua co tai khoan voi CCCD nay -> di man Dang ky.</summary>
    ChuaCo,

    /// <summary>Da co va da xac thuc -> can mat khau (man Lien ket hoac ban giao thang).</summary>
    DaCo,

    /// <summary>Khong hoi duoc (mat mang, API sap). Man hinh phai bao that, khong doan.</summary>
    KhongXacDinh
}

public record YeuCauMoTaiKhoan(string HoTen, string Cccd, string DienThoai, string? Email, string MatKhau);

public record KetQuaMoTaiKhoan(bool ThanhCong, string ThongBao, string? MaXacNhan);

public record KetQuaThaoTac(bool ThanhCong, string ThongBao);

/// <param name="YDinh">
/// Benh nhan bam nut gi de toi day: "dat-goi-kham", "lich-su-hen",
/// "ho-so-kham"... Moi doi tac tu biet man tuong ung cua minh nam o dau.
/// </param>
public record YeuCauBanGiao(string Cccd, string DienThoai, string? Email, string? MaXacNhan, string? MatKhau, string? YDinh = null);

/// <summary>Mo ta form ma man Ban giao se POST sang he doi tac.</summary>
/// <summary>Mot lan POST sang he doi tac.</summary>
public record BuocBanGiao(string Action, IReadOnlyDictionary<string, string> Truong);

/// <param name="CacBuoc">
/// Cac lan POST TUAN TU trong cung mot cua so. He doi tac cong don claim qua
/// tung lan, nen dang nhap xong roi chon chi nhanh la du ba claim ma Middleware
/// cua ho doi hoi.
/// </param>
/// <param name="DichCuoi">
/// Noi benh nhan can den sau khi cookie da du — man tuong ung voi nut ho bam,
/// khong phai luc nao cung la trang chu cua doi tac.
/// </param>
public record ThongTinBanGiao(IReadOnlyList<BuocBanGiao> CacBuoc, string DichCuoi);
