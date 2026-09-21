using Microsoft.AspNetCore.Mvc;
using SixosPwa.Models.Dto;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Controllers.Api;

/// <summary>
/// Cua nhan LICH SU KHAM tu HIS cua co so. Chi may goi — xac thuc bang khoa co
/// so, khong co cookie nao o day.
///
/// <para>
/// Day theo LO, mot cuoc goi = mot benh nhan nhieu dot. Luc benh nhan vua noi
/// ho so, HIS keo tron qua khu ve trong mot cuoc goi thay vi vai chuc cuoc.
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
                // 🔴 Chot 3: chua ai noi ho so thi TU CHOI ca lo, cong khong luu
                // gi. Ma may LyDoApi.ChuaCoNguoiNhan nam trong errors de hang doi
                // ben HIS phan biet duoc voi loi ky thuat — dung doc message
                // tieng Viet de quyet dinh.
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
