namespace SixosPwa.Models;

public class ThongBao
{
    public long Id { get; set; }

    /// <summary>Nội dung tin nhắn</summary>
    public string NoiDung { get; set; } = "";

    /// <summary>Thời gian gửi</summary>
    public DateTime ThoiGian { get; set; } = DateTime.Now;

    /// <summary>SĐT / tên người gửi (Admin hoặc DoiTac)</summary>
    public string NguoiGui { get; set; } = "";

    /// <summary>SĐT tài khoản nhận thông báo (bệnh nhân)</summary>
    public string NguoiNhan { get; set; } = "";

    /// <summary>Đã đọc hay chưa</summary>
    public bool DaDoc { get; set; } = false;
}
