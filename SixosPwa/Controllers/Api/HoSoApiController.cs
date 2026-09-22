using Microsoft.AspNetCore.Mvc;
using SixosPwa.Models.Dto;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Controllers.Api;

/// <summary>
/// Đường ĐỌC cho HIS: hỏi theo lô xem mã bệnh nhân nào đã có người nhận bên cổng.
///
/// <para>
/// Vì sao phải có cửa này: cổng TỪ CHỐI mọi tài liệu và đợt khám của mã bệnh
/// nhân chưa ai nối hồ sơ (chốt 3). Nếu không có đường hỏi ngược thì bộ đếm tồn
/// bên HIS không bao giờ về 0, và người ở quầy sẽ học cách phớt lờ nó — đúng cái
/// bệnh chốt 5 đợt 0 muốn tránh. Có cửa này thì màn *Gửi cho bệnh nhân* tách
/// được hai số: CHỜ NGƯỜI NHẬN và GỬI ĐƯỢC NGAY, và số thứ hai về 0 được.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/ho-so")]
[KhoaCoSo]
public class HoSoApiController : ControllerBase
{
    /// <summary>Một lần hỏi tối đa bấy nhiêu mã — đủ cho một màn hàng đợi, không đủ để quét cả danh bạ.</summary>
    private const int ToiDaMoiLanHoi = 1000;

    private readonly IDotKhamService _dotKham;
    private readonly INhatKyApi _nhatKy;

    public HoSoApiController(IDotKhamService dotKham, INhatKyApi nhatKy)
    {
        _dotKham = dotKham;
        _nhatKy = nhatKy;
    }

    [HttpPost("kiem-tra-nhan")]
    public async Task<IActionResult> KiemTraNhan([FromBody] KiemTraNhanRequest yeuCau)
    {
        var coSo = HttpContext.CoSoDaXacThuc();
        var duong = HttpContext.Request.Path.Value ?? "";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (yeuCau?.MaBenhNhan == null || yeuCau.MaBenhNhan.Count == 0)
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, coSo.Id,
                lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<KiemTraNhanResponseData>.Fail(
                "Danh sách mã bệnh nhân (maBenhNhan) không được rỗng."));
        }

        if (yeuCau.MaBenhNhan.Count > ToiDaMoiLanHoi)
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, coSo.Id,
                lyDo: LyDoApi.DuLieuSai, soLuong: yeuCau.MaBenhNhan.Count, ipGoi: ip);
            return BadRequest(ApiResponse<KiemTraNhanResponseData>.Fail(
                $"Một lần hỏi tối đa {ToiDaMoiLanHoi} mã, lần này có {yeuCau.MaBenhNhan.Count}."));
        }

        // Chỉ trả về trong phạm vi cơ sở của khóa đang dùng — một cơ sở không
        // bao giờ hỏi được hồ sơ của cơ sở khác.
        var daCo = await _dotKham.LocMaDaCoNguoiNhanAsync(coSo.Id, yeuCau.MaBenhNhan);

        await _nhatKy.GhiAsync(duong, KetQuaApi.Nhan, coSo.Id,
            soLuong: yeuCau.MaBenhNhan.Count, ipGoi: ip);

        return Ok(ApiResponse<KiemTraNhanResponseData>.Ok(new KiemTraNhanResponseData
        {
            DaCoNguoiNhan = daCo,
            SoDaHoi = yeuCau.MaBenhNhan.Count
        }, $"{daCo.Count}/{yeuCau.MaBenhNhan.Count} mã đã có người nhận."));
    }
}
