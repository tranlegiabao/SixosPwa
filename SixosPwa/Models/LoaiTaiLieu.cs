using System.Linq;

namespace SixosPwa.Models;

/// <summary>
/// Danh muc DONG cac loai tai lieu HIS day len (V9).
///
/// <para>
/// 🔴 Bon ma nay KHONG duoc doi: chung da nam trong
/// <c>SixosPwa_TaiLieu_API.postman_collection.json</c> — ban hop dong da giao
/// cho HIS cua co so. Doi ma la pha hop dong da phat.
/// </para>
/// <para>
/// Vi sao phai dong: truoc day man phan nhom bang <c>!= "DON_THUOC"</c>, tuc
/// moi chuoi la deu roi vao nhom "ket qua kham". HIS go sai mot ky tu
/// (<c>DON_THUOOC</c>) la don thuoc im lang nhay sang nhom khac ma khong ai
/// biet. Nay cua API chan ngay tu dau.
/// </para>
/// </summary>
public static class LoaiTaiLieu
{
    public const string DonThuoc = "DON_THUOC";
    public const string KetQuaXn = "KET_QUA_XN";
    public const string Cdha = "CDHA";
    public const string GiayRaVien = "GIAY_RA_VIEN";

    /// <summary>Ten nhom tren URL — <c>/benh-nhan/tai-lieu?nhom=...</c>.</summary>
    public const string NhomDonThuoc = "don-thuoc";
    public const string NhomKetQuaKham = "ket-qua-kham";
    public const string NhomTatCa = "tat-ca";

    public static readonly string[] TatCa = { DonThuoc, KetQuaXn, Cdha, GiayRaVien };

    /// <summary>
    /// Mang chu khong phai HashSet: EF dich <c>mang.Contains(cot)</c> thanh
    /// <c>IN (...)</c>, con HashSet thi khong.
    /// </summary>
    public static readonly string[] MaCuaNhomKetQuaKham = { KetQuaXn, Cdha, GiayRaVien };

    public static bool HopLe(string? ma) =>
        !string.IsNullOrWhiteSpace(ma) && TatCa.Contains(ma);

    /// <summary>Danh sach ma de nhet vao cau bao loi cho ben HIS doc.</summary>
    public static string DanhSachChoNguoiDoc() => string.Join(", ", TatCa);

    public static string TenHienThi(string? ma) => ma switch
    {
        DonThuoc => "Đơn thuốc",
        KetQuaXn => "Kết quả xét nghiệm",
        Cdha => "CĐHA / X-Quang",
        GiayRaVien => "Giấy ra viện",
        // Chin dong tai lieu cu trong CSDL co the mang ma ngoai danh muc — van
        // phai hien duoc, khong duoc lam vo man.
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
