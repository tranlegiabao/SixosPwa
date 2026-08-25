namespace SixosPwa.Models;

/// <summary>
/// Lien ket mot tai khoan SixosPwa voi mot co so doi tac, kem credential ben do.
/// Tach rieng thay vi nhet them cot vao TaiKhoan, vi mot benh nhan co the co
/// nhieu co so doi tac voi mat khau khac nhau.
/// </summary>
public class TaiKhoanDoiTac
{
    public long Id { get; set; }

    public long IdTaiKhoan { get; set; }

    /// <summary>Khoa ngoai sang <see cref="DMCSKCB"/> — truoc day la chuoi MaCoSo.</summary>
    public long IdCoSo { get; set; }

    /// <summary>
    /// Mat khau ben doi tac. Luu DOC LAI DUOC, khong bam — bat buoc de POST
    /// nguyen van sang trang dang nhap cua doi tac. Xem ADR 0005.
    /// </summary>
    public string? MatKhau { get; set; }

    /// <summary>
    /// Ma xac nhan doi tac vua tra ve khi mo tai khoan. Chi dung MOT lan o man
    /// Ban giao roi xoa ngay — nhung lan sau di duong dang nhap thuan.
    /// </summary>
    public string? MaXacNhanTam { get; set; }

    public bool DaLienKet { get; set; }

    public DateTime? NgayLienKet { get; set; }
}
