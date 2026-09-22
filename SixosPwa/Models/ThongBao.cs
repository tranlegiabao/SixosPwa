namespace SixosPwa.Models;

public class ThongBao
{
    public long Id { get; set; }

    /// <summary>Khóa ngoại sang HT_TaiKhoan — trước đây là chuỗi số điện thoại.</summary>
    public long IdNguoiGui { get; set; }

    /// <summary>Khóa ngoại sang HT_TaiKhoan — trước đây là chuỗi số điện thoại.</summary>
    public long IdNguoiNhan { get; set; }

    public string NoiDung { get; set; } = "";

    public DateTime ThoiGian { get; set; } = DateTime.Now;

    public bool DaDoc { get; set; }
}
