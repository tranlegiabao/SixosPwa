using Microsoft.AspNetCore.Mvc;
using SixosPwa.Models.Dto;
using SixosPwa.Security;
using SixosPwa.Services;

namespace SixosPwa.Controllers.Api;

/// <summary>
/// Duong DOC cho HIS: hoi theo lo xem ma benh nhan nao da co nguoi nhan ben cong.
///
/// <para>
/// Vi sao phai co cua nay: cong TU CHOI moi tai lieu va dot kham cua ma benh
/// nhan chua ai noi ho so (chot 3). Neu khong co duong hoi nguoc thi bo dem ton
/// ben HIS khong bao gio ve 0, va nguoi o quay se hoc cach phot lo no — dung cai
/// benh chot 5 dot 0 muon tranh. Co cua nay thi man *Gui cho benh nhan* tach
/// duoc hai so: CHO NGUOI NHAN va GUI DUOC NGAY, va so thu hai ve 0 duoc.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/ho-so")]
[KhoaCoSo]
public class HoSoApiController : ControllerBase
{
    /// <summary>Mot lan hoi toi da bay nhieu ma — du cho mot man hang doi, khong du de quet ca danh ba.</summary>
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

        // Chi tra ve trong pham vi co so cua khoa dang dung — mot co so khong
        // bao gio hoi duoc ho so cua co so khac.
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
