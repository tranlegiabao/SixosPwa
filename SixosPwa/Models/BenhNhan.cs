namespace SixosPwa.Models;

/// <summary>
/// Một HỒ SƠ = một CON NGƯỜI TẠI MỘT CƠ SỞ (đợt 1B).
/// Trước 1B khái niệm này tách làm hai bảng; nay gộp lại, và một người khám ở
/// N cơ sở thì có N dòng — đó là "chấp nhận lặp dòng" của C12.
/// Xem CONTEXT.md mục *Dòng neo* / *Thăng cấp* / *Nhân bản*.
/// </summary>
public class BenhNhan
{
    public long Id { get; set; }
    public string CCCD { get; set; } = "";
    public string TenBN { get; set; } = "";
    public string? SDT { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }

    /// <summary>
    /// Cơ sở mà hồ sơ này thuộc về. <b>NULL = DÒNG NEO</b> — trạng thái quá độ
    /// giữa *cửa 1* và *cửa 4* của luồng HIS đẩy sang (PA-C1).
    ///
    /// <para>
    /// 🔴 MỌI câu đọc phục vụ giao diện PHẢI lọc <c>IdCoSo != null</c>. Dòng neo
    /// không được hiện ở màn nào và không đăng nhập được. Xem ADR 0040.
    /// </para>
    /// </summary>
    public long? IdCoSo { get; set; }

    /// <summary>
    /// Mã do CƠ SỞ cấp, duy nhất trong phạm vi cơ sở (không duy nhất toàn hệ).
    /// RỖNG khi hồ sơ còn là *tự khai* — cổng không tự bịa mã (chốt 12 đợt 1).
    /// </summary>
    public string? MaBN { get; set; }

    /// <summary>
    /// *Cửa tài liệu* (chốt 9 đợt 1, ADR 0020): hồ sơ này đã được phép mở kết
    /// quả cận lâm sàng / đơn thuốc chưa. Đọc nó là "cơ sở đã ghi bản là đầu mối
    /// liên lạc của người này", KHÔNG phải "bạn chính là người này" — CCCD gõ
    /// lúc đăng nhập KHÔNG được xác thực, OTP chỉ xác thực số điện thoại.
    /// </summary>
    public bool DaMoTaiLieu { get; set; }

    /// <summary>
    /// *Mốc xem lịch* (ADR 0025) — lần gần nhất người dùng mở ở *Lịch khám của tôi*.
    /// MỘT CỘT, không phải bảng "đã đọc từng mục": lịch hẹn là TRẠNG THÁI xem đi
    /// xem lại. NULL = chưa mở lần nào => mọi mục đều là mới.
    /// </summary>
    public DateTime? NgayXemLichCuoi { get; set; }

    /// <summary>Một trong BỐN ô của luật gộp hồ sơ (ADR 0018, bản sửa đổi 2026-09-09).</summary>
    public DateTime? NgaySinh { get; set; }

    /// <summary>
    /// Ô thứ TƯ của luật gộp, thêm 2026-09-09. Giữ nguyên MÃ của HIS
    /// (<c>DM_GioiTinh.MaGioiTinh</c>): "1" Nam, "2" Nữ, "3" Chưa xác định —
    /// không dịch sang bit/enum vì dịch là thêm một chỗ để lệch.
    ///
    /// <para>
    /// Vì sao thêm: bỏ CCCD ra khỏi phép khớp thì còn <b>348 nhóm</b> trùng cả họ
    /// tên, ngày sinh lẫn giới tính mà CCCD hợp lệ KHÁC NHAU — chắc chắn là hai con
    /// người. Ba ô không đủ chặt.
    /// </para>
    /// </summary>
    public string? GioiTinh { get; set; }

    /// <summary>
    /// Tên đã chuẩn hóa bỏ dấu — một ô của luật gộp. Bên HIS cột cùng tên này
    /// RỖNG 100% (74.725/74.725) nên đây là kết quả chuẩn hóa của CỔNG, không
    /// phải bản chép từ HIS.
    /// </summary>
    public string? HoTenKhongDau { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
}

/// <summary>
/// Giới tính. 🔴 Đợt A đã XOÁ bảng <c>DM_GioiTinh</c> — đây KHÔNG còn là thực thể EF
/// (không có DbSet, không có mapping). Ba giá trị <c>1=Nam · 2=Nữ · 3=Không xác định</c>
/// nay là HẰNG trong C# + <c>CHECK</c> trên <c>DM_BenhNhan.GioiTinh</c>.
/// Lớp này giữ lại chỉ để các màn đang dựng danh mục tại chỗ không phải viết lại kiểu.
/// </summary>
public class DMGioiTinh
{
    public string MaGioiTinh { get; set; } = "";
    public string TenGioiTinh { get; set; } = "";
}

