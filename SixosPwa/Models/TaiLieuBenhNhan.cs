namespace SixosPwa.Models;

/// <summary>
/// Tài liệu y tế của bệnh nhân (đơn thuốc, kết quả xét nghiệm, CDHA, giấy ra viện,...)
/// do HIS của cơ sở y tế đẩy vào qua API tiếp nhận tài liệu.
/// </summary>
public class TaiLieuBenhNhan
{
    public long Id { get; set; }

    /// <summary>Khóa ngoại sang <see cref="DMCSKCB"/>.</summary>
    public long IdCoSo { get; set; }

    /// <summary>Khóa ngoại sang <see cref="BenhNhanCoSo"/> (null nếu bệnh nhân chưa có hồ sơ tại cơ sở).</summary>
    public long? IdBenhNhanCoSo { get; set; }

    /// <summary>Mã bệnh nhân do chính cơ sở cấp (MaBN trong HIS).</summary>
    public string MaBN { get; set; } = "";

    /// <summary>Loại tài liệu (DON_THUOC, KET_QUA_XN, CDHA, GIAY_RA_VIEN,...).</summary>
    public string LoaiTaiLieu { get; set; } = "";

    /// <summary>Tên hiển thị / tiêu đề của tài liệu.</summary>
    public string TenTaiLieu { get; set; } = "";

    /// <summary>Đường dẫn lưu file trên FTP. Kho nào thì xem <see cref="NguonKho"/>.</summary>
    public string DuongDanFtp { get; set; } = "";

    /// <summary>
    /// Tài liệu này nằm ở KHO NÀO: <c>CONG</c> = kho FTP của chính cổng
    /// (<c>sixospwa/...</c>, cổng tự ghi) · <c>COSO</c> = "Kho phiếu cơ sở" —
    /// FTP của phòng khám, cổng CHỈ ĐỌC (chế độ Trỏ đường, ADR 0030).
    ///
    /// 🔴 Cố ý là một CỘT, không suy từ tiền tố <c>sixospwa/</c> trong chuỗi: hai kho
    /// cùng tồn tại cho cùng một cơ sở, và luật ngầm nằm trong chuỗi thì đọc nhầm kho
    /// mà không báo gì (chốt 35). Dùng hằng <see cref="Services.NguonKhoTaiLieu"/>.
    /// </summary>
    public string NguonKho { get; set; } = "CONG";

    /// <summary>Dung lượng file (byte).</summary>
    public long DungLuongByte { get; set; }

    /// <summary>Ngày khám / ngày phát hành tài liệu.</summary>
    public DateTime? NgayKham { get; set; }

    /// <summary>Ghi chú bổ sung.</summary>
    public string? GhiChu { get; set; }

    /// <summary>Dinh danh phieu ben HIS — nua kia cua khoa tu nhien chong trung.</summary>
    public string? MaNguonHIS { get; set; }

    /// <summary>
    /// Bam SHA-256 (hex chu thuong) cua chinh noi dung PDF. NULL = dong cu chua
    /// biet bam => khong so duoc, cu day nhu cu. Them 08/09 de day lai cung noi
    /// dung khong de phien ban moi va khong bo lai file thua tren FTP.
    /// </summary>
    public string? BamNoiDung { get; set; }

    public int PhienBan { get; set; } = 1;

    /// <summary>
    /// Ket qua bi sua/ky lai thi day them mot phien ban moi; chi ban mang co
    /// nay duoc hien cho benh nhan. Cac ban cu giu lai lam doi chung.
    /// </summary>
    public bool LaBanMoiNhat { get; set; } = true;

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
