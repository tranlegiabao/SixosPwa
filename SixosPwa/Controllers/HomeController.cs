using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using WebPush;

namespace SixosPwa.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db, IConfiguration config)
    {
        _logger = logger;
        _db = db;
        _config = config;
    }

    public IActionResult Index()
    {
        ViewData["UserName"] = User.Identity?.Name ?? "Khách hàng";
        ViewData["UserRole"] = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
        return View();
    }

    [HttpGet]
    [Authorize(Roles = "Admin,DoiTac")]
    public IActionResult GuiTinNhan()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // -------------------------------------------------------------------------
    // Trả về VAPID Public Key để client đăng ký push subscription
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult VapidPublicKey()
    {
        var publicKey = _config["Vapid:PublicKey"] ?? "";
        return Json(new { publicKey });
    }

    // -------------------------------------------------------------------------
    // Lấy danh sách tài khoản User thật từ DB (cho GuiTinNhan dùng)
    // -------------------------------------------------------------------------
    [HttpGet]
    [Authorize(Roles = "Admin,DoiTac")]
    public async Task<IActionResult> DanhSachNguoiDung()
    {
        var danhSach = await _db.TaiKhoans
            .Where(t => t.Role == "User")
            .Select(t => new { t.Id, t.SDT, t.Role })
            .ToListAsync();
        return Json(danhSach);
    }

    // -------------------------------------------------------------------------
    // Nhận và lưu push subscription của thiết bị vào DB
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> DangKyPush([FromBody] PushSubscriptionRequest model)
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt) || string.IsNullOrEmpty(model.Endpoint))
            return Json(new { success = false });

        // Kiểm tra đã có endpoint này chưa (tránh lưu trùng)
        var existing = await _db.PushDangKys
            .FirstOrDefaultAsync(p => p.Endpoint == model.Endpoint);

        if (existing != null)
        {
            // Cập nhật SDT nếu đã có (thiết bị đổi tài khoản)
            existing.SDT = sdt;
            existing.P256dh = model.P256dh ?? "";
            existing.Auth = model.Auth ?? "";
            existing.ThoiGian = DateTime.Now;
        }
        else
        {
            await _db.PushDangKys.AddAsync(new PushDangKy
            {
                SDT = sdt,
                Endpoint = model.Endpoint,
                P256dh = model.P256dh ?? "",
                Auth = model.Auth ?? "",
                ThoiGian = DateTime.Now
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Đăng ký push thành công cho {SDT}", sdt);
        return Json(new { success = true });
    }

    // -------------------------------------------------------------------------
    // Gửi tin nhắn hàng loạt – lưu DB + gửi Web Push tới từng thiết bị
    // -------------------------------------------------------------------------
    [HttpPost]
    [Authorize(Roles = "Admin,DoiTac")]
    public async Task<IActionResult> GuiTinNhan([FromBody] SendSmsRequest model)
    {
        var nguoiGui = User.Identity?.Name;
        if (string.IsNullOrEmpty(nguoiGui))
            return Json(new { success = false, message = "Không tìm thấy thông tin đăng nhập!" });

        var smsMessage = string.IsNullOrWhiteSpace(model.Message) ? "test api gửi tin nhắn" : model.Message.Trim();
        var danhSachNhan = model.DanhSachNguoiNhan ?? new List<string>();

        if (danhSachNhan.Count == 0)
            return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 bệnh nhân!" });

        var now = DateTime.Now;

        // 1) Lưu ThongBao vào DB
        var thongBaos = danhSachNhan.Select(sdt => new ThongBao
        {
            NoiDung = smsMessage,
            ThoiGian = now,
            NguoiGui = nguoiGui,
            NguoiNhan = sdt,
            DaDoc = false
        }).ToList();
        await _db.ThongBaos.AddRangeAsync(thongBaos);
        await _db.SaveChangesAsync();

        // 2) Gửi Web Push tới tất cả thiết bị đã đăng ký của từng bệnh nhân
        var vapidPublicKey = _config["Vapid:PublicKey"] ?? "";
        var vapidPrivateKey = _config["Vapid:PrivateKey"] ?? "";
        var vapidSubject = _config["Vapid:Subject"] ?? "mailto:admin@hissoft.vn";

        var webPushClient = new WebPushClient();
        webPushClient.SetVapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);

        var danhSachSubscription = await _db.PushDangKys
            .Where(p => danhSachNhan.Contains(p.SDT))
            .ToListAsync();

        int pushOk = 0, pushFail = 0;
        foreach (var sub in danhSachSubscription)
        {
            try
            {
                var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title = "💬 Tin nhắn mới từ HisSoft",
                    body = smsMessage,
                    icon = "/static/icon-192.png",
                    badge = "/static/icon-192.png",
                    sender = nguoiGui,
                    url = "/"
                });
                await webPushClient.SendNotificationAsync(subscription, payload);
                pushOk++;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                           || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription hết hạn – xoá khỏi DB
                _db.PushDangKys.Remove(sub);
                pushFail++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi gửi push cho {SDT}: {Message}", sub.SDT, ex.Message);
                pushFail++;
            }
        }

        if (pushFail > 0) await _db.SaveChangesAsync(); // Lưu xoá subscription lỗi

        _logger.LogInformation("Gửi {Total} thông báo: {Ok} push thành công, {Fail} lỗi", 
            danhSachNhan.Count, pushOk, pushFail);

        return Json(new
        {
            success = true,
            message = $"Đã gửi thành công tin nhắn tới {danhSachNhan.Count} bệnh nhân!",
            pushOk,
            pushFail
        });
    }

    // -------------------------------------------------------------------------
    // Lấy danh sách thông báo chưa đọc của tài khoản hiện tại
    // -------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> LayThongBao()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
            return Json(new { success = false, soMoi = 0, danhSach = Array.Empty<object>() });

        var danhSach = await _db.ThongBaos
            .Where(t => t.NguoiNhan == sdt)
            .OrderByDescending(t => t.ThoiGian)
            .Take(20)
            .Select(t => new
            {
                t.Id,
                t.NoiDung,
                t.NguoiGui,
                t.DaDoc,
                ThoiGian = t.ThoiGian.ToString("HH:mm dd/MM/yyyy")
            })
            .ToListAsync();

        var soMoi = danhSach.Count(t => !t.DaDoc);
        return Json(new { success = true, soMoi, danhSach });
    }

    // -------------------------------------------------------------------------
    // Đánh dấu tất cả thông báo của user là đã đọc
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> DanhDauDaDoc()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
            return Json(new { success = false });

        var chuaDoc = await _db.ThongBaos
            .Where(t => t.NguoiNhan == sdt && !t.DaDoc)
            .ToListAsync();

        chuaDoc.ForEach(t => t.DaDoc = true);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}

// ── Request models ─────────────────────────────────────────────────────────

public class SendSmsRequest
{
    public string Message { get; set; } = string.Empty;
    public List<string> DanhSachNguoiNhan { get; set; } = new();
}

public class PushSubscriptionRequest
{
    public string? Endpoint { get; set; }
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
}
