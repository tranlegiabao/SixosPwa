using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class TaiKhoanController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public TaiKhoanController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, string? role, int page = 1)
    {
        page = SafePage(page);
        var query = _db.TaiKhoans.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => x.SDT.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(role) && AllowedRoles.Contains(role))
            query = query.Where(x => x.Role == role);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.Id)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        return View(new TaiKhoanListViewModel
        {
            Items = items,
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
        model.Role = model.Role.Trim();
        ValidateRole(model.Role);

        if (ModelState.IsValid && await _db.TaiKhoans.AnyAsync(x => x.SDT == model.SDT))
            ModelState.AddModelError(nameof(model.SDT), "Số điện thoại đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        _db.TaiKhoans.Add(new TaiKhoan { SDT = model.SDT, Role = model.Role });
        await _db.SaveChangesAsync();
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
        model.Role = model.Role.Trim();
        ValidateRole(model.Role);

        var entity = await _db.TaiKhoans.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        var currentPhone = User.Identity?.Name;
        if (entity.SDT == currentPhone && model.Role != "Admin")
            ModelState.AddModelError(nameof(model.Role), "Không thể tự hạ quyền tài khoản Admin đang đăng nhập.");

        if (entity.Role == "Admin" && model.Role != "Admin"
            && await _db.TaiKhoans.CountAsync(x => x.Role == "Admin") <= 1)
            ModelState.AddModelError(nameof(model.Role), "Không thể hạ quyền Admin cuối cùng của hệ thống.");

        if (!ModelState.IsValid) return View(model);

        entity.Role = model.Role;
        await _db.SaveChangesAsync();
        Success("Đã cập nhật vai trò tài khoản.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateRole(string role)
    {
        if (!AllowedRoles.Contains(role))
            ModelState.AddModelError(nameof(TaiKhoanEditViewModel.Role), "Vai trò không hợp lệ.");
    }
}
