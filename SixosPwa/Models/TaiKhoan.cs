namespace SixosPwa.Models;

public class TaiKhoan
{
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";

    /// <summary>
    /// Mat khau dang nhap noi bo. Dot A da xoa bang <c>HT_TaiKhoanDoiTac</c> nen
    /// day la cho DUY NHAT con giu mat khau (ADR 0009).
    /// Sau migration cot nay dang NULL: phan BAM chua duoc thi hanh (xem muc Dinh chinh
    /// cua ADR 0009), nen hai tai khoan Admin/DoiTac tam thoi khong dang nhap duoc.
    /// </summary>
    public string? MatKhauNoiBo { get; set; }

    // 🔴 Tu dot 1B bang nay CHI CON ADMIN: CK_HT_TaiKhoan_Role CHECK (Role='Admin').
    //    Benh nhan khong con tai khoan — "loi vao" cua ho la dong DM_BenhNhan co
    //    dung SDT tai dung co so (luat C7b). Xem ADR 0027 (da dao) va ADR 0034.

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
