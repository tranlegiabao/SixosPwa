namespace SixosPwa.Models;

/// <summary>
/// Cấu hình hệ thống (nguyên mẫu từ bảng HT_Config của hệ thống HIS).
/// </summary>
public class HTConfig
{
    public long Id { get; set; }

    /// <summary>Mã chức năng / cấu hình (vd: GONOI, QLSTT, ...).</summary>
    public string? MaChucNang { get; set; }

    // 🔴 Đợt A (bước A8): cột HT_Config.SoLuong ĐÃ BỊ XOÁ khỏi DB, gộp vào GiaTri.
    // Thực thể này được ApplicationDbContext map theo quy ước (mọi property = một cột),
    // nên chỉ cần khai lại SoLuong là EF sinh SELECT [SoLuong] ⇒ nổ
    // "Invalid column name 'SoLuong'" lúc CHẠY (build vẫn xanh). Mọi nơi cần con số
    // đọc GiaTri (xem HTConfigService.LaySoLuongAsync).

    /// <summary>Cờ bật/tắt (true = Có hiệu lực / Bật, false = Tắt).</summary>
    public bool? HieuLuc { get; set; }

    /// <summary>Diễn giải ý nghĩa cấu hình.</summary>
    public string? Ghichu { get; set; }

    /// <summary>Mốc ngày áp dụng (nếu có).</summary>
    public DateTime? Ngay { get; set; }

    /// <summary>Giá trị số nguyên tùy chọn.</summary>
    public int? GiaTri { get; set; }

    /// <summary>Phân nhóm chức năng (vd: Hồ sơ, Tiếp nhận, Tài chính, ...).</summary>
    public string? Nhom { get; set; }
}
