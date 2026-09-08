using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models.Dto;
using SixosPwa.Security;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers.Api;

// 🔴 KHONG dat [AllowAnonymous] o muc LOP. Dat o day thi MOI action deu thua
// huong, ke ca duong doc tep — do la cach xem/{id} tung mo cho ca the gioi.
// Tung action tu khai: cua cho MAY dung [KhoaCoSo], cua cho NGUOI dung
// [Authorize].
[ApiController]
[Route("api/v1/tai-lieu")]
public class TaiLieuApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITaiLieuService _taiLieuService;
    private readonly INhatKyApi _nhatKy;
    private readonly ILogger<TaiLieuApiController> _logger;

    public TaiLieuApiController(
        ApplicationDbContext db,
        ITaiLieuService taiLieuService,
        INhatKyApi nhatKy,
        ILogger<TaiLieuApiController> logger)
    {
        _db = db;
        _taiLieuService = taiLieuService;
        _nhatKy = nhatKy;
        _logger = logger;
    }

    /// <summary>
    /// API tiếp nhận tài liệu y tế (PDF mã hóa Base64) từ các hệ thống HIS của cơ sở y tế.
    /// </summary>
    /// <remarks>
    /// Xác thực bằng <see cref="KhoaCoSoAttribute"/>: hai header <c>X-API-Key</c>
    /// và <c>X-Ma-CSKCB</c>. Cơ sở đã xác thực đọc ra từ <c>HttpContext</c>.
    /// </remarks>
    /// <param name="loaiTaiLieu">Loại tài liệu (Header: X-Loai-Tai-Lieu)</param>
    /// <param name="request">Body JSON chứa mã bệnh nhân, file PDF base64 và thông tin mở rộng</param>
    [HttpPost("tiep-nhan")]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status404NotFound)]
    // 🔴 Xac thuc chuyen sang [KhoaCoSo] (muc V7). Bat buoc, khong con la tuy
    // chon: cot DM_CSKCB.ApiKey da bi bo khoi CSDL, doan so chuoi cu khong con
    // gi de so. Ba khoa cua 3 co so dang test da duoc chuyen sang
    // HT_KhoaApiCoSo dang BAM o script 01, bam cung cach => co so GIU NGUYEN
    // khoa cu van goi duoc, khong phai cap lai.
    //
    // Attribute lo luon: thieu header, khoa sai, khoa tat, khoa het han, va
    // khoa dung nhung go nham ma co so. No cung ghi dong nhat ky cho moi luot
    // bi chan, nen o day chi con ghi ket qua NGHIEP VU — moi cuoc goi dung mot
    // dong HT_LogApiCoSo.
    //
    // KHONG kiem DM_CSKCB.Active nua: theo ADR 0013 co ay chi quyet dinh hien
    // thi cong khai va nhan dang nhap moi. Co so tam an di sua noi dung ma bi
    // ngung nhan ket qua xet nghiem la loi im lang.
    [AllowAnonymous]
    [KhoaCoSo]
    public async Task<IActionResult> TiepNhanTaiLieu(
        [FromHeader(Name = "X-Loai-Tai-Lieu")] string? loaiTaiLieu,
        [FromBody] TiepNhanTaiLieuRequest request)
    {
        var cskcb = HttpContext.CoSoDaXacThuc();
        var idKhoa = HttpContext.IdKhoaDaXacThuc();
        var duong = HttpContext.Request.Path.Value ?? "";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(loaiTaiLieu))
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id, idKhoa,
                lyDo: LyDoApi.ThieuHeader, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Thiếu Header loại tài liệu 'X-Loai-Tai-Lieu'."));
        }

        if (request == null)
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id, idKhoa,
                lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Dữ liệu Request Body không được để trống."));
        }

        // 4. Thực thi tiếp nhận và lưu trữ tài liệu
        try
        {
            var ketQua = await _taiLieuService.TiepNhanTaiLieuAsync(cskcb, loaiTaiLieu, request);
            await _nhatKy.GhiAsync(duong, KetQuaApi.Nhan, cskcb.Id, idKhoa,
                maBN: request.MaBenhNhan, ipGoi: ip);
            return Ok(ApiResponse<TiepNhanTaiLieuResponseData>.Ok(ketQua, "Tiếp nhận tài liệu thành công."));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Tham so khong hop le khi tiep nhan tai lieu co so {MaCoSo}: {Message}", cskcb.MaCoSo, ex.Message);
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id, idKhoa,
                maBN: request.MaBenhNhan, lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi xu ly khi tiep nhan tai lieu cho co so {MaCoSo}", cskcb.MaCoSo);
            await _nhatKy.GhiAsync(duong, KetQuaApi.Loi, cskcb.Id, idKhoa,
                maBN: request.MaBenhNhan, ipGoi: ip);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Đã xảy ra lỗi máy chủ nội bộ trong quá trình tiếp nhận tài liệu."));
        }
    }

    /// <summary>
    /// Endpoint xem / tải tệp PDF an toàn từ kho lưu trữ FTP qua Proxy.
    /// </summary>
    /// <param name="id">ID tài liệu</param>
    // 🔴 VA TAM (dot 2 giai doan 2). Truoc do action nay thua huong
    // [AllowAnonymous] cua lop, ma {id} lai la IDENTITY tuan tu => go 1,2,3...
    // la tai duoc benh an cua bat ky ai.
    //
    // Day CHI la mieng va: [Authorize] chan nguoi la, doan kiem duoi chan benh
    // nhan nay xem tai lieu cua benh nhan kia. Cach dung han (V1) van la CHUYEN
    // duong doc ra khoi khu api/v1 sang HomeController voi route
    // /benh-nhan/tai-lieu/xem/{id}, va them tang thu ba la *Cua tai lieu*
    // (DM_BenhNhanCoSo.DaMoTaiLieu, chot 9 dot 1 + ADR 0020). Khu api/v1 chi
    // danh cho MAY.
    [Authorize]
    [HttpGet("xem/{id:long}")]
    public async Task<IActionResult> XemTaiLieu(long id)
    {
        var taiLieu = await _taiLieuService.LayTaiLieuAsync(id);
        if (taiLieu == null)
        {
            return NotFound("Không tìm thấy tài liệu yêu cầu.");
        }

        // Cung luat khop ho so ma man DanhSachTaiLieu dang dung. Tra ve 404 chu
        // khong 403: bao "co tai lieu nay nhung khong phai cua ban" la da lo ton
        // tai cua no.
        var cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        var laCuaNguoiDangXem = await (
            from p in _db.BenhNhans.AsNoTracking()
            join h in _db.BenhNhanCoSos.AsNoTracking() on p.Id equals h.IdBenhNhan
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals cs.Id
            where (p.SDT == dinhDanh || p.Email == dinhDanh || p.CCCD == cccd)
                  && cs.MaCoSo == maCoSo
                  && cs.Id == taiLieu.IdCoSo
                  && (h.Id == taiLieu.IdBenhNhanCoSo || h.MaBN == taiLieu.MaBN)
            select h.Id).AnyAsync();

        if (!laCuaNguoiDangXem)
        {
            _logger.LogWarning("Chan doc tai lieu {Id} khong thuoc nguoi dang dang nhap", id);
            return NotFound("Không tìm thấy tài liệu yêu cầu.");
        }

        try
        {
            var stream = await _taiLieuService.TaiStreamPdfAsync(taiLieu.DuongDanFtp);
            var safeFileName = $"{taiLieu.MaBN}_{taiLieu.LoaiTaiLieu}_{taiLieu.Id}.pdf";
            
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeFileName}\"";
            return File(stream, "application/pdf", enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải tài liệu {Id} từ FTP: {Path}", id, taiLieu.DuongDanFtp);
            return NotFound("Không thể tải tệp tài liệu từ kho lưu trữ.");
        }
    }
}
