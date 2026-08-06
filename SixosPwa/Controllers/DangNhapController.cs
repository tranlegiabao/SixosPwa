using Microsoft.AspNetCore.Mvc;

namespace SixosPwa.Controllers;

/// <summary>
/// Man dang nhap cua khuon mau.
///
/// LUU Y: o ban khuon mau nay KHONG co xac thuc - bam Dang nhap la vao thang.
/// Man hinh ton tai vi 2 ly do: cho giong bo cuc that, va de co cho dat NUT CAI DAT.
/// Khi nhan ban thanh san pham that thi thay than ham Login(POST) ben duoi bang
/// kiem tra tai khoan that (xem README.md muc "Nhan ban khuon mau").
/// </summary>
public class DangNhapController : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(string? userName)
    {
        // Khong kiem tra gi. Giu lai ten go vao chi de trang chao hien thi cho vui.
        TempData["UserName"] = string.IsNullOrWhiteSpace(userName) ? "khách" : userName.Trim();
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
