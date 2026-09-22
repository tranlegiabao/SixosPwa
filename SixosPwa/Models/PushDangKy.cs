namespace SixosPwa.Models;

/// <summary>Lưu thông tin đăng ký Web Push của từng thiết bị.</summary>
public class PushDangKy
{
    public long Id { get; set; }

    /// <summary>Khóa ngoại sang HT_TaiKhoan — trước đây là chuỗi số điện thoại.</summary>
    /// <summary>Hồ sơ nhận push. Từ 1B trỏ tới <c>DM_BenhNhan(ID)</c>, không còn là tài khoản (C15/PA-2a).</summary>
    public long IdBenhNhan { get; set; }

    /// <summary>Khóa ngoại sang HT_ThietBi. Có thể rỗng nếu chưa nhận diện được thiết bị.</summary>
    // 🔴 Cột HT_PushDangKy.IDThietBi đã BỊ BỎ cùng với bảng HT_ThietBi (đợt A, §3).

    /// <summary>Push endpoint URL do browser cung cấp.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Public key ECDH của browser (base64url).</summary>
    public string P256dh { get; set; } = "";

    /// <summary>Auth secret của browser (base64url).</summary>
    public string Auth { get; set; } = "";

    public DateTime ThoiGian { get; set; } = DateTime.Now;
}
