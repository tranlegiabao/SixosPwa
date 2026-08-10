using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SixosPwa.Models;
using SixosPwa.Services;
using SixosPwa.Hubs;
using System.Text;

namespace SixosPwa.Controllers;

/// <summary>
/// Controller quản lý gửi SMS cho bệnh nhân (Admin/Đối tác)
/// </summary>
[Authorize(Roles = "Admin,DoiTac")]
public class SmsController : Controller
{
    private readonly ISmsService _smsService;
    private readonly IBenhNhanService _benhNhanService;
    private readonly ILichSuTinNhanService _lichSuService;
    private readonly IMauTinNhanService _mauTinNhanService;
    private readonly ILogger<SmsController> _logger;

    public SmsController(
        ISmsService smsService,
        IBenhNhanService benhNhanService,
        ILichSuTinNhanService lichSuService,
        IMauTinNhanService mauTinNhanService,
        ILogger<SmsController> logger)
    {
        _smsService = smsService;
        _benhNhanService = benhNhanService;
        _lichSuService = lichSuService;
        _mauTinNhanService = mauTinNhanService;
        _logger = logger;
    }

    /// <summary>
    /// Trang gửi SMS nâng cao (cho admin/nhân viên y tế)
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var danhSachBenhNhan = await _benhNhanService.LayDanhSachBenhNhanAsync();
        var danhSachMau = await _mauTinNhanService.LayDanhSachMauAsync();
        
        ViewBag.DanhSachBenhNhan = danhSachBenhNhan;
        ViewBag.DanhSachMau = danhSachMau;
        
        return View();
    }

    /// <summary>
    /// Trang lịch sử gửi SMS
    /// </summary>
    public async Task<IActionResult> LichSu(DateTime? tuNgay, DateTime? denNgay)
    {
        var lichSu = await _lichSuService.LayTatCaLichSuAsync(tuNgay, denNgay);
        return View(lichSu);
    }

    /// <summary>
    /// Trang quản lý mẫu tin nhắn
    /// </summary>
    public async Task<IActionResult> MauTinNhan()
    {
        var danhSachMau = await _mauTinNhanService.LayDanhSachMauAsync();
        return View(danhSachMau);
    }

    /// <summary>
    /// Trang quản lý bệnh nhân
    /// </summary>
    public async Task<IActionResult> QuanLyBenhNhan()
    {
        var danhSach = await _benhNhanService.LayDanhSachBenhNhanAsync();
        return View(danhSach);
    }

    /// <summary>
    /// API: Import danh sách bệnh nhân từ CSV
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImportBenhNhan(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { thanhCong = false, thongBao = "Vui lòng chọn file" });
            }

            var danhSach = new List<ImportBenhNhanDto>();

            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                // Bỏ qua header
                var header = await reader.ReadLineAsync();
                
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',');
                    if (values.Length < 3) continue;

                    danhSach.Add(new ImportBenhNhanDto
                    {
                        MaBenhNhan = values[0].Trim(),
                        HoTen = values[1].Trim(),
                        SoDienThoai = values[2].Trim(),
                        NgaySinh = values.Length > 3 ? values[3].Trim() : null,
                        GioiTinh = values.Length > 4 ? values[4].Trim() : null,
                        DiaChi = values.Length > 5 ? values[5].Trim() : null,
                        Email = values.Length > 6 ? values[6].Trim() : null
                    });
                }
            }

            var soLuong = await _benhNhanService.ImportDanhSachBenhNhanAsync(danhSach);

            return Ok(new { thanhCong = true, thongBao = $"Đã import {soLuong} bệnh nhân", soLuong });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi import bệnh nhân");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi: " + ex.Message });
        }
    }

    /// <summary>
    /// API: Gửi SMS hàng loạt cho nhiều bệnh nhân
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuiSmsHangLoat([FromBody] GuiSmsHangLoatRequest request, [FromServices] IHubContext<ThongBaoHub> hubContext)
    {
        try
        {
            int thanhCong = 0;
            int thatBai = 0;

            foreach (var benhNhanId in request.DanhSachBenhNhanId)
            {
                var benhNhan = await _benhNhanService.LayBenhNhanTheoIdAsync(benhNhanId);
                if (benhNhan == null || !benhNhan.ChoPhepNhanTinNhan)
                {
                    thatBai++;
                    continue;
                }

                // Thay thế placeholder trong nội dung
                var noiDung = request.NoiDung
                    .Replace("{TenBenhNhan}", benhNhan.HoTen)
                    .Replace("{MaBenhNhan}", benhNhan.MaBenhNhan);

                var ketQua = await _smsService.GuiSmsAsync(benhNhan.SoDienThoai, noiDung);

                // Lưu lịch sử
                await _lichSuService.LuuLichSuAsync(new LichSuTinNhan
                {
                    BenhNhanId = benhNhanId,
                    DoiTac = request.DoiTac,
                    NoiDung = noiDung,
                    LoaiTinNhan = request.LoaiTinNhan,
                    TrangThai = ketQua ? "DaGui" : "ThatBai"
                });

                if (ketQua)
                {
                    thanhCong++;
                    
                    // GỬI THÔNG BÁO REALTIME CHO BỆNH NHÂN
                    try
                    {
                        await hubContext.Clients.Group($"BenhNhan_{benhNhanId}")
                            .SendAsync("NhanTinNhanMoi", new
                            {
                                tieuDe = "Tin nhắn mới",
                                noiDung = noiDung,
                                doiTac = request.DoiTac,
                                thoiGian = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                            });
                        
                        _logger.LogInformation("Đã gửi thông báo realtime cho bệnh nhân {BenhNhanId}", benhNhanId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi khi gửi thông báo realtime cho bệnh nhân {BenhNhanId}", benhNhanId);
                    }
                }
                else
                {
                    thatBai++;
                }
            }

            return Ok(new { 
                thanhCong = true, 
                thongBao = $"Đã gửi: {thanhCong} thành công, {thatBai} thất bại",
                soLuongThanhCong = thanhCong,
                soLuongThatBai = thatBai
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi SMS hàng loạt");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }

    /// <summary>
    /// API gửi nhắc lịch khám
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuiNhacLichKham([FromBody] NhacLichKhamRequest request)
    {
        try
        {
            var ketQua = await _smsService.GuiNhacLichKhamAsync(
                request.SoDienThoai,
                request.TenBenhNhan,
                request.ThoiGianKham,
                request.TenBacSi,
                request.PhongKham
            );

            if (ketQua)
            {
                return Ok(new { thanhCong = true, thongBao = "Đã gửi SMS thành công" });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể gửi SMS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi nhắc lịch khám");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }

    /// <summary>
    /// API gửi xác nhận đặt lịch
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuiXacNhanDatLich([FromBody] XacNhanDatLichRequest request)
    {
        try
        {
            var ketQua = await _smsService.GuiXacNhanDatLichAsync(
                request.SoDienThoai,
                request.TenBenhNhan,
                request.ThoiGianKham,
                request.MaDatLich
            );

            if (ketQua)
            {
                return Ok(new { thanhCong = true, thongBao = "Đã gửi xác nhận" });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể gửi SMS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi xác nhận đặt lịch");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }

    /// <summary>
    /// API gửi kết quả xét nghiệm
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuiKetQuaXetNghiem([FromBody] KetQuaXetNghiemRequest request)
    {
        try
        {
            var ketQua = await _smsService.GuiKetQuaXetNghiemAsync(
                request.SoDienThoai,
                request.TenBenhNhan,
                request.LoaiXetNghiem,
                request.LinkXemKetQua
            );

            if (ketQua)
            {
                return Ok(new { thanhCong = true, thongBao = "Đã gửi kết quả" });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể gửi SMS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi kết quả xét nghiệm");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }

    /// <summary>
    /// API gửi nhắc uống thuốc
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuiNhacUongThuoc([FromBody] NhacUongThuocRequest request)
    {
        try
        {
            var ketQua = await _smsService.GuiNhacUongThuocAsync(
                request.SoDienThoai,
                request.TenBenhNhan,
                request.TenThuoc,
                request.LieuDung
            );

            if (ketQua)
            {
                return Ok(new { thanhCong = true, thongBao = "Đã gửi nhắc nhở" });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể gửi SMS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi nhắc uống thuốc");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }

    /// <summary>
    /// API gửi SMS tùy chỉnh
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GuiSmsTuyChon([FromBody] SmsTuyChonRequest request)
    {
        try
        {
            var ketQua = await _smsService.GuiSmsAsync(request.SoDienThoai, request.NoiDung);

            if (ketQua)
            {
                return Ok(new { thanhCong = true, thongBao = "Đã gửi SMS" });
            }

            return BadRequest(new { thanhCong = false, thongBao = "Không thể gửi SMS" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi SMS tùy chọn");
            return StatusCode(500, new { thanhCong = false, thongBao = "Lỗi hệ thống" });
        }
    }
}

public record NhacLichKhamRequest(string SoDienThoai, string TenBenhNhan, DateTime ThoiGianKham, string TenBacSi, string PhongKham);
public record XacNhanDatLichRequest(string SoDienThoai, string TenBenhNhan, DateTime ThoiGianKham, string MaDatLich);
public record KetQuaXetNghiemRequest(string SoDienThoai, string TenBenhNhan, string LoaiXetNghiem, string LinkXemKetQua);
public record NhacUongThuocRequest(string SoDienThoai, string TenBenhNhan, string TenThuoc, string LieuDung);
public record SmsTuyChonRequest(string SoDienThoai, string NoiDung);
public record GuiSmsHangLoatRequest(List<int> DanhSachBenhNhanId, string NoiDung, string LoaiTinNhan, string? DoiTac);
