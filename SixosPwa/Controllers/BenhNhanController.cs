using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SixosPwa.Models;
using SixosPwa.Services;
using System.Text;

namespace SixosPwa.Controllers;

/// <summary>
/// Controller cho chức năng dành cho bệnh nhân
/// </summary>
[Authorize(Roles = "BenhNhan")]
public class BenhNhanController : Controller
{
    private readonly IBenhNhanService _benhNhanService;
    private readonly ILichSuTinNhanService _lichSuService;
    private readonly IHoaDonService _hoaDonService;
    private readonly ILogger<BenhNhanController> _logger;

    public BenhNhanController(
        IBenhNhanService benhNhanService,
        ILichSuTinNhanService lichSuService,
        IHoaDonService hoaDonService,
        ILogger<BenhNhanController> logger)
    {
        _benhNhanService = benhNhanService;
        _lichSuService = lichSuService;
        _hoaDonService = hoaDonService;
        _logger = logger;
    }

    /// <summary>
    /// Trang chủ bệnh nhân (Dashboard)
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Lấy BenhNhanId từ claims
        var benhNhanIdClaim = User.FindFirst("BenhNhanId")?.Value;
        
        if (string.IsNullOrEmpty(benhNhanIdClaim) || !int.TryParse(benhNhanIdClaim, out int benhNhanId))
        {
            _logger.LogWarning("Không tìm thấy BenhNhanId trong claims");
            return RedirectToAction("Login", "DangNhap");
        }

        var benhNhan = await _benhNhanService.LayBenhNhanTheoIdAsync(benhNhanId);
        if (benhNhan == null)
        {
            _logger.LogWarning("Không tìm thấy bệnh nhân với Id {BenhNhanId}", benhNhanId);
            return RedirectToAction("Login", "DangNhap");
        }

        // Lấy lịch sử tin nhắn
        var lichSu = await _lichSuService.LayLichSuTheoBenhNhanAsync(benhNhan.Id);
        
        // Lấy hóa đơn
        var hoaDons = await _hoaDonService.LayDanhSachHoaDonTheoBenhNhanAsync(benhNhan.Id);

        ViewBag.LichSuTinNhan = lichSu;
        ViewBag.HoaDons = hoaDons;

        return View(benhNhan);
    }

    /// <summary>
    /// Xem lịch sử tin nhắn
    /// </summary>
    public async Task<IActionResult> LichSuTinNhan(int? benhNhanId)
    {
        // Demo: Dùng ID từ parameter hoặc mặc định = 1
        var id = benhNhanId ?? 1;
        var benhNhan = await _benhNhanService.LayBenhNhanTheoIdAsync(id);
        if (benhNhan == null)
        {
            return NotFound();
        }

        var lichSu = await _lichSuService.LayLichSuTheoBenhNhanAsync(id);
        
        ViewBag.BenhNhan = benhNhan;
        return View(lichSu);
    }

    /// <summary>
    /// Xem lịch sử hóa đơn
    /// </summary>
    public async Task<IActionResult> LichSuHoaDon(int? benhNhanId)
    {
        var id = benhNhanId ?? 1;
        var benhNhan = await _benhNhanService.LayBenhNhanTheoIdAsync(id);
        if (benhNhan == null)
        {
            return NotFound();
        }

        var hoaDons = await _hoaDonService.LayDanhSachHoaDonTheoBenhNhanAsync(id);
        
        ViewBag.BenhNhan = benhNhan;
        return View(hoaDons);
    }

    /// <summary>
    /// Cài đặt nhận tin nhắn
    /// </summary>
    public async Task<IActionResult> CaiDat(int? benhNhanId)
    {
        var id = benhNhanId ?? 1;
        var benhNhan = await _benhNhanService.LayBenhNhanTheoIdAsync(id);
        if (benhNhan == null)
        {
            return NotFound();
        }

        return View(benhNhan);
    }

    /// <summary>
    /// API: Bật/tắt nhận tin nhắn
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CapNhatTrangThaiNhanTinNhan([FromBody] CapNhatTrangThaiRequest request)
    {
        try
        {
            var ketQua = await _benhNhanService.CapNhatTrangThaiNhanTinNhanAsync(request.BenhNhanId, request.ChoPhep);
            
            if (ketQua)
            {
                var thongBao = request.ChoPhep ? "Đã bật nhận tin nhắn" : "Đã tắt nhận tin nhắn";
                return Ok(new { thanhCong = true, thongBao });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể cập nhật" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật trạng thái nhận tin nhắn");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }

    /// <summary>
    /// API: Trả lời tin nhắn
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TraLoiTinNhan([FromBody] TraLoiTinNhanRequest request)
    {
        try
        {
            var benhNhan = await _benhNhanService.LayBenhNhanTheoIdAsync(request.BenhNhanId);
            if (benhNhan == null)
            {
                return BadRequest(new { thanhCong = false, thongBao = "Không tìm thấy bệnh nhân" });
            }

            // Lưu tin nhắn trả lời vào lịch sử
            var tinNhanTraLoi = new LichSuTinNhan
            {
                BenhNhanId = request.BenhNhanId,
                DoiTac = benhNhan.HoTen,
                NoiDung = request.NoiDung,
                LoaiTinNhan = "Trả lời",
                TrangThai = "DaGui",
                ThoiGianGui = DateTime.Now
            };

            var ketQua = await _lichSuService.LuuLichSuAsync(tinNhanTraLoi);

            if (ketQua)
            {
                _logger.LogInformation("Bệnh nhân {BenhNhanId} đã trả lời tin nhắn {TinNhanGocId}", request.BenhNhanId, request.TinNhanGocId);
                return Ok(new { thanhCong = true, thongBao = "Đã gửi trả lời", tinNhanId = tinNhanTraLoi.Id });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể gửi trả lời" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi trả lời tin nhắn");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }
}

public record CapNhatTrangThaiRequest(int BenhNhanId, bool ChoPhep);
public record TraLoiTinNhanRequest(int TinNhanGocId, int BenhNhanId, string NoiDung);
