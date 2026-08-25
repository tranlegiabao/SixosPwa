namespace SixosPwa.Models;

public class ThietBi
{
    public long Id { get; set; }

    /// <summary>Khoa ngoai sang HT_TaiKhoan — truoc day la chuoi so dien thoai.</summary>
    public long IdTaiKhoan { get; set; }

    /// <summary>Ma thiet bi do trinh duyet sinh. Ten cu IdThietBi rat de nham voi khoa chinh.</summary>
    public string MaThietBi { get; set; } = "";

    public string? TenThietBi { get; set; }

    public bool TrangThai { get; set; } = true;

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
