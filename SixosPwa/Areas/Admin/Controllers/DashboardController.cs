using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class DashboardController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _adminStoredProcedures;
    private readonly IWebHostEnvironment _environment;

    private const string ContentImageFolder = "static/img_nd";
    private const long MaxContentImageSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };
    private static readonly HtmlSanitizer HtmlFilter = CreateHtmlFilter();

    public DashboardController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures,
        IWebHostEnvironment environment)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
        _environment = environment;
    }

    public async Task<IActionResult> Index(long? facilityId = null, long? topicId = null)
    {
        var nhomCSList = await _db.DMNhomCSs.AsNoTracking().ToListAsync();
        var chuDeList = await _db.DMChuDes.AsNoTracking().ToListAsync();
        var facilityList = await _db.DMCSKCBs.AsNoTracking().ToListAsync();
        var selectedFacility = facilityId.HasValue
            ? facilityList.FirstOrDefault(x => x.Id == facilityId.Value)
            : null;
        var selectedTopic = topicId.HasValue
            ? chuDeList.FirstOrDefault(x => x.ID == topicId.Value)
            : null;
        var selectedNhom = selectedFacility == null
            ? null
            : nhomCSList.FirstOrDefault(x =>
                string.Equals(x.LoaiCS, selectedFacility.LoaiCS, StringComparison.OrdinalIgnoreCase));
        var noiDung = selectedFacility != null && selectedTopic != null
            ? await _adminStoredProcedures.GetNoiDungCskcbAsync(
                selectedFacility.MaCoSo,
                selectedFacility.TenCoSo,
                selectedTopic.ID)
            : null;

        
        // Count of patient accounts by MaCoSo
        var facilityPatients = await _db.TaiKhoanDoiTacs
            .Join(_db.TaiKhoans, 
                  td => td.IdTaiKhoan, 
                  tk => tk.Id, 
                  (td, tk) => new { td.MaCoSo, tk.SDT, tk.Id, tk.CCCD })
            .ToListAsync();
            
        var patientsByFacility = facilityPatients
            .GroupBy(x => x.MaCoSo)
            .ToDictionary(
                g => g.Key, 
                g => g.Select(x => new PatientAccountStat { Id = x.Id, SDT = x.SDT ?? "", CCCD = x.CCCD ?? "" }).ToList()
            );

        var groups = nhomCSList
            .Where(x => new[] { "benhvien", "nhakhoa", "pkdk", "nhathuoc" }.Contains(x.LoaiCS?.ToLower()))
            .Select(n => new FacilityGroupStat
            {
                TenLoaiCS = n.TenLoaiCS ?? "",
                LoaiCS = n.LoaiCS ?? "",
                Facilities = facilityList
                    .Where(f => string.Equals(f.LoaiCS, n.LoaiCS, StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FacilityStat
                    {
                        Id = f.Id,
                        MaCoSo = f.MaCoSo ?? "",
                        TenCoSo = f.TenCoSo ?? "",
                        PatientCount = patientsByFacility.GetValueOrDefault(f.MaCoSo ?? "", new List<PatientAccountStat>()).Count,
                        PatientAccounts = patientsByFacility.GetValueOrDefault(f.MaCoSo ?? "", new List<PatientAccountStat>())
                    })
                    .ToList()
            })
            .ToList();

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
            NhomCSList = nhomCSList,
            ChuDeList = chuDeList,
            FacilityList = facilityList,
            SelectedNhomCSId = selectedNhom?.ID,
            SelectedFacilityId = selectedFacility?.Id,
            SelectedTopicId = selectedTopic?.ID,
            NoiDung = noiDung,
            FacilityGroupStats = groups
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveContent(DashboardContentEditViewModel model)
    {
        var redirectValues = new { facilityId = model.FacilityId, topicId = model.TopicId };
        if (!ModelState.IsValid)
        {
            Error("Vui lòng chọn cơ sở y tế và chủ đề trước khi lưu.");
            return RedirectToAction(nameof(Index), redirectValues);
        }

        var facility = await _db.DMCSKCBs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.FacilityId);
        var topic = await _db.DMChuDes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == model.TopicId);
        if (facility == null || topic == null)
        {
            Error("Cơ sở y tế hoặc chủ đề không hợp lệ.");
            return RedirectToAction(nameof(Index), redirectValues);
        }

        var result = await _adminStoredProcedures.SaveNoiDungCskcbAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            topic.ID,
            SanitizeHtml(model.NoiDung));
        if (!result.Succeeded)
        {
            Error(result.Message ?? "Không thể lưu nội dung.");
            return RedirectToAction(nameof(Index), redirectValues);
        }

        Success("Đã lưu nội dung HTML cho cơ sở y tế.");
        return RedirectToAction(nameof(Index), redirectValues);
    }

    [HttpGet]
    public async Task<IActionResult> GetContent(long facilityId, long topicId)
    {
        var facility = await _db.DMCSKCBs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == facilityId);
        var topic = await _db.DMChuDes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ID == topicId);
        if (facility == null || topic == null)
            return NotFound();

        var noiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            topic.ID);
        return Json(new { noiDung = noiDung ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return Json(new { error = "Chưa chọn ảnh." });

        if (file.Length > MaxContentImageSize)
            return Json(new { error = "Ảnh phải nhỏ hơn 5 MB." });

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedContentImageExtensions.Contains(extension))
            return Json(new { error = "Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF." });

        var directory = Path.Combine(
            _environment.WebRootPath,
            ContentImageFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(directory, fileName);
        await using var stream = new FileStream(path, FileMode.CreateNew);
        await file.CopyToAsync(stream);

        return Json(new { location = $"/{ContentImageFolder}/{fileName}" });
    }

    private static HtmlSanitizer CreateHtmlFilter()
    {
        var filter = new HtmlSanitizer();
        filter.AllowedTags.Clear();
        foreach (var tag in new[]
                 { "p", "br", "b", "strong", "i", "em", "u", "s", "sub", "sup", "ul", "ol", "li",
                   "h1", "h2", "h3", "h4", "blockquote", "hr", "table", "thead", "tbody", "tfoot",
                   "tr", "th", "td", "img", "a", "span", "div" })
            filter.AllowedTags.Add(tag);

        filter.AllowedAttributes.Clear();
        foreach (var attribute in new[]
                 { "style", "class", "src", "alt", "href", "title", "width", "height", "colspan",
                   "rowspan", "target", "rel" })
            filter.AllowedAttributes.Add(attribute);

        filter.AllowedSchemes.Clear();
        filter.AllowedSchemes.Add("http");
        filter.AllowedSchemes.Add("https");
        filter.AllowedSchemes.Add("mailto");
        return filter;
    }

    private static string? SanitizeHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var sanitized = HtmlFilter.Sanitize(html).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
