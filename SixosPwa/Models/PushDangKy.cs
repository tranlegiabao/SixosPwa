namespace SixosPwa.Models;

/// <summary>Lưu thông tin đăng ký Web Push của từng thiết bị</summary>
public class PushDangKy
{
    public long Id { get; set; }

    /// <summary>SĐT tài khoản sở hữu thiết bị này</summary>
    public string SDT { get; set; } = "";

    /// <summary>Push endpoint URL do browser cung cấp</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Public key ECDH của browser (base64url)</summary>
    public string P256dh { get; set; } = "";

    /// <summary>Auth secret của browser (base64url)</summary>
    public string Auth { get; set; } = "";

    /// <summary>Thời điểm đăng ký</summary>
    public DateTime ThoiGian { get; set; } = DateTime.Now;

    public string? IdThietBi { get; set; }
}
