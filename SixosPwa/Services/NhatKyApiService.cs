namespace SixosPwa.Services;

/// <summary>Kết quả một cuộc gọi vào khu API — giá trị đi thẳng vào cột KetQua.</summary>
public static class KetQuaApi
{
    public const string Nhan = "NHAN";
    public const string TuChoi = "TU_CHOI";
    public const string Loi = "LOI";
}

/// <summary>
/// Lý do bị từ chối. Chuỗi cố định chứ không phải câu tiếng Việt: đây là cột để
/// GROUP BY lúc đối soát, không phải câu để đọc.
/// </summary>
public static class LyDoApi
{
    public const string ThieuHeader = "THIEU_HEADER";
    public const string KhoaSai = "KHOA_SAI";
    public const string KhoaTat = "KHOA_TAT";
    public const string KhoaHetHan = "KHOA_HET_HAN";
    public const string KhoaKhacCoSo = "KHOA_KHAC_CO_SO";
    public const string ChuaCoNguoiNhan = "CHUA_CO_NGUOI_NHAN";
    public const string DuLieuSai = "DU_LIEU_SAI";
    public const string LoaiTaiLieuLa = "LOAI_TAI_LIEU_LA";
}

public interface INhatKyApi
{
    Task GhiAsync(
        string endpoint,
        string ketQua,
        long? idCoSo = null,
        string? maBN = null,
        string? maNguonHIS = null,
        string? lyDo = null,
        int? soLuong = null,
        string? ipGoi = null);
}

/// <summary>
/// Nhật ký đối soát của khu API nhận. Ghi qua stored procedure theo ADR 0008.
///
/// <para>
/// Quy ước: MỖI cuộc gọi để lại ĐÚNG MỘT dòng. Hoặc attribute xác thực chặn lại
/// và ghi dòng từ chối, hoặc controller chạy xong và ghi dòng kết quả nghiệp vụ.
/// Không ghi cả hai, nếu không mỗi cuộc gọi thành hai dòng và số liệu đối soát
/// đếm gấp đôi.
/// </para>
/// <para>
/// Không bao giờ ghi nội dung tệp vào đây.
/// </para>
/// </summary>
public class NhatKyApiService : INhatKyApi
{
    private readonly AdminStoredProcedureService _thuTuc;
    private readonly ILogger<NhatKyApiService> _logger;

    public NhatKyApiService(AdminStoredProcedureService thuTuc, ILogger<NhatKyApiService> logger)
    {
        _thuTuc = thuTuc;
        _logger = logger;
    }

    public async Task GhiAsync(
        string endpoint,
        string ketQua,
        long? idCoSo = null,
        string? maBN = null,
        string? maNguonHIS = null,
        string? lyDo = null,
        int? soLuong = null,
        string? ipGoi = null)
    {
        try
        {
            // 🔴 Cột HT_LogApiCoSo.IDKhoa đã bị xoá và tham số @IDKhoa đã gỡ khỏi stored
            // HT_LogApiCoSo_Ghi (gộp HT_KhoaApiCoSo vào DM_CSKCB). Còn truyền idKhoa vào
            // đây thì SqlException nuốt ở catch dưới ⇒ nhật ký API chết trong im lặng.
            await _thuTuc.GhiLogApiCoSoAsync(
                idCoSo, endpoint, maBN, maNguonHIS, ketQua, lyDo, soLuong, ipGoi);
        }
        catch (Exception ex)
        {
            // Nhật ký hỏng KHÔNG được làm hỏng cuộc gọi nghiệp vụ: mất một dòng
            // đối soát còn hơn từ chối một phiếu kết quả thật.
            _logger.LogError(ex, "Khong ghi duoc nhat ky API ({Endpoint}, {KetQua})", endpoint, ketQua);
        }
    }
}
