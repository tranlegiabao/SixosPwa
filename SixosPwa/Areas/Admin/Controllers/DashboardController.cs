using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class DashboardController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Nam sua 2026-08-22: thu muc chua anh chen tu trinh soan thao noi dung co so.
    /// Cung ho voi static/img_cs, static/logo_cs ma CoSoYTeController dang dung.
    /// </summary>
    private const string ThuMucAnhNoiDung = "static/img_nd";

    private const long DungLuongAnhToiDa = 5 * 1024 * 1024;

    private static readonly HashSet<string> DuoiAnhChoPhep = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    public DashboardController(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public async Task<IActionResult> Index(long? facilityId = null, long? topicId = null)
    {
        var model = new DashboardViewModel
        {
            AccountCount = await _db.TaiKhoans.CountAsync(),
            AdminAccountCount = await _db.TaiKhoans.CountAsync(x => x.Role == "Admin"),
            PatientAccountCount = await _db.TaiKhoans.CountAsync(x => x.Role != "Admin"),
            PartnerCount = await _db.DoiTacs.CountAsync(),
            PatientCount = await _db.BenhNhans.CountAsync(),
            FacilityCount = await _db.DMCSKCBs.CountAsync(),
            VerifiedFacilityCount = await _db.DMCSKCBs.CountAsync(x => x.XacMinh == 1),
            NotificationCount = await _db.ThongBaos.CountAsync(),
            PushSubscriptionCount = await _db.PushDangKys.CountAsync(),
            UnreadNotificationCount = await _db.ThongBaos.CountAsync(x => !x.DaDoc),
            RecentNotifications = await _db.ThongBaos.AsNoTracking()
                .OrderByDescending(x => x.ThoiGian)
                .Take(6)
                .ToListAsync(),
            RecentAccounts = (await _db.TaiKhoans.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(6)
                .ToListAsync()).Select(x =>
                {
                    x.Role = NormalizeRole(x.Role);
                    return x;
                }).ToList(),
            RecentPartners = await _db.DoiTacs.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(4)
                .ToListAsync(),
            RecentFacilities = await _db.DMCSKCBs.AsNoTracking().OrderByDescending(x => x.Id).Take(4).ToListAsync(),
            NhomCSList = await _db.DMNhomCSs.AsNoTracking().ToListAsync(),
            ChuDeList = await _db.DMChuDes.AsNoTracking().ToListAsync(),
            FacilityList = await _db.DMCSKCBs.AsNoTracking().ToListAsync(),
            // Nam sua 2026-08-22: giu lai lua chon sau khi Luu (POST-redirect-GET).
            SelectedFacilityId = facilityId,
            SelectedTopicId = topicId
        };

        return View(model);
    }

    // =========================================================================
    // Nam sua 2026-08-22: man /Admin truoc day KHONG co POST nao - nut "Luu"
    // khong nam trong form, khong JS bat su kien => noi dung soan xong bay mat.
    //
    // BAY CHET NGUOI - phai ghi LoaiND la CHUOI (DMChuDe.LoaiND: "gioithieu",
    // "dichvu"...), TUYET DOI khong phai DMChuDe.ID. HomeController.LoadNoiDungAsync
    // loc theo NDCSKCB.AllowedLoaiND (whitelist chuoi); ghi so thi trang co so
    // loc rot sach va hien trong tron.
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveContent(DashboardContentEditViewModel model)
    {
        var luaChon = new { facilityId = model.FacilityId, topicId = model.TopicId };

        if (!ModelState.IsValid)
        {
            Error("Vui lòng chọn phòng khám và chủ đề trước khi lưu.");
            return RedirectToAction(nameof(Index), luaChon);
        }

        var coSo = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.FacilityId);
        var chuDe = await _db.DMChuDes.AsNoTracking().FirstOrDefaultAsync(x => x.ID == model.TopicId);

        if (coSo == null || chuDe == null)
        {
            Error("Phòng khám hoặc chủ đề không hợp lệ.");
            return RedirectToAction(nameof(Index), luaChon);
        }

        var loaiND = chuDe.LoaiND?.Trim();
        if (string.IsNullOrEmpty(loaiND) || !NDCSKCB.AllowedLoaiND.Contains(loaiND))
        {
            Error($"Chủ đề \"{chuDe.TenChuDe}\" chưa gán mã hợp lệ ở danh mục DMChuDe (cột LoaiND).");
            return RedirectToAction(nameof(Index), luaChon);
        }

        var noiDungSach = LamSachHtml(model.NoiDung);

        var dongCu = await _db.NDCSKCBs
            .Where(x => x.MaCoSo == coSo.MaCoSo && x.LoaiND == loaiND)
            .ToListAsync();

        if (string.IsNullOrEmpty(noiDungSach))
        {
            if (dongCu.Count > 0) _db.NDCSKCBs.RemoveRange(dongCu);
        }
        else
        {
            var dong = dongCu.FirstOrDefault();
            if (dong == null)
            {
                _db.NDCSKCBs.Add(new NDCSKCB
                {
                    MaCoSo = coSo.MaCoSo,
                    TenCoSo = coSo.TenCoSo,
                    LoaiND = loaiND,
                    NoiDung = noiDungSach
                });
            }
            else
            {
                dong.MaCoSo = coSo.MaCoSo;
                dong.TenCoSo = coSo.TenCoSo;
                dong.NoiDung = noiDungSach;
                // Don dong trung neu du lieu cu co nhieu ban ghi cung LoaiND.
                _db.NDCSKCBs.RemoveRange(dongCu.Skip(1));
            }
        }

        await _db.SaveChangesAsync();

        Success(string.IsNullOrEmpty(noiDungSach)
            ? $"Đã xoá nội dung \"{chuDe.TenChuDe}\" của {coSo.TenCoSo}."
            : $"Đã lưu nội dung \"{chuDe.TenChuDe}\" cho {coSo.TenCoSo}.");
        return RedirectToAction(nameof(Index), luaChon);
    }

    /// <summary>
    /// Nap lai noi dung da luu vao trinh soan thao khi admin doi phong kham / chu de.
    /// Khong co endpoint nay thi khong sua duoc noi dung cu, chi ghi de duoc.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetContent(long facilityId, long topicId)
    {
        var coSo = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == facilityId);
        var chuDe = await _db.DMChuDes.AsNoTracking().FirstOrDefaultAsync(x => x.ID == topicId);
        if (coSo == null || chuDe == null) return NotFound();

        var loaiND = chuDe.LoaiND?.Trim();
        if (string.IsNullOrEmpty(loaiND) || !NDCSKCB.AllowedLoaiND.Contains(loaiND))
        {
            return Json(new { noiDung = string.Empty });
        }

        var noiDung = await _db.NDCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == coSo.MaCoSo && x.LoaiND == loaiND)
            .OrderBy(x => x.Id)
            .Select(x => x.NoiDung)
            .FirstOrDefaultAsync();

        return Json(new { noiDung = noiDung ?? string.Empty });
    }

    // =========================================================================
    // Nam sua 2026-08-22: endpoint upload anh cho TinyMCE.
    // TinyMCE doi JSON co khoa "location" (khong phai "url"), va doi HTTP 200
    // ke ca khi loi - bao loi bang khoa "error". Tra 4xx/5xx thi editor hien
    // "HTTP Error" chung chung, nguoi dung khong biet vi sao.
    //
    // Validate bam theo khuon CoSoYTeController.SaveImageAsync (5 MB, whitelist
    // duoi file, ten file Guid). CHEP logic sang day chu khong goi nguoc ve
    // CoSoYTeController: ham do la private, phu thuoc ModelState cua chinh no,
    // va file do dang bi nhanh khac sua - dung vao la va merge.
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return Json(new { error = "Chưa chọn ảnh." });
        }

        if (file.Length > DungLuongAnhToiDa)
        {
            return Json(new { error = "Ảnh phải nhỏ hơn 5 MB." });
        }

        var duoiFile = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(duoiFile) || !DuoiAnhChoPhep.Contains(duoiFile))
        {
            return Json(new { error = "Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF." });
        }

        var thuMuc = Path.Combine(
            _environment.WebRootPath,
            ThuMucAnhNoiDung.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(thuMuc);

        var tenFile = $"{Guid.NewGuid():N}{duoiFile.ToLowerInvariant()}";
        var duongDan = Path.Combine(thuMuc, tenFile);

        await using (var stream = new FileStream(duongDan, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        return Json(new { location = $"/{ThuMucAnhNoiDung}/{tenFile}" });
    }

    // =========================================================================
    // Loc HTML LUC GHI (khong phai luc doc): DB sach vinh vien, view chi viec
    // Html.Raw. Trang chi tiet co so la trang CONG KHAI nen mot tai khoan admin
    // bi chiem se thanh stored XSS neu khong loc.
    // =========================================================================
    private static readonly HtmlSanitizer BoLoc = TaoBoLoc();

    private static HtmlSanitizer TaoBoLoc()
    {
        var loc = new HtmlSanitizer();

        loc.AllowedTags.Clear();
        foreach (var the in new[]
                 {
                     "p", "br", "b", "strong", "i", "em", "u", "s", "sub", "sup",
                     "ul", "ol", "li", "h1", "h2", "h3", "h4", "blockquote", "hr",
                     "table", "thead", "tbody", "tfoot", "tr", "th", "td",
                     "img", "a", "span", "div"
                 })
        {
            loc.AllowedTags.Add(the);
        }

        loc.AllowedAttributes.Clear();
        foreach (var thuocTinh in new[]
                 {
                     "style", "class", "src", "alt", "href", "title",
                     "width", "height", "colspan", "rowspan", "target", "rel"
                 })
        {
            loc.AllowedAttributes.Add(thuocTinh);
        }

        loc.AllowedCssProperties.Clear();
        foreach (var css in new[]
                 {
                     "color", "background-color", "font-size", "font-weight",
                     "font-style", "text-align", "text-decoration",
                     "width", "height", "margin", "padding", "border",
                     "border-collapse", "list-style-type"
                 })
        {
            loc.AllowedCssProperties.Add(css);
        }

        loc.AllowedSchemes.Clear();
        loc.AllowedSchemes.Add("http");
        loc.AllowedSchemes.Add("https");
        loc.AllowedSchemes.Add("mailto");

        return loc;
    }

    private static string LamSachHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var sach = BoLoc.Sanitize(html).Trim();

        // TinyMCE tra "<p>&nbsp;</p>" khi o soan thao rong -> phai coi la RONG,
        // neu khong se luu rac va trang co so hien mot dong trong thay vi doan
        // gioi thieu mac dinh.
        var chiConChu = sach
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", " ", StringComparison.OrdinalIgnoreCase);
        chiConChu = System.Text.RegularExpressions.Regex.Replace(chiConChu, "<.*?>", string.Empty);

        return string.IsNullOrWhiteSpace(chiConChu) ? string.Empty : sach;
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
