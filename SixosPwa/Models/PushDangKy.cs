namespace SixosPwa.Models;

/// <summary>Luu thong tin dang ky Web Push cua tung thiet bi.</summary>
public class PushDangKy
{
    public long Id { get; set; }

    /// <summary>Khoa ngoai sang HT_TaiKhoan — truoc day la chuoi so dien thoai.</summary>
    public long IdTaiKhoan { get; set; }

    /// <summary>Khoa ngoai sang HT_ThietBi. Co the rong neu chua nhan dien duoc thiet bi.</summary>
    // 🔴 Cot HT_PushDangKy.IDThietBi da BI BO cung voi bang HT_ThietBi (dot A, §3).

    /// <summary>Push endpoint URL do browser cung cap.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Public key ECDH cua browser (base64url).</summary>
    public string P256dh { get; set; } = "";

    /// <summary>Auth secret cua browser (base64url).</summary>
    public string Auth { get; set; } = "";

    public DateTime ThoiGian { get; set; } = DateTime.Now;
}
