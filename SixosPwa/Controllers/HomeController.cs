using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Security;
using SixosPwa.Services.Partner;
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

    // Action Index (trang benh nhan cu) da duoc go bo ngay 2026-08-22 theo yeu cau
    // cua user: luong do khong dung nua, thay bang /benh-nhan. Lay lai neu can:
    //   git show 224341a -- SixosPwa/Views/Home/Index.cshtml

    public IActionResult TimBacSi(long phongKhamId)
    {
        ViewData["PhongKhamId"] = phongKhamId;
        var phongKham = _db.PhongKhams.FirstOrDefault(p => p.Id == phongKhamId);
        ViewData["TenPhongKham"] = phongKham?.TenPhongKham ?? "PhÃ²ng khÃ¡m";
        return View();
    }

    public IActionResult HoSoBenhAn(long phongKhamId)
    {
        ViewData["PhongKhamId"] = phongKhamId;
        var phongKham = _db.PhongKhams.FirstOrDefault(p => p.Id == phongKhamId);
        ViewData["TenPhongKham"] = phongKham?.TenPhongKham ?? "PhÃ²ng khÃ¡m";
        return View();
    }

    [HttpGet("/Home/DanhSachCoSo-{type}")]
    [AllowAnonymous]
    public async Task<IActionResult> DanhSachCoSo(string type)
    {
        // Danh má»¥c tÆ°Æ¡ng á»©ng
        string title = "CÆ¡ sá»Ÿ y táº¿";
        switch (type)
        {
            case "benhvien": title = "Bá»‡nh viá»‡n"; break;
            case "pkdk": title = "PhÃ²ng khÃ¡m Ä‘a khoa"; break;
            case "nhakhoa": title = "Nha khoa"; break;
            case "phongmach": title = "PhÃ²ng máº¡ch"; break;
            case "nhathuoc": title = "NhÃ  thuá»‘c"; break;
        }
        ViewData["Title"] = title;
        ViewData["Type"] = type;

        var dsCoso = await _db.DMCSKCBs
            .Where(x => x.LoaiCS == type)
            .OrderByDescending(x => x.QuangCao)
            .ToListAsync();

        return View(dsCoso);
    }

    /// <summary>
    /// URL co dinh cua tung co so. Slug do quan tri vien dat tay (DMCSKCB.Slug),
    /// KHONG sinh tu ten, nen doi ten co so khong lam gay URL da phat cho doi tac.
    /// </summary>
    [HttpGet("/pk/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> ChiTietCoSo(string slug)
    {
        var coSo = await _db.DMCSKCBs.FirstOrDefaultAsync(x => x.Slug == slug);

        if (coSo == null) return NotFound();

        await DoDuLieuCoSoAsync(coSo);
        return View();
    }

    /// <summary>
    /// URL cu khop co so bang cach bo dau ten. Giu lai va chuyen huong 301 sang
    /// /pk/{slug} de moi duong link da phat di khong chet.
    /// </summary>
    [HttpGet("/Home/DangKyOnline/{ten?}")]
    [AllowAnonymous]
    public async Task<IActionResult> ChiTietCoSoTheoTen(string? ten, string? diaChi, string? type, string? img, string? logo)
    {
        if (!string.IsNullOrEmpty(ten))
        {
            var cleanTen = RemoveAccentsAndSpaces(ten);
            var allCS = await _db.DMCSKCBs.ToListAsync();
            var matchedCS = allCS.FirstOrDefault(x =>
                RemoveAccentsAndSpaces(x.TenCoSo ?? "").Equals(cleanTen, StringComparison.OrdinalIgnoreCase) ||
                (x.TenCoSo ?? "").Equals(ten, StringComparison.OrdinalIgnoreCase));

            if (matchedCS != null)
            {
                if (!string.IsNullOrWhiteSpace(matchedCS.Slug))
                {
                    return RedirectPermanent($"/pk/{matchedCS.Slug}");
                }

                // Co so chua duoc dat slug: van hien duoc trang, chi la khong co
                // URL co dinh. Quan tri vien dat slug trong man Admin/CoSoYTe.
                await DoDuLieuCoSoAsync(matchedCS);
                return View(nameof(ChiTietCoSo));
            }
        }

        ViewData["TenCoSo"] = ten ?? "CÆ¡ sá»Ÿ y táº¿";
        ViewData["DiaChi"] = diaChi ?? "Äang cáº­p nháº­t";
        ViewData["Type"] = type ?? "benhvien";
        ViewData["Img"] = img ?? AnhCoSoMacDinh;
        ViewData["Logo"] = logo ?? LogoCoSoMacDinh;
        return View(nameof(ChiTietCoSo));
    }

    /// <summary>
    /// Trang chu cua benh nhan tai co so KHONG co API rieng. Dot 2026-08 moi chi
    /// dung giao dien: ba the dich vu deu dan toi man "Dang cap nhat".
    /// </summary>
    [HttpGet("/benh-nhan")]
    public async Task<IActionResult> TrangBenhNhan(string? loi = null)
    {
        var cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        var coSo = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);

        var benhNhan = string.IsNullOrWhiteSpace(dinhDanh)
            ? null
            : await _db.BenhNhans.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SDT == dinhDanh || x.Email == dinhDanh);

        ViewBag.MaCoSo = maCoSo;
        ViewBag.TenCoSo = coSo?.TenCoSo ?? "CÆ¡ sá»Ÿ khÃ¡m chá»¯a bá»‡nh";
        ViewBag.TenBenhNhan = benhNhan?.TenBN ?? dinhDanh;
        ViewBag.DienThoai = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value ?? benhNhan?.SDT;
        ViewBag.CccdCheBot = CheBotCccd(cccd);
        ViewBag.CoLoiKetNoi = loi == "khong-ket-noi-duoc";

        return View();
    }

    private static string? GetOperatingHoursValue(DMCSKCB coSo)
    {
        if (!string.IsNullOrWhiteSpace(coSo.NgayLamViec)
            && coSo.GioMoCua.HasValue
            && coSo.GioDongCua.HasValue)
        {
            return OperatingHours.Encode(
                coSo.NgayLamViec,
                coSo.GioMoCua.Value.ToString(@"hh\:mm"),
                coSo.GioDongCua.Value.ToString(@"hh\:mm"));
        }

        return coSo.TGLamViec;
    }

    /// <summary>Man trong cho ba the chua noi du lieu.</summary>
    [HttpGet("/benh-nhan/sap-co")]
    public IActionResult SapCo(string? muc = null)
    {
        (ViewBag.TenMuc, ViewBag.BieuTuong) = muc switch
        {
            "dat-goi-kham" => ("ÄÄƒng kÃ½ khÃ¡m theo gÃ³i", "â–¤"),
            "lich-su-hen" => ("Lá»‹ch sá»­ háº¹n khÃ¡m", "â—·"),
            "ho-so-kham" => ("Tra cá»©u há»“ sÆ¡ khÃ¡m bá»‡nh", "â—«"),
            _ => ("Chá»©c nÄƒng", "â—Œ")
        };

        return View();
    }

    /// <summary>Che bot so CCCD khi hien tren man: 0772â€¢â€¢â€¢â€¢â€¢069.</summary>
    private static string CheBotCccd(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return "â€”";
        if (cccd.Length <= 7) return cccd;

        return $"{cccd[..4]}{new string('\u2022', cccd.Length - 7)}{cccd[^3..]}";
    }

    private const string AnhCoSoMacDinh = "https://images.unsplash.com/photo-1519494026892-80bbd2d6fd0d?w=800&q=80";
    private const string LogoCoSoMacDinh = "https://tse1.mm.bing.net/th/id/OIP.JgUNpJPll-8BkzE3XN6LggHaHa?r=0&pid=Api&P=0&h=180";

    /// <summary>
    /// Do du lieu mot co so ra ViewData cho trang chi tiet. MaCoSo va Slug la hai
    /// thu hai nut "Dang ky kham" / "Dang nhap" phai mang theo â€” thieu chung thi
    /// man dang nhap khong biet benh nhan dang o co so nao.
    /// </summary>
    private async Task DoDuLieuCoSoAsync(DMCSKCB coSo)
    {
        ViewData["MaCoSo"] = coSo.MaCoSo;
        ViewData["Slug"] = coSo.Slug;
        ViewData["TenCoSo"] = coSo.TenCoSo;
        ViewData["DiaChi"] = coSo.DiaChi ?? "Äang cáº­p nháº­t";
        ViewData["Type"] = coSo.LoaiCS ?? "benhvien";
        ViewData["Img"] = coSo.Img ?? AnhCoSoMacDinh;
        ViewData["Logo"] = coSo.logo ?? LogoCoSoMacDinh;
        ViewData["TGLamViec"] = GetOperatingHoursValue(coSo);
        ViewData["NoiDungCskcb"] = await LoadNoiDungAsync(coSo);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadNoiDungAsync(DMCSKCB coSo)
    {
        IQueryable<NDCSKCB> query = _db.NDCSKCBs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(coSo.MaCoSo))
        {
            query = query.Where(x => x.MaCoSo == coSo.MaCoSo);
        }
        else
        {
            query = query.Where(x => (x.MaCoSo == null || x.MaCoSo == "") && x.TenCoSo == coSo.TenCoSo);
        }

        var topicById = (await _db.DMChuDes.AsNoTracking().ToListAsync())
            .Where(x => !string.IsNullOrWhiteSpace(x.LoaiND))
            .ToDictionary(x => x.ID.ToString(), x => x.LoaiND!, StringComparer.OrdinalIgnoreCase);
        var items = await query
            .OrderBy(x => x.Id)
            .ToListAsync();

        return items
            .Select(x => new
            {
                Item = x,
                LoaiND = ResolveLoaiND(x.LoaiND, topicById)
            })
            .Where(x => x.LoaiND != null
                && NDCSKCB.AllowedLoaiND.Contains(x.LoaiND, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.LoaiND!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Item.NoiDung ?? "", StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveLoaiND(
        string? storedLoaiND,
        IReadOnlyDictionary<string, string> topicById)
    {
        var value = storedLoaiND?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (topicById.TryGetValue(value, out var loaiND)) return loaiND;

        return NDCSKCB.AllowedLoaiND.FirstOrDefault(x =>
            string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string RemoveAccentsAndSpaces(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        string cleanText = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        
        cleanText = cleanText.Replace("Ä‘", "d").Replace("Ä", "D");
        
        var finalSb = new System.Text.StringBuilder();
        foreach (char c in cleanText)
        {
            if (char.IsLetterOrDigit(c))
            {
                finalSb.Append(c);
            }
        }
        return finalSb.ToString();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ThongTinBenhNhan()
    {
        var topCSKCBList = new List<TopCSKCBQC>();
        try
        {
            var conn = _db.Database.GetDbConnection();
            bool wasClosed = conn.State == System.Data.ConnectionState.Closed;
            if (wasClosed)
            {
                await conn.OpenAsync();
            }

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandText = "Top_CSKCB_QC";

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    topCSKCBList.Add(new TopCSKCBQC
                    {
                        TenCoSo = reader["TenCoSo"]?.ToString() ?? "",
                        NoiDung = reader["NoiDung"]?.ToString() ?? "",
                        Img = reader["Img"]?.ToString() ?? ""
                    });
                }
            }
            finally
            {
                if (wasClosed && conn.State == System.Data.ConnectionState.Open)
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lá»—i khi gá»i stored procedure Top_CSKCB_QC");
        }

        ViewData["TopCSKCB"] = topCSKCBList;

        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
        {
            return View(new List<LichSuKham>());
        }

        ViewData["UserName"] = sdt;
        ViewData["UserRole"] = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "BenhNhan";

        // Láº¥y thÃ´ng tin bá»‡nh nhÃ¢n
        var benhNhan = await _db.BenhNhans
            .FirstOrDefaultAsync(b => b.SDT == sdt);

        // Náº¿u chÆ°a cÃ³ bá»‡nh nhÃ¢n, táº¡o má»›i tá»± Ä‘á»™ng
        if (benhNhan == null)
        {
            benhNhan = new BenhNhan
            {
                MaBN = $"BN-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                MaDT = "DT001",
                SDT = sdt,
                TenBN = $"Bá»‡nh nhÃ¢n {sdt.Substring(sdt.Length - 4)}",
                DiaChi = "ChÆ°a cáº­p nháº­t",
                Email = ""
            };
            _db.BenhNhans.Add(benhNhan);
            await _db.SaveChangesAsync();
        }

        ViewData["MaBN"] = benhNhan.MaBN;
        ViewData["TenBN"] = benhNhan.TenBN;
        ViewData["DiaChi"] = benhNhan.DiaChi ?? "";
        ViewData["Email"] = benhNhan.Email ?? "";

        // Láº¥y danh sÃ¡ch phÃ²ng khÃ¡m Ä‘Ã£ khÃ¡m
        var lichSuKham = await _db.LichSuKhams
            .Include(ls => ls.PhongKham)
            .Where(ls => ls.MaBN == benhNhan.MaBN)
            .OrderByDescending(ls => ls.NgayKhamGanNhat)
            .ToListAsync();

        // Náº¿u chÆ°a cÃ³ lá»‹ch sá»­ khÃ¡m, táº¡o dá»¯ liá»‡u máº«u
        if (lichSuKham.Count == 0)
        {
            // Äáº£m báº£o cÃ³ Ã­t nháº¥t phÃ²ng khÃ¡m PKDK Báº£o Minh
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
                        TrangThai = "Äang theo dÃµi Ä‘á»‹nh ká»³"
                    }
                };

                // ThÃªm phÃ²ng khÃ¡m khÃ¡c náº¿u cÃ³
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
                        TrangThai = "á»”n Ä‘á»‹nh"
                    });
                }

                _db.LichSuKhams.AddRange(lichSuMoi);
                await _db.SaveChangesAsync();

                // Load láº¡i dá»¯ liá»‡u
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
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public IActionResult GuiTinNhan()
    {
        var doiTacs = _db.DoiTacs.ToList();
        return View(doiTacs);
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public IActionResult LocDanhSachBN([FromBody] LocBNRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.TenDT) || string.IsNullOrWhiteSpace(model.Password))
            return Json(new { success = false, message = "Vui lÃ²ng nháº­p Ä‘á»§ thÃ´ng tin Ä‘á»‘i tÃ¡c vÃ  máº­t kháº©u." });

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

            // Kiá»ƒm tra káº¿t quáº£ Ä‘áº§u tiÃªn â€“ cÃ³ thá»ƒ lÃ  lá»—i xÃ¡c thá»±c
            if (reader.FieldCount == 2 && reader.GetName(0) == "Success")
            {
                if (reader.Read())
                {
                    bool ok = reader.GetBoolean(0);
                    string msg = reader.GetString(1);
                    return Json(new { success = ok, message = msg });
                }
            }

            // Káº¿t quáº£ bÃ¬nh thÆ°á»ng â€“ danh sÃ¡ch bá»‡nh nhÃ¢n
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
            _logger.LogError(ex, "Lá»—i khi gá»i stored procedure LocDanhSachBN");
            return Json(new { success = false, message = "Lá»—i há»‡ thá»‘ng: " + ex.Message });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // -------------------------------------------------------------------------
    // Tráº£ vá» VAPID Public Key Ä‘á»ƒ client Ä‘Äƒng kÃ½ push subscription
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult VapidPublicKey()
    {
        var publicKey = _config["Vapid:PublicKey"] ?? "";
        return Json(new { publicKey });
    }

    // -------------------------------------------------------------------------
    // Láº¥y danh sÃ¡ch tÃ i khoáº£n bá»‡nh nhÃ¢n tháº­t tá»« DB (cho GuiTinNhan dÃ¹ng)
    // -------------------------------------------------------------------------
    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> DanhSachNguoiDung()
    {
        var danhSach = await _db.TaiKhoans
            .Where(t => t.Role != "Admin")
            .Select(t => new { t.Id, t.SDT, t.Role })
            .ToListAsync();
        return Json(danhSach);
    }

    // -------------------------------------------------------------------------
    // Nháº­n vÃ  lÆ°u push subscription cá»§a thiáº¿t bá»‹ vÃ o DB
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> DangKyPush([FromBody] PushSubscriptionRequest model)
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt) || string.IsNullOrEmpty(model.Endpoint))
            return Json(new { success = false });

        // Kiá»ƒm tra Ä‘Ã£ cÃ³ endpoint nÃ y chÆ°a (trÃ¡nh lÆ°u trÃ¹ng)
        var existing = await _db.PushDangKys
            .FirstOrDefaultAsync(p => p.Endpoint == model.Endpoint);

        if (existing != null)
        {
            // Cáº­p nháº­t SDT náº¿u Ä‘Ã£ cÃ³ (thiáº¿t bá»‹ Ä‘á»•i tÃ i khoáº£n)
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
        _logger.LogInformation("ÄÄƒng kÃ½ push thÃ nh cÃ´ng cho {SDT}", sdt);
        return Json(new { success = true });
    }

    // -------------------------------------------------------------------------
    // Gá»­i tin nháº¯n hÃ ng loáº¡t â€“ lÆ°u DB + gá»­i Web Push tá»›i tá»«ng thiáº¿t bá»‹
    // -------------------------------------------------------------------------
    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> GuiTinNhan([FromBody] SendSmsRequest model)
    {
        var nguoiGui = User.Identity?.Name;
        if (string.IsNullOrEmpty(nguoiGui))
            return Json(new { success = false, message = "KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin Ä‘Äƒng nháº­p!" });

        var smsMessage = string.IsNullOrWhiteSpace(model.Message) ? "test api gá»­i tin nháº¯n" : model.Message.Trim();
        var danhSachNhan = model.DanhSachNguoiNhan ?? new List<string>();

        if (danhSachNhan.Count == 0)
            return Json(new { success = false, message = "Vui lÃ²ng chá»n Ã­t nháº¥t 1 bá»‡nh nhÃ¢n!" });

        var now = DateTime.Now;

        // 1) LÆ°u ThongBao vÃ o DB
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

        // 2) Gá»­i Web Push tá»›i táº¥t cáº£ thiáº¿t bá»‹ Ä‘Ã£ Ä‘Äƒng kÃ½ cá»§a tá»«ng bá»‡nh nhÃ¢n
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
                    title = "ðŸ’¬ Tin nháº¯n má»›i tá»« HisSoft",
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
                // Subscription háº¿t háº¡n â€“ xoÃ¡ khá»i DB
                _db.PushDangKys.Remove(sub);
                pushFail++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lá»—i gá»­i push cho {SDT}: {Message}", sub.SDT, ex.Message);
                pushFail++;
            }
        }

        if (pushFail > 0) await _db.SaveChangesAsync(); // LÆ°u xoÃ¡ subscription lá»—i

        _logger.LogInformation("Gá»­i {Total} thÃ´ng bÃ¡o: {Ok} push thÃ nh cÃ´ng, {Fail} lá»—i", 
            danhSachNhan.Count, pushOk, pushFail);

        return Json(new
        {
            success = true,
            message = $"ÄÃ£ gá»­i thÃ nh cÃ´ng tin nháº¯n tá»›i {danhSachNhan.Count} bá»‡nh nhÃ¢n!",
            pushOk,
            pushFail
        });
    }

    // -------------------------------------------------------------------------
    // Láº¥y danh sÃ¡ch thÃ´ng bÃ¡o chÆ°a Ä‘á»c cá»§a tÃ i khoáº£n hiá»‡n táº¡i
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
    // ÄÃ¡nh dáº¥u táº¥t cáº£ thÃ´ng bÃ¡o cá»§a user lÃ  Ä‘Ã£ Ä‘á»c
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
    // Gá»­i tin nháº¯n tráº£ lá»i (Patient -> Admin/DoiTac, hoáº·c ngÆ°á»£c láº¡i)
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> TraLoiTinNhan([FromBody] ReplyRequest model)
    {
        var nguoiGui = User.Identity?.Name;
        if (string.IsNullOrEmpty(nguoiGui))
            return Json(new { success = false, message = "Vui lÃ²ng Ä‘Äƒng nháº­p láº¡i." });

        if (string.IsNullOrWhiteSpace(model.Message) || string.IsNullOrWhiteSpace(model.NguoiNhan))
            return Json(new { success = false, message = "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡." });

        var now = DateTime.Now;

        // LÆ°u vÃ o DB
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

        // Gá»­i Push (TÃ¡i sá»­ dá»¥ng logic gá»­i)
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
                    title = "ðŸ’¬ Pháº£n há»“i tá»« " + nguoiGui,
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
                _logger.LogWarning("Lá»—i gá»­i push cho {SDT}: {Message}", sub.SDT, ex.Message);
            }
        }

        return Json(new { 
            success = true, 
            message = "ÄÃ£ gá»­i pháº£n há»“i thÃ nh cÃ´ng.",
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
    // Láº¥y lá»‹ch sá»­ trÃ² chuyá»‡n (Admin <-> Bá»‡nh nhÃ¢n)
    // -------------------------------------------------------------------------
    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> GetChatHistory(string sdtBenhNhan)
    {
        var adminId = User.Identity?.Name;
        if (string.IsNullOrEmpty(adminId) || string.IsNullOrEmpty(sdtBenhNhan))
            return Json(new { success = false, message = "Dá»¯ liá»‡u khÃ´ng há»£p lá»‡." });

        var messages = await _db.ThongBaos
            .Where(t => (t.NguoiGui == adminId && t.NguoiNhan == sdtBenhNhan) || 
                        (t.NguoiGui == sdtBenhNhan && t.NguoiNhan == adminId))
            .OrderBy(t => t.ThoiGian)
            .Select(t => new
            {
                id = t.Id,
                noiDung = t.NoiDung,
                thoiGian = t.ThoiGian.ToString("HH:mm dd/MM/yyyy"),
                isSender = t.NguoiGui == adminId,
                daDoc = t.DaDoc
            })
            .ToListAsync();

        return Json(new { success = true, data = messages });
    }

    // -------------------------------------------------------------------------
    // Láº¥y toÃ n bá»™ lá»‹ch sá»­ tin nháº¯n giá»¯a ngÆ°á»i dÃ¹ng hiá»‡n táº¡i vÃ  má»™t Ä‘á»‘i tÃ¡c/bá»‡nh nhÃ¢n
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

// â”€â”€ Request models â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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


