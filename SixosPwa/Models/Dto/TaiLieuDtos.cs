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
    /// Dinh danh phieu ben HIS (IDPhieuCLS, IDToaThuoc...). Cung
    /// (co so, loai, maNguonHIS) => day lai KHONG de dong trung; noi dung doi
    /// => them mot phien ban moi va chi ban moi nhat duoc hien.
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

/// <summary>
/// DTO dữ liệu trả về khi tiếp nhận tài liệu thành công.
/// </summary>
public class TiepNhanTaiLieuResponseData
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("idBenhNhanCoSo")]
    public long? IdBenhNhanCoSo { get; set; }

    /// <summary>
    /// true = noi dung y het ban dang co nen cong GIU NGUYEN ban do: khong them
    /// phien ban, khong upload tep moi. HIS ghi nhat ky la THANH CONG kem thong
    /// diep "noi dung khong doi" — nguoi o quay thay dung su that thay vi tuong
    /// vua tao mot ban moi.
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
