namespace SixosPwa.Models;

public class DoiTac
{
    public long Id { get; set; }
    public string MaDT { get; set; } = "";
    public string TenDT { get; set; } = "";
    public string? DiaChi { get; set; }
    public string? SDT { get; set; }
    public string? Email { get; set; }
    // 🔴 Cot DM_DoiTac.IDPM da BI BO (dot A, §3) — chua bao gio duoc dung.
    public string? BrandName { get; set; }

    /// <summary>Doi ten tu Password — tranh nham voi mat khau dang nhap noi bo (ADR 0009).</summary>
    public string? MatKhauDoiTac { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
}
