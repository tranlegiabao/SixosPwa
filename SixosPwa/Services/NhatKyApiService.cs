namespace SixosPwa.Services;

/// <summary>Ket qua mot cuoc goi vao khu API — gia tri di thang vao cot KetQua.</summary>
public static class KetQuaApi
{
    public const string Nhan = "NHAN";
    public const string TuChoi = "TU_CHOI";
    public const string Loi = "LOI";
}

/// <summary>
/// Ly do bi tu choi. Chuoi co dinh chu khong phai cau tieng Viet: day la cot de
/// GROUP BY luc doi soat, khong phai cau de doc.
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
}

public interface INhatKyApi
{
    Task GhiAsync(
        string endpoint,
        string ketQua,
        long? idCoSo = null,
        long? idKhoa = null,
        string? maBN = null,
        string? maNguonHIS = null,
        string? lyDo = null,
        int? soLuong = null,
        string? ipGoi = null);
}

/// <summary>
/// Nhat ky doi soat cua khu API nhan. Ghi qua stored procedure theo ADR 0008.
///
/// <para>
/// Quy uoc: MOI cuoc goi de lai DUNG MOT dong. Hoac attribute xac thuc chan lai
/// va ghi dong tu choi, hoac controller chay xong va ghi dong ket qua nghiep vu.
/// Khong ghi ca hai, neu khong moi cuoc goi thanh hai dong va so lieu doi soat
/// dem gap doi.
/// </para>
/// <para>
/// Khong bao gio ghi noi dung tep vao day.
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
        long? idKhoa = null,
        string? maBN = null,
        string? maNguonHIS = null,
        string? lyDo = null,
        int? soLuong = null,
        string? ipGoi = null)
    {
        try
        {
            await _thuTuc.GhiLogApiCoSoAsync(
                idCoSo, idKhoa, endpoint, maBN, maNguonHIS, ketQua, lyDo, soLuong, ipGoi);
        }
        catch (Exception ex)
        {
            // Nhat ky hong KHONG duoc lam hong cuoc goi nghiep vu: mat mot dong
            // doi soat con hon tu choi mot phieu ket qua that.
            _logger.LogError(ex, "Khong ghi duoc nhat ky API ({Endpoint}, {KetQua})", endpoint, ketQua);
        }
    }
}
