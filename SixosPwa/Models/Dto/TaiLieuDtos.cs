using System.Text.Json.Serialization;

namespace SixosPwa.Models.Dto;

/// <summary>
/// DTO nhận vào từ Body JSON khi các hệ thống bên ngoài gọi API tiếp nhận tài liệu.
/// </summary>
public class TiepNhanTaiLieuRequest
{
    [JsonPropertyName("maBenhNhan")]
    public string MaBenhNhan { get; set; } = "";

    [JsonPropertyName("cccd")]
    public string? Cccd { get; set; }

    [JsonPropertyName("sdt")]
    public string? Sdt { get; set; }

    [JsonPropertyName("hoTen")]
    public string? HoTen { get; set; }

    /// <summary>
    /// Định danh phiếu bên HIS (IDPhieuCLS, IDToaThuoc...). Cùng
    /// (cơ sở, loại, maNguonHIS) => đẩy lại KHÔNG đẻ dòng trùng; nội dung đổi
    /// => thêm một phiên bản mới và chỉ bản mới nhất được hiện.
    /// </summary>
    [JsonPropertyName("maNguonHIS")]
    public string? MaNguonHIS { get; set; }

    [JsonPropertyName("filePdf")]
    public string FilePdf { get; set; } = "";

    [JsonPropertyName("tenTaiLieu")]
    public string? TenTaiLieu { get; set; }

    [JsonPropertyName("ngayKham")]
    public DateTime? NgayKham { get; set; }

    /// <summary>
    /// 🔴 GIỮ NGUYÊN dù cột <c>QL_TaiLieuBenhNhan.GhiChu</c> đã bị bỏ ở đợt A:
    /// HIS đang gửi lên, bỏ tham số là vỡ bên HIS (tiền lệ V14). Nhận rồi BỎ.
    /// </summary>
    [JsonPropertyName("ghiChu")]
    public string? GhiChu { get; set; }
}

public class TiepNhanTaiLieuResponseData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("idBenhNhan")]
    public long? IdBenhNhan { get; set; }

    /// <summary>
    /// true = nội dung y hệt bản đang có nên cổng GIỮ NGUYÊN bản đó: không thêm
    /// phiên bản, không upload tệp mới. HIS ghi nhật ký là THÀNH CÔNG kèm thông
    /// điệp "nội dung không đổi" — người ở quầy thấy đúng sự thật thay vì tưởng
    /// vừa tạo một bản mới.
    /// </summary>
    [JsonPropertyName("noiDungKhongDoi")]
    public bool NoiDungKhongDoi { get; set; }

    [JsonPropertyName("maBN")]
    public string MaBN { get; set; } = "";

    [JsonPropertyName("loaiTaiLieu")]
    public string LoaiTaiLieu { get; set; } = "";

    [JsonPropertyName("tenTaiLieu")]
    public string TenTaiLieu { get; set; } = "";

    [JsonPropertyName("duongDan")]
    public string DuongDan { get; set; } = "";

    [JsonPropertyName("dungLuongByte")]
    public long DungLuongByte { get; set; }

    [JsonPropertyName("ngayTao")]
    public DateTime NgayTao { get; set; }
}

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Thành công") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? new List<string>() };
}
