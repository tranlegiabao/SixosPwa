using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Models;

namespace SixosPwa.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        ViewData["UserName"] = User.Identity?.Name ?? "Khách hàng";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpPost]
    public IActionResult GuiTinNhan([FromBody] SendSmsRequest model)
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
        {
            return Json(new { success = false, message = "Không tìm thấy thông tin đăng nhập!" });
        }

        var smsMessage = string.IsNullOrWhiteSpace(model.Message) ? "test api gửi tin nhắn" : model.Message.Trim();

        _logger.LogInformation("Gửi SMS đến {Sdt}: {Message}", sdt, smsMessage);

        return Json(new { 
            success = true, 
            message = $"Đã gửi thành công tin nhắn tới số {sdt}!",
            details = $"[SMS Simulated] To: {sdt} | Content: {smsMessage}"
        });
    }
}

public class SendSmsRequest
{
    public string Message { get; set; } = string.Empty;
}
