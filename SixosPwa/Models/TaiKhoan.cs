namespace SixosPwa.Models;

public class TaiKhoan
{
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";

    /// <summary>
    /// Mat khau dang nhap noi bo cua Admin / DoiTac. Tach han khoi mat khau doi tac
    /// (<see cref="TaiKhoanDoiTac.MatKhau"/>) — xem ADR 0009.
    /// Sau migration cot nay dang NULL: phan BAM chua duoc thi hanh (xem muc Dinh chinh
    /// cua ADR 0009), nen hai tai khoan Admin/DoiTac tam thoi khong dang nhap duoc.
    /// </summary>
    public string? MatKhauNoiBo { get; set; }

    /// <summary>Ho so con nguoi tuong ung. NULL voi Admin/DoiTac va voi tai khoan vua dang ky OTP.</summary>
    public long? IdBenhNhan { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
