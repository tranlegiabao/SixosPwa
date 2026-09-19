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
    /// <summary>Duong doc "Kho phieu co so" — FTP cua phong kham, CHI DOC (ADR 0030).</summary>
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
        var duong = HttpContext.Request.Path.Value ?? "";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrWhiteSpace(loaiTaiLieu))
        {
            await _nhatKy.GhiAsync(duong, KetQuaApi.TuChoi, cskcb.Id,
                lyDo: LyDoApi.ThieuHeader, ipGoi: ip);
            return BadRequest(ApiResponse<TiepNhanTaiLieuResponseData>.Fail("Thiếu Header loại tài liệu 'X-Loai-Tai-Lieu'."));
        }

        // 🔴 Danh muc DONG (V9). Truoc day man phan nhom bang != "DON_THUOC" nen
        // moi chuoi la deu roi vao nhom "ket qua kham" — HIS go sai mot ky tu la
        // don thuoc im lang nhay nhom. Chan ngay tu cua, va noi ro ma nao hop le
        // de ben HIS sua duoc ngay chu khong phai doan.
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

        // 4. Thực thi tiếp nhận và lưu trữ tài liệu
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
            // 🔴 Chot 3: khong luu gi, khong day tep. Ma may LyDoApi.ChuaCoNguoiNhan
            // nam trong errors de hang doi ben HIS phan biet duoc voi loi ky
            // thuat — dung doc cau tieng Viet de quyet dinh co thu lai hay khong.
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

        // Ba tang, cung luat man DanhSachTaiLieu dang dung. Tra ve 404 chu khong
        // 403: bao "co tai lieu nay nhung khong phai cua ban" la da lo su ton
        // tai cua no.
        //
        // 🔴 KHONG khop bang CCCD: CCCD go luc dang nhap khong duoc xac thuc,
        // OTP chi xac thuc so dien thoai (A6 dot 1). Va ho so phai qua *Cua tai
        // lieu* (chot 9 dot 1, ADR 0020).
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        // 🔴 Lay luon MaBN cua HO SO tu DM_BenhNhanCoSo: cot QL_TaiLieuBenhNhan.MaBN
        // da bi xoa (no chi la ban sao cua ho so, de lech). Cung mot cau, khong them
        // luot hoi CSDL nao.
        // 🔴 Chu so huu ho so noi bang DM_BenhNhan.IdTaiKhoan (ADR 0019), KHONG
        // bang so dien thoai. Ban cu chi so `p.SDT == dinhDanh`, ma SDT cua HO SO
        // thuong KHAC SDT cua TAI KHOAN — mot tai khoan giu nhieu ho so (me + con),
        // moi ho so mang so dien thoai rieng cua nguoi do. Hau qua do duoc that:
        // Cong tai lieu LIET KE duoc tai lieu, nhung bam vao lai 404 vi chot quyen
        // soi bang khoa khac voi cau liet ke.
        // Van giu nhanh so SDT/Email lam DU PHONG cho ho so cu chua noi IdTaiKhoan.
        var idTaiKhoan = await _db.TaiKhoans.AsNoTracking()
            .Where(t => t.SDT == dinhDanh || t.Email == dinhDanh)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();

        // Dot 1B: mot dong DA LA "con nguoi + ho so tai co so" (luat C2).
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
            // Hai kho, hai duong doc. Re theo COT NguonKho chu khong doan bang tien to
            // "sixospwa/" trong chuoi — chot 35, ADR 0030.
            var stream = taiLieu.NguonKho == NguonKhoTaiLieu.CoSo
                ? await _khoCoSo.TaiVeAsync(taiLieu.IdCoSo, taiLieu.DuongDanFtp)
                : await _taiLieuService.TaiStreamPdfAsync(taiLieu.DuongDanFtp);

            var safeFileName = $"{hoSo.MaBN}_{taiLieu.LoaiTaiLieu}_{taiLieu.Id}.pdf";

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{safeFileName}\"";
            // 🔴 KHONG dat [ResponseCache] kieu AnhController (Duration = 86400): anh logo
            // la cua cong cong, phieu benh nhan thi khong. Va chot 37 da chot khong dem o
            // bat ky dau — do that PDF 232KB chi mat 110–241 ms.
            Response.Headers["Cache-Control"] = "no-store";
            return File(stream, "application/pdf", enableRangeProcessing: true);
        }
        // 🔴 Chot 38: TACH hai ca ra. Gop lai chinh la loi im lang ma thuat ngu
        // *Chua hoi duoc co so* (CONTEXT.md) sinh ra de dep — benh nhan tuong tai lieu
        // cua minh bi mat, trong khi no van con nguyen ben phong kham.
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
