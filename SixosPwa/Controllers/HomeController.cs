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

    public IActionResult Index(long? phongKhamId)
    {
        ViewData["UserName"] = User.Identity?.Name ?? "Khách hàng";
        ViewData["UserRole"] = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";
        ViewData["PhongKhamId"] = phongKhamId;
        
        // Lấy thông tin phòng khám nếu có
        if (phongKhamId.HasValue)
        {
            var phongKham = _db.PhongKhams.FirstOrDefault(p => p.Id == phongKhamId.Value);
            ViewData["TenPhongKham"] = phongKham?.TenPhongKham ?? "Phòng khám";
        }
        
        return View();
    }

    public IActionResult TimBacSi(long phongKhamId)
    {
        ViewData["PhongKhamId"] = phongKhamId;
        var phongKham = _db.PhongKhams.FirstOrDefault(p => p.Id == phongKhamId);
        ViewData["TenPhongKham"] = phongKham?.TenPhongKham ?? "Phòng khám";
        return View();
    }

    public IActionResult HoSoBenhAn(long phongKhamId)
    {
        ViewData["PhongKhamId"] = phongKhamId;
        var phongKham = _db.PhongKhams.FirstOrDefault(p => p.Id == phongKhamId);
        ViewData["TenPhongKham"] = phongKham?.TenPhongKham ?? "Phòng khám";
        return View();
    }

    [HttpGet]
    public IActionResult DanhSachCoSo(string type)
    {
        // Danh mục tương ứng
        string title = "Cơ sở y tế";
        switch (type)
        {
            case "benhvien": title = "Bệnh viện"; break;
            case "pkdk": title = "Phòng khám đa khoa"; break;
            case "nhakhoa": title = "Nha khoa"; break;
            case "phongmach": title = "Phòng mạch"; break;
            case "nhathuoc": title = "Nhà thuốc"; break;
        }
        ViewData["Title"] = title;
        ViewData["Type"] = type;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ThongTinBenhNhan()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
        {
            return RedirectToAction("Login", "DangNhap");
        }

        ViewData["UserName"] = sdt;
        ViewData["UserRole"] = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "User";

        // Lấy thông tin bệnh nhân
        var benhNhan = await _db.BenhNhans
            .FirstOrDefaultAsync(b => b.SDT == sdt);

        // Nếu chưa có bệnh nhân, tạo mới tự động
        if (benhNhan == null)
        {
            benhNhan = new BenhNhan
            {
                MaBN = $"BN-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                MaDT = "DT001",
                SDT = sdt,
                TenBN = $"Bệnh nhân {sdt.Substring(sdt.Length - 4)}",
                DiaChi = "Chưa cập nhật",
                Email = ""
            };
            _db.BenhNhans.Add(benhNhan);
            await _db.SaveChangesAsync();
        }

        ViewData["MaBN"] = benhNhan.MaBN;
        ViewData["TenBN"] = benhNhan.TenBN;
        ViewData["DiaChi"] = benhNhan.DiaChi ?? "";
        ViewData["Email"] = benhNhan.Email ?? "";

        // Lấy danh sách phòng khám đã khám
        var lichSuKham = await _db.LichSuKhams
            .Include(ls => ls.PhongKham)
            .Where(ls => ls.MaBN == benhNhan.MaBN)
            .OrderByDescending(ls => ls.NgayKhamGanNhat)
            .ToListAsync();

        // Nếu chưa có lịch sử khám, tạo dữ liệu mẫu
        if (lichSuKham.Count == 0)
        {
            // Đảm bảo có ít nhất phòng khám PKDK Bảo Minh
            var phongKhamBaoMinh = await _db.PhongKhams.FirstOrDefaultAsync(p => p.MaPhongKham == "PKDK-BM");
            if (phongKhamBaoMinh != null)
            {
                var lichSuMoi = new List<LichSuKham>
                {
                    new LichSuKham
                    {
                        MaBN = benhNhan.MaBN,
                        PhongKhamId = phongKhamBaoMinh.Id,
                        NgayKhamDau = DateTime.Now.AddMonths(-6),
                        NgayKhamGanNhat = DateTime.Now.AddDays(-5),
                        SoLanKham = 8,
                        TrangThai = "Đang theo dõi định kỳ"
                    }
                };

                // Thêm phòng khám khác nếu có
                var phongKhamKhac = await _db.PhongKhams
                    .Where(p => p.MaPhongKham != "PKDK-BM")
                    .Take(2)
                    .ToListAsync();

                foreach (var pk in phongKhamKhac)
                {
                    lichSuMoi.Add(new LichSuKham
                    {
                        MaBN = benhNhan.MaBN,
                        PhongKhamId = pk.Id,
                        NgayKhamDau = DateTime.Now.AddMonths(-4),
                        NgayKhamGanNhat = DateTime.Now.AddMonths(-1),
                        SoLanKham = new Random().Next(2, 6),
                        TrangThai = "Ổn định"
                    });
                }

                _db.LichSuKhams.AddRange(lichSuMoi);
                await _db.SaveChangesAsync();

                // Load lại dữ liệu
                lichSuKham = await _db.LichSuKhams
                    .Include(ls => ls.PhongKham)
                    .Where(ls => ls.MaBN == benhNhan.MaBN)
                    .OrderByDescending(ls => ls.NgayKhamGanNhat)
                    .ToListAsync();
            }
        }

        return View(lichSuKham);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,DoiTac")]
    public IActionResult GuiTinNhan()
    {
        var doiTacs = _db.DoiTacs.ToList();
        return View(doiTacs);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,DoiTac")]
    public IActionResult LocDanhSachBN([FromBody] LocBNRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.TenDT) || string.IsNullOrWhiteSpace(model.Password))
            return Json(new { success = false, message = "Vui lòng nhập đủ thông tin đối tác và mật khẩu." });

        try
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = "LocDanhSachBN";

            var pTenDT = cmd.CreateParameter();
            pTenDT.ParameterName = "@TenDT";
            pTenDT.Value = model.TenDT;
            cmd.Parameters.Add(pTenDT);

            var pPassword = cmd.CreateParameter();
            pPassword.ParameterName = "@Password";
            pPassword.Value = model.Password;
            cmd.Parameters.Add(pPassword);

            using var reader = cmd.ExecuteReader();

            // Kiểm tra kết quả đầu tiên – có thể là lỗi xác thực
            if (reader.FieldCount == 2 && reader.GetName(0) == "Success")
            {
                if (reader.Read())
                {
                    bool ok = reader.GetBoolean(0);
                    string msg = reader.GetString(1);
                    return Json(new { success = ok, message = msg });
                }
            }

            // Kết quả bình thường – danh sách bệnh nhân
            var list = new List<object>();
            while (reader.Read())
            {
                list.Add(new
                {
                    id    = reader["ID"],
                    maBN  = reader["MaBN"].ToString(),
                    maDT  = reader["MaDT"].ToString(),
                    sdt   = reader["SDT"].ToString(),
                    tenBN = reader["TenBN"].ToString(),
                    diaChi = reader["DiaChi"]?.ToString() ?? "",
                    email  = reader["Email"]?.ToString() ?? ""
                });
            }

            return Json(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stored procedure LocDanhSachBN");
            return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
        }
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
            existing.IdThietBi = model.DeviceId;
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
                IdThietBi = model.DeviceId,
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

    // -------------------------------------------------------------------------
    // Gửi tin nhắn trả lời (Patient -> Admin/DoiTac, hoặc ngược lại)
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> TraLoiTinNhan([FromBody] ReplyRequest model)
    {
        var nguoiGui = User.Identity?.Name;
        if (string.IsNullOrEmpty(nguoiGui))
            return Json(new { success = false, message = "Vui lòng đăng nhập lại." });

        if (string.IsNullOrWhiteSpace(model.Message) || string.IsNullOrWhiteSpace(model.NguoiNhan))
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

        var now = DateTime.Now;

        // Lưu vào DB
        var msg = new ThongBao
        {
            NoiDung = model.Message.Trim(),
            ThoiGian = now,
            NguoiGui = nguoiGui,
            NguoiNhan = model.NguoiNhan,
            DaDoc = false
        };
        await _db.ThongBaos.AddAsync(msg);
        await _db.SaveChangesAsync();

        // Gửi Push (Tái sử dụng logic gửi)
        var vapidPublicKey = _config["Vapid:PublicKey"] ?? "";
        var vapidPrivateKey = _config["Vapid:PrivateKey"] ?? "";
        var vapidSubject = _config["Vapid:Subject"] ?? "mailto:admin@hissoft.vn";

        var webPushClient = new WebPushClient();
        if (!string.IsNullOrEmpty(vapidPublicKey) && !string.IsNullOrEmpty(vapidPrivateKey))
        {
            webPushClient.SetVapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
        }

        var danhSachSubscription = await _db.PushDangKys
            .Where(p => p.SDT == model.NguoiNhan)
            .ToListAsync();

        foreach (var sub in danhSachSubscription)
        {
            try
            {
                var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title = "💬 Phản hồi từ " + nguoiGui,
                    body = model.Message.Trim(),
                    icon = "/static/icon-192.png",
                    badge = "/static/icon-192.png",
                    sender = nguoiGui,
                    url = "/"
                });
                await webPushClient.SendNotificationAsync(subscription, payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi gửi push cho {SDT}: {Message}", sub.SDT, ex.Message);
            }
        }

        return Json(new { 
            success = true, 
            message = "Đã gửi phản hồi thành công.",
            data = new {
                id = msg.Id,
                noiDung = msg.NoiDung,
                nguoiGui = msg.NguoiGui,
                nguoiNhan = msg.NguoiNhan,
                thoiGian = msg.ThoiGian.ToString("HH:mm dd/MM/yyyy")
            }
        });
    }

    // -------------------------------------------------------------------------
    // Lấy toàn bộ lịch sử tin nhắn giữa người dùng hiện tại và một đối tác/bệnh nhân
    // -------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> LayLichSuTinNhan([FromQuery] string doiTac)
    {
        var me = User.Identity?.Name;
        if (string.IsNullOrEmpty(me) || string.IsNullOrEmpty(doiTac))
            return Json(new { success = false });

        var messages = await _db.ThongBaos
            .Where(t => (t.NguoiGui == me && t.NguoiNhan == doiTac) || 
                        (t.NguoiGui == doiTac && t.NguoiNhan == me))
            .OrderBy(t => t.ThoiGian)
            .Select(t => new {
                t.Id,
                t.NoiDung,
                t.NguoiGui,
                t.NguoiNhan,
                t.DaDoc,
                ThoiGian = t.ThoiGian.ToString("HH:mm dd/MM/yyyy")
            })
            .ToListAsync();

        return Json(new { success = true, messages });
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
    public string? DeviceId { get; set; }
}

public class LocBNRequest
{
    public string TenDT { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ReplyRequest
{
    public string NguoiNhan { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
