using System.Linq;

namespace SixosPwa.Models;

/// <summary>
/// Danh mục ĐÓNG các loại tài liệu HIS đẩy lên (V9).
///
/// <para>
/// 🔴 Bốn mã này KHÔNG được đổi: chúng đã nằm trong
/// <c>SixosPwa_TaiLieu_API.postman_collection.json</c> — bản hợp đồng đã giao
/// cho HIS của cơ sở. Đổi mã là phá hợp đồng đã phát.
/// </para>
/// <para>
/// Vì sao phải đóng: trước đây màn phân nhóm bằng <c>!= "DON_THUOC"</c>, tức
/// mọi chuỗi lạ đều rơi vào nhóm "kết quả khám". HIS gõ sai một ký tự
/// (<c>DON_THUOOC</c>) là đơn thuốc im lặng nhảy sang nhóm khác mà không ai
/// biết. Này của API chặn ngay từ đầu.
/// </para>
/// </summary>
public static class LoaiTaiLieu
{
    public const string DonThuoc = "DON_THUOC";
    public const string KetQuaXn = "KET_QUA_XN";
    public const string Cdha = "CDHA";
    public const string GiayRaVien = "GIAY_RA_VIEN";

    /// <summary>Tên nhóm trên URL — <c>/benh-nhan/tai-lieu?nhom=...</c>.</summary>
    public const string NhomDonThuoc = "don-thuoc";
    public const string NhomKetQuaKham = "ket-qua-kham";
    public const string NhomTatCa = "tat-ca";

    public static readonly string[] TatCa = { DonThuoc, KetQuaXn, Cdha, GiayRaVien };

    /// <summary>
    /// Mảng chứ không phải HashSet: EF dịch <c>mang.Contains(cot)</c> thành
    /// <c>IN (...)</c>, còn HashSet thì không.
    /// </summary>
    public static readonly string[] MaCuaNhomKetQuaKham = { KetQuaXn, Cdha, GiayRaVien };

    public static bool HopLe(string? ma) =>
        !string.IsNullOrWhiteSpace(ma) && TatCa.Contains(ma);

    /// <summary>Danh sách mã để nhét vào câu báo lỗi cho bên HIS đọc.</summary>
    public static string DanhSachChoNguoiDoc() => string.Join(", ", TatCa);

    public static string TenHienThi(string? ma) => ma switch
    {
        DonThuoc => "Đơn thuốc",
        KetQuaXn => "Kết quả xét nghiệm",
        Cdha => "CĐHA / X-Quang",
        GiayRaVien => "Giấy ra viện",
        // Chín dòng tài liệu cũ trong CSDL có thể mang mã ngoài danh mục — vẫn
        // phải hiện được, không được làm vỡ màn.
        _ => "Hồ sơ y tế"
    };

    public static string NhomUrl(string? ma) =>
        ma == DonThuoc ? NhomDonThuoc : NhomKetQuaKham;

    public static string TieuDeNhom(string? nhom) => nhom switch
    {
        NhomDonThuoc => "Đơn thuốc",
        NhomKetQuaKham => "Kết quả khám",
        _ => "Tài liệu y tế"
    };
}
