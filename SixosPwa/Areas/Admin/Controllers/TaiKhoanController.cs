using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class TaiKhoanController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _adminStoredProcedures;

    public TaiKhoanController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
    }

    public async Task<IActionResult> Index(string? q, string? role, int page = 1)
    {
        page = SafePage(page);
        var query = _db.TaiKhoans.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => x.SDT.Contains(q));
        }

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Role == "Admin");
        else if (string.Equals(role, "BenhNhan", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Role != "Admin");

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        return View(new TaiKhoanListViewModel
        {
            Items = items.Select(x =>
            {
                x.Role = NormalizeRole(x.Role);
                return x;
            }).ToList(),
            Query = q,
            Role = role,
            Page = page,
            PageSize = DefaultPageSize,
            TotalItems = total
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new TaiKhoanEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaiKhoanEditViewModel model)
    {
        model.SDT = model.SDT.Trim();
        model.Role = NormalizeRole(model.Role.Trim());
        ValidateRole(model.Role);

        if (ModelState.IsValid && await _db.TaiKhoans.AnyAsync(x => x.SDT == model.SDT))
            ModelState.AddModelError(nameof(model.SDT), "Số điện thoại đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        // MatKhauNoiBo de null — phan bam chua thi hanh (Dinh chinh ADR 0009).
        var (result, _) = await _adminStoredProcedures.SaveTaiKhoanAsync(
            0, model.SDT, null, model.Role, null, null);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.SDT), result.Message ?? "Không thể tạo tài khoản.");
            return View(model);
        }

        Success("Đã tạo tài khoản mới.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var entity = await _db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        return View(new TaiKhoanEditViewModel { Id = entity.Id, SDT = entity.SDT, Role = entity.Role });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TaiKhoanEditViewModel model)
    {
        model.Role = NormalizeRole(model.Role.Trim());
        ValidateRole(model.Role);

        var entity = await _db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        var currentPhone = User.Identity?.Name;
        if (entity.SDT == currentPhone && model.Role != "Admin")
            ModelState.AddModelError(nameof(model.Role), "Không thể tự hạ quyền tài khoản Admin đang đăng nhập.");

        if (NormalizeRole(entity.Role) == "Admin" && model.Role != "Admin"
            && await _db.TaiKhoans.CountAsync(x => x.Role == "Admin") <= 1)
            ModelState.AddModelError(nameof(model.Role), "Không thể hạ quyền Admin cuối cùng của hệ thống.");

        if (!ModelState.IsValid) return View(model);

        var (result, _) = await _adminStoredProcedures.SaveTaiKhoanAsync(
            model.Id,
            entity.SDT,
            entity.Email,
            NormalizeRole(model.Role),
            null,
            entity.IdBenhNhan);
        if (!result.Succeeded)
        {
            if (result.Code == 3) return NotFound();
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể cập nhật tài khoản.");
            return View(model);
        }

        Success("Đã cập nhật vai trò tài khoản.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateRole(string role)
    {
        if (!AllowedRoles.Contains(role))
            ModelState.AddModelError(nameof(TaiKhoanEditViewModel.Role), "Vai trò không hợp lệ.");
    }
}
