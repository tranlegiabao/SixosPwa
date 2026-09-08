using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models.Dto;
using SixosPwa.Services;

namespace SixosPwa.Controllers.Api;

[ApiController]
[Route("api/v1/tai-lieu")]
[AllowAnonymous]
public class TaiLieuApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITaiLieuService _taiLieuService;
    private readonly ILogger<TaiLieuApiController> _logger;

    public TaiLieuApiController(
        ApplicationDbContext db,
        ITaiLieuService taiLieuService,
        ILogger<TaiLieuApiController> logger)
    {
        _db = db;
        _taiLieuService = taiLieuService;
        _logger = logger;
    }

    /// <summary>
    /// API tiếp nhận tài liệu y tế (PDF mã hóa Base64) từ các hệ thống HIS của cơ sở y tế.
    /// </summary>
    /// <param name="apiKey">Khóa bảo mật của CSKCB (Header: X-API-Key)</param>
    /// <param name="maCskcb">Mã cơ sở khám chữa bệnh (Header: X-Ma-CSKCB)</param>
    /// <param name="loaiTaiLieu">Loại tài liệu (Header: X-Loai-Tai-Lieu)</param>
    /// <param name="request">Body JSON chứa mã bệnh nhân, file PDF base64 và thông tin mở rộng</param>
    [HttpPost("tiep-nhan")]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TiepNhanTaiLieu(
        [FromHeader(Name = "X-API-Key")] string? apiKey,
        [FromHeader(Name = "X-Ma-CSKCB")] string? maCskcb,
        [FromHeader(Name = "X-Loai-Tai-Lieu")] string? loaiTaiLieu,
        [FromBody] TiepNhanTaiLieuRequest request)
    {
        // 1. Kiểm tra các header bắt buộc
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Thiếu Header xác thực 'X-API-Key'."));
        }

        if (string.IsNullOrWhiteSpace(maCskcb))
        {
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Thiếu Header mã cơ sở khám chữa bệnh 'X-Ma-CSKCB'."));
        }

        if (string.IsNullOrWhiteSpace(loaiTaiLieu))
        {
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Thiếu Header loại tài liệu 'X-Loai-Tai-Lieu'."));
        }

        if (request == null)
        {
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Dữ liệu Request Body không được để trống."));
        }

        // 2. Tra cứu cơ sở y tế
        var cskcb = await _db.DMCSKCBs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.MaCoSo == maCskcb.Trim());

        if (cskcb == null)
        {
            _logger.LogWarning("Không tìm thấy cơ sở y tế với mã: {MaCoSo}", maCskcb);
            return NotFound(ApiResponse<TiepNhanTaiLieuResponseData>.Fail($"Không tìm thấy cơ sở y tế với mã '{maCskcb}'."));
        }

        if (!cskcb.Active)
        {
            _logger.LogWarning("Cơ sở y tế {MaCoSo} đang bị vô hiệu hóa", maCskcb);
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Cơ sở khám chữa bệnh này đang tạm dừng hoạt động hoặc chưa được kích hoạt."));
        }

        // 3. Xác thực API Key
        if (string.IsNullOrWhiteSpace(cskcb.ApiKey) || !string.Equals(cskcb.ApiKey, apiKey.Trim(), StringComparison.Ordinal))
        {
            _logger.LogWarning("Xác thực API Key thất bại cho cơ sở: {MaCoSo}", maCskcb);
            return Unauthorized(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Xác thực thất bại: Khóa 'X-API-Key' không chính xác hoặc cơ sở chưa được cấp khóa API."));
        }

        // 4. Thực thi tiếp nhận và lưu trữ tài liệu
        try
        {
            var ketQua = await _taiLieuService.TiepNhanTaiLieuAsync(cskcb, loaiTaiLieu, request);
            return Ok(ApiResponse<TiepNhanTaiLieuResponseData>.Ok(ketQua, "Tiếp nhận tài liệu thành công."));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Tham số không hợp lệ khi tiếp nhận tài liệu cơ sở {MaCoSo}: {Message}", maCskcb, ex.Message);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xử lý khi tiếp nhận tài liệu cho cơ sở {MaCoSo}", maCskcb);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Đã xảy ra lỗi máy chủ nội bộ trong quá trình tiếp nhận tài liệu."));
        }
    }

    /// <summary>
    /// Endpoint xem / tải tệp PDF an toàn từ kho lưu trữ FTP qua Proxy.
    /// </summary>
    /// <param name="id">ID tài liệu</param>
    [HttpGet("xem/{id:long}")]
    public async Task<IActionResult> XemTaiLieu(long id)
    {
        var taiLieu = await _taiLieuService.LayTaiLieuAsync(id);
        if (taiLieu == null)
        {
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
