using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Models.Dto;
using SixosPwa.Security;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers.Api;

// 🔴 KHÔNG đặt [AllowAnonymous] ở mức LỚP. Đặt ở đây thì MỌI action đều thừa
// hưởng, kể cả đường đọc tệp — đó là cách xem/{id} từng mở cho cả thế giới.
// Từng action tự khai: cửa cho MÁY dùng [KhoaCoSo], cửa cho NGƯỜI dùng
// [Authorize].
[ApiController]
[Route("api/v1/tai-lieu")]
public class TaiLieuApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITaiLieuService _taiLieuService;
    /// <summary>Đường đọc "Kho phiếu cơ sở" — FTP của phòng khám, CHỈ ĐỌC (ADR 0030).</summary>
    private readonly IKhoCoSoService _khoCoSo;
    private readonly INhatKyApi _nhatKy;
    private readonly ILogger<TaiLieuApiController> _logger;

    public TaiLieuApiController(
        ApplicationDbContext db,
        ITaiLieuService taiLieuService,
        IKhoCoSoService khoCoSo,
        INhatKyApi nhatKy,
        ILogger<TaiLieuApiController> logger)
    {
        _db = db;
        _taiLieuService = taiLieuService;
        _khoCoSo = khoCoSo;
        _nhatKy = nhatKy;
        _logger = logger;
    }

    [HttpPost("tiep-nhan")]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<TiepNhanTaiLieuResponseData>), StatusCodes.Status404NotFound)]
    // 🔴 Xác thực chuyển sang [KhoaCoSo] (mục V7). Bắt buộc, không còn là tùy
    // chọn: cột DM_CSKCB.ApiKey đã bị bỏ khỏi CSDL, đoạn so chuỗi cũ không còn
    // gì để so. Ba khóa của 3 cơ sở đang test đã được chuyển sang
    // HT_KhoaApiCoSo dạng BĂM ở script 01, băm cùng cách => cơ sở GIỮ NGUYÊN
    // khóa cũ vẫn gọi được, không phải cấp lại.
    //
    // Attribute lo luôn: thiếu header, khóa sai, khóa tắt, khóa hết hạn, và
    // khóa đúng nhưng gõ nhầm mã cơ sở. Nó cũng ghi dòng nhật ký cho mọi lượt
    // bị chặn, nên ở đây chỉ còn ghi kết quả NGHIỆP VỤ — mỗi cuộc gọi đúng một
    // dòng HT_LogApiCoSo.
    //
    // KHÔNG kiểm DM_CSKCB.Active nữa: theo ADR 0013 cờ ấy chỉ quyết định hiển
    // thị công khai và nhận đăng nhập mới. Cơ sở tạm ẩn đi sửa nội dung mà bị
    // ngừng nhận kết quả xét nghiệm là lỗi im lặng.
    [AllowAnonymous]
    [KhoaCoSo]
    public async Task<IActionResult> TiepNhanTaiLieu(
        [FromHeader(Name = "X-Loai-Tai-Lieu")] string? loaiTaiLieu,
        [FromBody] TiepNhanTaiLieuRequest request)
    {
        var cskcb = HttpContext.CoSoDaXacThuc();
        var duong = HttpContext.Request.Path.Value ?? "";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(loaiTaiLieu))
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id,
                lyDo: LyDoApi.ThieuHeader, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Thiếu Header loại tài liệu 'X-Loai-Tai-Lieu'."));
        }

        // 🔴 Danh mục ĐÓNG (V9). Trước đây màn phân nhóm bằng != "DON_THUOC" nên
        // mọi chuỗi lạ đều rơi vào nhóm "kết quả khám" — HIS gõ sai một ký tự là
        // đơn thuốc im lặng nhảy nhóm. Chặn ngay từ cửa, và nói rõ mã nào hợp lệ
        // để bên HIS sửa được ngay chứ không phải đoán.
        if (!LoaiTaiLieu.HopLe(loaiTaiLieu))
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id,
                lyDo: LyDoApi.LoaiTaiLieuLa, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail(
                $"Loại tài liệu '{loaiTaiLieu}' không hợp lệ. Chỉ nhận: {LoaiTaiLieu.DanhSachChoNguoiDoc()}.",
                new List<string> { LyDoApi.LoaiTaiLieuLa }));
        }

        if (request == null)
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id,
                lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Dữ liệu Request Body không được để trống."));
        }

        try
        {
            var ketQua = await _taiLieuService.TiepNhanTaiLieuAsync(cskcb, loaiTaiLieu, request);
            await _nhatKy.GhiAsync(duong, KetQuaApi.Nhan, cskcb.Id,
                maBN: request.MaBenhNhan, maNguonHIS: request.MaNguonHIS, ipGoi: ip);
            // Nội dung y hệt bản đang có ⇒ vẫn là THÀNH CÔNG (tài liệu đã ở đúng
            // chỗ nó cần ở), nhưng nói rõ là không tạo bản mới — để người ở quầy
            // bên HIS đọc nhật ký thấy đúng sự thật.
            return Ok(ApiResponse<TiepNhanTaiLieuResponseData>.Ok(
                ketQua,
                ketQua.NoiDungKhongDoi
                    ? "Nội dung không đổi — giữ nguyên bản hiện có, không tạo phiên bản mới."
                    : "Tiếp nhận tài liệu thành công."));
        }
        catch (ChuaCoNguoiNhanException ex)
        {
            // 🔴 Chốt 3: không lưu gì, không đẩy tệp. Mã máy LyDoApi.ChuaCoNguoiNhan
            // nằm trong errors để hàng đợi bên HIS phân biệt được với lỗi kỹ
            // thuật — đừng đọc câu tiếng Việt để quyết định có thử lại hay không.
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id,
                maBN: ex.MaBN, maNguonHIS: request.MaNguonHIS,
                lyDo: LyDoApi.ChuaCoNguoiNhan, ipGoi: ip);

            return Conflict(ApiResponse<TiepNhanTaiLieuResponseData>.Fail(
                ex.Message + " Giữ lại ở hàng đợi và hỏi lại qua /api/v1/ho-so/kiem-tra-nhan.",
                new List<string> { LyDoApi.ChuaCoNguoiNhan }));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Tham so khong hop le khi tiep nhan tai lieu co so {MaCoSo}: {Message}", cskcb.MaCoSo, ex.Message);
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id,
                maBN: request.MaBenhNhan, lyDo: LyDoApi.DuLieuSai, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi xu ly khi tiep nhan tai lieu cho co so {MaCoSo}", cskcb.MaCoSo);
            await _nhatKy.GhiAsync(duong, KetQuaApi.Loi, cskcb.Id,
                maBN: request.MaBenhNhan, ipGoi: ip);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Đã xảy ra lỗi máy chủ nội bộ trong quá trình tiếp nhận tài liệu."));
        }
    }

    // 🔴 VÁ TẠM (đợt 2 giai đoạn 2). Trước đó action này thừa hưởng
    // [AllowAnonymous] của lớp, mà {id} lại là IDENTITY tuần tự => gõ 1,2,3...
    // là tải được bệnh án của bất kỳ ai.
    //
    // Đây CHỈ là miếng vá: [Authorize] chặn người lạ, đoạn kiểm dưới chặn bệnh
    // nhân này xem tài liệu của bệnh nhân kia. Cách đúng hẳn (V1) vẫn là CHUYỂN
    // đường đọc ra khỏi khu api/v1 sang HomeController với route
    // /benh-nhan/tai-lieu/xem/{id}, và thêm tầng thứ ba là *Cửa tài liệu*
    // (DM_BenhNhanCoSo.DaMoTaiLieu, chốt 9 đợt 1 + ADR 0020). Khu api/v1 chỉ
    // dành cho MÁY.
    [Authorize]
    [HttpGet("xem/{id:long}")]
    public async Task<IActionResult> XemTaiLieu(long id)
    {
        var taiLieu = await _taiLieuService.LayTaiLieuAsync(id);
        if (taiLieu == null)
        {
            return NotFound("Không tìm thấy tài liệu yêu cầu.");
        }

        // Ba tầng, cùng luật màn DanhSachTaiLieu đang dùng. Trả về 404 chứ không
        // 403: báo "có tài liệu này nhưng không phải của bạn" là đã lộ sự tồn
        // tại của nó.
        //
        // 🔴 KHÔNG khớp bằng CCCD: CCCD gõ lúc đăng nhập không được xác thực,
        // OTP chỉ xác thực số điện thoại (A6 đợt 1). Và hồ sơ phải qua *Cửa tài
        // liệu* (chốt 9 đợt 1, ADR 0020).
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        // 🔴 Đợt 1B: không còn tra HT_TaiKhoan ở đây. Quyền đọc tài liệu được
        // quyết bằng chính dòng DM_BenhNhan: đúng định danh phiên (SDT hoặc Email)
        // VÀ đúng cơ sở VÀ cửa tài liệu đã mở (ADR 0036). Biến idTaiKhoan cũ nằm
        // lại một vòng gọi DB chết và một khối chú thích nói sai về phép kiểm.

        // Đợt 1B: một dòng ĐÃ LÀ "con người + hồ sơ tại cơ sở" (luật C2).
        var hoSo = await (
            from h in _db.BenhNhans.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals (long?)cs.Id
            where (h.SDT == dinhDanh || h.Email == dinhDanh)
                  && h.DaMoTaiLieu
                  && cs.MaCoSo == maCoSo
                  && cs.Id == taiLieu.IdCoSo
                  && h.Id == taiLieu.IdBenhNhan
            select new { h.Id, h.MaBN }).FirstOrDefaultAsync();

        if (hoSo is null)
        {
            _logger.LogWarning("Chan doc tai lieu {Id} khong thuoc nguoi dang dang nhap", id);
            return NotFound("Không tìm thấy tài liệu yêu cầu.");
        }

        try
        {
            // Hai kho, hai đường đọc. Rẽ theo CỘT NguonKho chứ không đoán bằng tiền tố
            // "sixospwa/" trong chuỗi — chốt 35, ADR 0030.
            var stream = taiLieu.NguonKho == NguonKhoTaiLieu.CoSo
                ? await _khoCoSo.TaiVeAsync(taiLieu.IdCoSo, taiLieu.DuongDanFtp)
                : await _taiLieuService.TaiStreamPdfAsync(taiLieu.DuongDanFtp);

            var safeFileName = $"{hoSo.MaBN}_{taiLieu.LoaiTaiLieu}_{taiLieu.Id}.pdf";

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeFileName}\"";
            // 🔴 KHÔNG đặt [ResponseCache] kiểu AnhController (Duration = 86400): ảnh logo
            // là của công cộng, phiếu bệnh nhân thì không. Và chốt 37 đã chốt không đệm ở
            // bất kỳ đâu — đo thật PDF 232KB chỉ mất 110–241 ms.
            Response.Headers["Cache-Control"] = "no-store";
            return File(stream, "application/pdf", enableRangeProcessing: true);
        }
        // 🔴 Chốt 38: TÁCH hai ca ra. Gộp lại chính là lỗi im lặng mà thuật ngữ
        // *Chưa nối được cơ sở* (CONTEXT.md) sinh ra để dẹp — bệnh nhân tưởng tài liệu
        // của mình bị mất, trong khi nó vẫn còn nguyên bên phòng khám.
        catch (KhoCoSoKhongCoTepException ex)
        {
            _logger.LogWarning(ex, "Kho co so khong co tep cho tai lieu {Id}: {Path}", id, taiLieu.DuongDanFtp);
            return NotFound("Không tìm thấy tệp tài liệu trên kho của cơ sở.");
        }
        catch (KhoCoSoKhongNoiDuocException ex)
        {
            _logger.LogError(ex, "Khong noi duoc kho co so cho tai lieu {Id}: {Path}", id, taiLieu.DuongDanFtp);
            return StatusCode(StatusCodes.Status502BadGateway,
                "Chưa lấy được tài liệu từ phòng khám — tài liệu vẫn còn nguyên.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải tài liệu {Id} từ FTP: {Path}", id, taiLieu.DuongDanFtp);
            return NotFound("Không thể tải tệp tài liệu từ kho lưu trữ.");
        }
    }
}
