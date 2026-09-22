namespace SixosPwa.Models;

public class TaiKhoan
{
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";

    /// <summary>
    /// Mật khẩu đăng nhập nội bộ. Đợt A đã xóa bảng <c>HT_TaiKhoanDoiTac</c> nên
    /// đây là chỗ DUY NHẤT còn giữ mật khẩu (ADR 0009).
    /// Sau migration cột này đang NULL: phần BĂM chưa được thi hành (xem mục Đính chính
    /// của ADR 0009), nên hai tài khoản Admin/DoiTac tạm thời không đăng nhập được.
    /// </summary>
    public string? MatKhauNoiBo { get; set; }

    // 🔴 Từ đợt 1B bảng này CHỈ CÒN ADMIN: CK_HT_TaiKhoan_Role CHECK (Role='Admin').
    //    Bệnh nhân không còn tài khoản — "lối vào" của họ là dòng DM_BenhNhan có
    //    đúng SDT tại đúng cơ sở (luật C7b). Xem ADR 0027 (đã đảo) và ADR 0040.

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
