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

    public DashboardController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
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
                selectedTopic.LoaiND ?? string.Empty)
            : null;

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
            NoiDung = noiDung
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
        if (facility == null || topic == null || string.IsNullOrWhiteSpace(topic.LoaiND))
        {
            Error("Cơ sở y tế hoặc chủ đề không hợp lệ.");
            return RedirectToAction(nameof(Index), redirectValues);
        }

        var result = await _adminStoredProcedures.SaveNoiDungCskcbAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            topic.LoaiND,
            model.NoiDung);
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
        if (facility == null || topic == null || string.IsNullOrWhiteSpace(topic.LoaiND))
            return NotFound();

        var noiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            topic.LoaiND);
        return Json(new { noiDung = noiDung ?? string.Empty });
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
