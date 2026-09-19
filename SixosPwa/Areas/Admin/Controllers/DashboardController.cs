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
    private readonly IFtpService _ftp;
    private readonly IDonAnhService _donAnh;

    private const long MaxContentImageSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };
    private static readonly HtmlSanitizer HtmlFilter = CreateHtmlFilter();

    public DashboardController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures,
        IFtpService ftp,
        IDonAnhService donAnh)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
        _ftp = ftp;
        _donAnh = donAnh;
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
        var selectedNhom = selectedFacility?.IdNhomCS is null
            ? null
            : nhomCSList.FirstOrDefault(x => x.ID == selectedFacility.IdNhomCS);
        var noiDung = selectedFacility != null && selectedTopic != null
            ? await _adminStoredProcedures.GetNoiDungCskcbAsync(selectedFacility.Id, selectedTopic.ID)
            : null;


        // Đếm HỒ SƠ bệnh nhân theo MaCoSo.
        //
        // 🔴 Đợt A đổi hẳn trục đi: bảng `HT_TaiKhoanDoiTac` bị xóa và cột
        // `HT_TaiKhoan.IDBenhNhan` cũng bị xóa (thi hành nốt ADR 0019), nên bản cũ
        // (tài khoản-đối tác → tài khoản → hồ sơ, 1–1) không còn đường nào chạy.
        // Trục đúng bây giờ: DM_BenhNhanCoSo (hồ sơ TẠI một cơ sở) → DM_BenhNhan →
        // HT_TaiKhoan qua `DM_BenhNhan.IdTaiKhoan`, quan hệ 1–N.
        //
        // Hệ quả CỐ Ý: một tài khoản quản nhiều hồ sơ thì đếm NHIỀU dòng, vì con số
        // trên dashboard là "bao nhiêu hồ sơ bệnh nhân thuộc cơ sở này" — đúng thứ
        // cơ sở quan tâm — chứ không phải "bao nhiêu người đăng nhập". Giữ join 1–1
        // cũ thì mỗi tài khoản chỉ đếm được một hồ sơ, các hồ sơ còn lại BIẾN MẤT.
        //
        // Tài khoản có thể NULL (hồ sơ cơ sở tự khai, chưa ai nhận) ⇒ LEFT JOIN, và
        // khi đó SDT lấy từ chính hồ sơ.
        // Dot 1B: mot dong = con nguoi + ho so tai co so, va benh nhan khong con
        // tai khoan => SDT lay thang tu chinh dong do.
        var facilityPatients = await (
            from h in _db.BenhNhans.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals (long?)cs.Id
            select new
            {
                MaCoSo = cs.MaCoSo,
                SDT = h.SDT,
                // Dot 1B: khong con tai khoan benh nhan; ID cua ho so chinh la danh tinh.
                Id = (long?)h.Id,
                CCCD = h.CCCD
            })
            .ToListAsync();

        var patientsByFacility = facilityPatients
            .GroupBy(x => x.MaCoSo)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new PatientAccountStat { Id = x.Id, SDT = x.SDT ?? "", CCCD = x.CCCD ?? "" }).ToList()
            );

        var groups = nhomCSList
            .Where(x => new[] { "benhvien", "nhakhoa", "pkdk", "nhathuoc" }.Contains(x.MaNhom.ToLower()))
            .Select(n => new FacilityGroupStat
            {
                TenLoaiCS = n.TenNhom,
                LoaiCS = n.MaNhom,
                Facilities = facilityList
                    .Where(f => f.IdNhomCS == n.ID)
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
            // Dot 1B: benh nhan khong con tai khoan (ADR 0034) => luon 0.
            PatientAccountCount = 0,
            PatientCount = await _db.BenhNhans.CountAsync(),
            FacilityCount = await _db.DMCSKCBs.CountAsync(),
            VisibleFacilityCount = await _db.DMCSKCBs.CountAsync(x => x.HienThiCongKhai),
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

        // Ban HTML truoc khi sua — doc TRUOC khi luu, neu khong thi khong con
        // cach nao biet admin vua go bo the <img> nao.
        var noiDungCu = await _adminStoredProcedures.GetNoiDungCskcbAsync(facility.Id, topic.ID);
        var noiDungMoi = SanitizeHtml(model.NoiDung);

        var result = await _adminStoredProcedures.SaveNoiDungCskcbAsync(
            facility.Id,
            topic.ID,
            noiDungMoi);
        if (!result.Succeeded)
        {
            Error(result.Message ?? "Không thể lưu nội dung.");
            return RedirectToAction(nameof(Index), redirectValues);
        }

        // Luu xong roi moi don: DB da giu ban MOI nen phep do cheo trong
        // DonAnhService khong con thay anh vua bi go.
        await _donAnh.DonTheoHtmlAsync(noiDungCu, noiDungMoi);

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

        var noiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(facility.Id, topic.ID);
        return Json(new { noiDung = noiDung ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile? file, [FromQuery] string? maCoSo = null, [FromForm] string? maCoSoForm = null)
    {
        if (file == null || file.Length == 0)
            return Json(new { error = "Chưa chọn ảnh." });

        if (file.Length > MaxContentImageSize)
            return Json(new { error = "Ảnh phải nhỏ hơn 5 MB." });

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedContentImageExtensions.Contains(extension))
            return Json(new { error = "Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF." });

        var maCS = !string.IsNullOrWhiteSpace(maCoSo) ? maCoSo : maCoSoForm;

        try
        {
            var duongDanFtp = await _ftp.UploadFileAsync(file, KhoAnh.ThuMucFtp(maCS, KhoAnh.ThuMucNoiDung));
            var url = KhoAnh.UrlTuDuongDanFtp(duongDanFtp);
            if (url == null) return Json(new { error = "Không tải được ảnh lên máy chủ FTP." });

            return Json(new { location = url });
        }
        catch (Exception)
        {
            return Json(new { error = "Không tải được ảnh lên máy chủ FTP." });
        }
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
