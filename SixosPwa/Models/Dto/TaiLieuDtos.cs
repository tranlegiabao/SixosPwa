using System.Text.Json.Serialization;

namespace SixosPwa.Models.Dto;

/// <summary>
/// DTO nhận vào từ Body JSON khi các hệ thống bên ngoài gọi API tiếp nhận tài liệu.
/// </summary>
public class TiepNhanTaiLieuRequest
{
    [JsonPropertyName("maBenhNhan")]
    public string MaBenhNhan { get; set; } = "";

    [JsonPropertyName("filePdf")]
    public string FilePdf { get; set; } = "";

    [JsonPropertyName("tenTaiLieu")]
    public string? TenTaiLieu { get; set; }

    [JsonPropertyName("ngayKham")]
    public DateTime? NgayKham { get; set; }

    [JsonPropertyName("ghiChu")]
    public string? GhiChu { get; set; }
}

/// <summary>
/// DTO dữ liệu trả về khi tiếp nhận tài liệu thành công.
/// </summary>
public class TiepNhanTaiLieuResponseData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

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

/// <summary>
/// Wrapper phản hồi chuẩn của API.
/// </summary>
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
