using SixosPwa.Models;
using SixosPwa.Models.Dto;

namespace SixosPwa.Services;

public interface ITaiLieuService
{
    Task<TiepNhanTaiLieuResponseData> TiepNhanTaiLieuAsync(
        DMCSKCB cskcb,
        string loaiTaiLieu,
        TiepNhanTaiLieuRequest request);

    Task<TaiLieuBenhNhan?> LayTaiLieuAsync(long id);

    Task<Stream> TaiStreamPdfAsync(string duongDanFtp);
}
