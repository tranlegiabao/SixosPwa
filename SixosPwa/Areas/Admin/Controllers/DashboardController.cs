using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class DashboardController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
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
            RecentFacilities = await _db.DMCSKCBs.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Take(4)
                .ToListAsync()
        };

        return View(model);
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
