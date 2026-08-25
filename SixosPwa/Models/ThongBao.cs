namespace SixosPwa.Models;

public class ThongBao
{
    public long Id { get; set; }

    /// <summary>Khoa ngoai sang HT_TaiKhoan — truoc day la chuoi so dien thoai.</summary>
    public long IdNguoiGui { get; set; }

    /// <summary>Khoa ngoai sang HT_TaiKhoan — truoc day la chuoi so dien thoai.</summary>
    public long IdNguoiNhan { get; set; }

    public string NoiDung { get; set; } = "";

    public DateTime ThoiGian { get; set; } = DateTime.Now;

    public bool DaDoc { get; set; }
}
