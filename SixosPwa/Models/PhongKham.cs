namespace SixosPwa.Models;

/// <summary>Thông tin phòng khám mà bệnh nhân đã đến khám</summary>
public class PhongKham
{
    public long Id { get; set; }

    /// <summary>Mã phòng khám (ví dụ: PK001, PK002)</summary>
    public string MaPhongKham { get; set; } = "";

    /// <summary>Tên phòng khám</summary>
    public string TenPhongKham { get; set; } = "";

    /// <summary>Địa chỉ phòng khám</summary>
    public string? DiaChi { get; set; }

    /// <summary>Số điện thoại liên hệ</summary>
    public string? SoDienThoai { get; set; }

    /// <summary>Mô tả / Chuyên khoa</summary>
    public string? MoTa { get; set; }

    /// <summary>Logo / Icon phòng khám (URL)</summary>
    public string? LogoUrl { get; set; }
}
