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
    public long? IdBenhNhan { get; set; }

    // 🔴 Ba cột MaBN / DungLuongByte / GhiChu đã BỊ BỎ khỏi QL_TaiLieuBenhNhan
    //    (đợt A, §3). MaBN suy qua IDBenhNhanCoSo -> DM_BenhNhanCoSo.MaBN.
    //    DTO `TaiLieuDtos` VẪN GIỮ `GhiChu`/`MaBenhNhan` vì HIS đang gửi lên —
    //    nhận rồi BỎ, không INSERT (tiền lệ V14).

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

    /// <summary>Ngày khám / ngày phát hành tài liệu.</summary>
    public DateTime? NgayKham { get; set; }

    /// <summary>Định danh phiếu bên HIS — nửa kia của khóa tự nhiên chống trùng.</summary>
    public string? MaNguonHIS { get; set; }

    /// <summary>
    /// Băm SHA-256 (hex chữ thường) của chính nội dung PDF. NULL = dòng cũ chưa
    /// biết băm => không so được, cứ đẩy như cũ. Thêm 08/09 để đẩy lại cùng nội
    /// dung không đẻ phiên bản mới và không bỏ lại file thừa trên FTP.
    /// </summary>
    public string? BamNoiDung { get; set; }

    public int PhienBan { get; set; } = 1;

    /// <summary>
    /// Kết quả bị sửa/ký lại thì đẩy thêm một phiên bản mới; chỉ bản mang cờ
    /// này được hiện cho bệnh nhân. Các bản cũ giữ lại làm đối chứng.
    /// </summary>
    public bool LaBanMoiNhat { get; set; } = true;

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
