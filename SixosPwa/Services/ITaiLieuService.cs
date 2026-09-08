using SixosPwa.Models;
using SixosPwa.Models.Dto;

namespace SixosPwa.Services;

public interface ITaiLieuService
{
    /// <summary>
    /// Tiếp nhận, giải mã Base64, kiểm tra định dạng PDF, upload lên FTP và lưu metadata vào DB.
    /// </summary>
    Task<TiepNhanTaiLieuResponseData> TiepNhanTaiLieuAsync(
        DMCSKCB cskcb,
        string loaiTaiLieu,
        TiepNhanTaiLieuRequest request);

    /// <summary>
    /// Lấy thông tin tài liệu theo ID.
    /// </summary>
    Task<TaiLieuBenhNhan?> LayTaiLieuAsync(long id);

    /// <summary>
    /// Tải luồng file PDF từ FTP để trả về cho người xem.
    /// </summary>
    Task<Stream> TaiStreamPdfAsync(string duongDanFtp);
}
