using Microsoft.AspNetCore.Mvc;
using SixosPwa.Models.Dto;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Controllers.Api;

/// <summary>
/// Cửa nhận LỊCH SỬ KHÁM từ HIS của cơ sở. Chỉ máy gọi — xác thực bằng khóa cơ
/// sở, không có cookie nào ở đây.
///
/// <para>
/// Đẩy theo LÔ, một cuộc gọi = một bệnh nhân nhiều đợt. Lúc bệnh nhân vừa nối
/// hồ sơ, HIS kéo trọn quá khứ về trong một cuộc gọi thay vì vài chục cuộc.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/dot-kham")]
[KhoaCoSo]
public class DotKhamApiController : ControllerBase
{
    private readonly IDotKhamService _dotKham;
    private readonly INhatKyApi _nhatKy;
    private readonly ILogger<DotKhamApiController> _logger;

    public DotKhamApiController(
        IDotKhamService dotKham,
        INhatKyApi nhatKy,
        ILogger<DotKhamApiController> logger)
    {
        _dotKham = dotKham;
        _nhatKy = nhatKy;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> NhanLo([FromBody] NhanDotKhamRequest yeuCau)
    {
        var coSo = HttpContext.CoSoDaXacThuc();
        var duong = HttpContext.Request.Path.Value ?? "";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (yeuCau == null)
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, coSo.Id,
                lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<NhanDotKhamResponseData>.Fail("Thân yêu cầu không được để trống."));
        }

        try
        {
            var (coHoSo, duLieu) = await _dotKham.NhanLoAsync(coSo, yeuCau);

            if (!coHoSo)
            {
                // 🔴 Chốt 3: chưa ai nối hồ sơ thì TỪ CHỐI cả lô, cổng không lưu
                // gì. Mã máy LyDoApi.ChuaCoNguoiNhan nằm trong errors để hàng đợi
                // bên HIS phân biệt được với lỗi kỹ thuật — đừng đọc message
                // tiếng Việt để quyết định.
                await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, coSo.Id,
                    maBN: duLieu.MaBenhNhan, lyDo: LyDoApi.ChuaCoNguoiNhan,
                    soLuong: yeuCau.DotKham?.Count, ipGoi: ip);

                return Conflict(ApiResponse<NhanDotKhamResponseData>.Fail(
                    "Mã bệnh nhân này chưa có hồ sơ nào nhận tại cổng. Giữ lại ở hàng đợi và hỏi lại qua /api/v1/ho-so/kiem-tra-nhan.",
                    new List<string> { LyDoApi.ChuaCoNguoiNhan }));
            }

            await _nhatKy.GhiAsync(duong, KetQuaApi.Nhan, coSo.Id,
                maBN: duLieu.MaBenhNhan, soLuong: duLieu.SoDaNhan, ipGoi: ip);

            return Ok(ApiResponse<NhanDotKhamResponseData>.Ok(duLieu,
                $"Đã nhận {duLieu.SoDaNhan} đợt khám, bỏ qua {duLieu.SoBoQua}."));
        }
        catch (ArgumentException ex)
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, coSo.Id,
                maBN: yeuCau.MaBenhNhan, lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<NhanDotKhamResponseData>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi nhan lo dot kham cua co so {MaCoSo}", coSo.MaCoSo);
            await _nhatKy.GhiAsync(duong, KetQuaApi.Loi, coSo.Id,
                maBN: yeuCau.MaBenhNhan, ipGoi: ip);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<NhanDotKhamResponseData>.Fail("Lỗi máy chủ khi tiếp nhận lô đợt khám."));
        }
    }
}
