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

    // 🔴 Cot HT_TaiKhoan.IDBenhNhan da BI BO (dot A, §3). Chieu dung la nguoc
    //    lai: DM_BenhNhan.IDTaiKhoan tro ve tai khoan quan minh (ADR 0019) — mot
    //    tai khoan quan NHIEU ho so, nen khoa 1-1 cu la sai ban chat.

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
