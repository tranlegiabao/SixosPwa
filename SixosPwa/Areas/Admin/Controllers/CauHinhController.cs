using Microsoft.AspNetCore.Mvc;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class CauHinhController : AdminControllerBase
{
    private readonly IHTConfigService _configService;
    private readonly ILogger<CauHinhController> _logger;

    public CauHinhController(
        IHTConfigService configService,
        ILogger<CauHinhController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? nhom)
    {
        var items = await _configService.LayDanhSachAsync(q, nhom);
        var danhSachNhom = await _configService.LayDanhSachNhomAsync();

        return View(new CauHinhListViewModel
        {
            Items = items,
            DanhSachNhom = danhSachNhom,
            Query = q,
            Nhom = nhom
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(long id, bool hieuLuc)
    {
        try
        {
            var success = await _configService.CapNhatHieuLucAsync(id, hieuLuc);
            if (!success)
            {
                return Json(new { success = false, message = "Không tìm thấy cấu hình chỉ định." });
            }

            return Json(new
            {
                success = true,
                message = $"Đã {(hieuLuc ? "bật" : "tắt")} cấu hình thành công."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật hiệu lực cấu hình ID={Id}", id);
            return Json(new { success = false, message = "Có lỗi xảy ra khi cập nhật cấu hình." });
        }
    }
}
