namespace SixosPwa.Models;

/// <summary>
/// Cấu hình kho FTP <b>của phòng khám</b> mà cổng chỉ ĐỌC — "Kho phiếu cơ sở".
///
/// Phục vụ chế độ <b>Trỏ đường</b> (ADR 0030): tài liệu đã ký số nằm sẵn trên FTP
/// của cơ sở, cổng không giữ byte mà chỉ giữ đường rồi kéo về khi bệnh nhân mở.
/// 🔴 Đừng gọi chế độ này là "đẩy thẳng" — chữ <i>Đẩy</i> ở CONTEXT.md nghĩa là HIS
/// chủ động gửi byte lên, mà ở đây byte đi NGƯỢC chiều (cổng tự kéo).
///
/// 🔴 Khác <see cref="KhoaApiCoSo"/> ở hai điểm, đừng chép khuôn quá tay:
/// <list type="number">
///   <item>Mỗi cơ sở đúng MỘT kho (UNIQUE IDCoSo). Khóa API cố ý 1-nhiều vì cần xoay khóa.</item>
///   <item><see cref="MatKhau"/> lưu THÔ, không băm — FTP cần đăng nhập nên phải đọc lại được
///   (chốt 36). Khóa API chỉ cần so sánh nên băm được.</item>
/// </list>
/// </summary>
public class KhoFtpCoSo
{
    public long Id { get; set; }

    /// <summary>Khóa ngoại sang <see cref="DMCSKCB"/>. Duy nhất — mỗi cơ sở một kho.</summary>
    public long IdCoSo { get; set; }

    /// <summary>Máy chủ FTP của phòng khám (không kèm lược đồ <c>ftp://</c>).</summary>
    public string Host { get; set; } = "";

    public string TaiKhoan { get; set; } = "";

    /// <summary>🔴 Lưu THÔ. Xem ghi chú của lớp.</summary>
    public string MatKhau { get; set; } = "";

    /// <summary>
    /// Thư mục gốc trên FTP của phòng khám. Mặc định RỖNG: đo 135/135 đường
    /// <c>URLKySo</c> trên Dev_Master3 đều tính từ gốc FTP ("77121/CongVan/...")
    /// nên không cần tiền tố nào (chốt 39).
    /// </summary>
    public string ThuMucGoc { get; set; } = "";

    /// <summary>Mặc định TẮT — chưa Thử kết nối đạt thì không bật được (chốt 41).</summary>
    public bool Active { get; set; }

    /// <summary>
    /// Lần bấm <i>Thử kết nối kho</i> gần nhất và ĐẠT. Đổi Host/TàiKhoản/MậtKhẩu/ThưMụcGốc
    /// thì stored xóa mốc này — lần thử cũ hết hiệu lực.
    /// </summary>
    public DateTime? NgayThuDat { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    public DateTime? NgaySua { get; set; }
}
