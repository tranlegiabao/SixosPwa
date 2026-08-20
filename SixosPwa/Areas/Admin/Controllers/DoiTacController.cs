using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class DoiTacController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public DoiTacController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        page = SafePage(page);
        var query = _db.DoiTacs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => x.MaDT.Contains(q) || x.TenDT.Contains(q)
                || (x.SDT != null && x.SDT.Contains(q)));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.TenDT)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        return View(new DoiTacListViewModel
        {
            Items = items,
            Query = q,
            Page = page,
            PageSize = DefaultPageSize,
            TotalItems = total
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new DoiTacEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DoiTacEditViewModel model)
    {
        Normalize(model);
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Vui lòng nhập mật khẩu đối tác.");
        if (ModelState.IsValid && await _db.DoiTacs.AnyAsync(x => x.MaDT == model.MaDT))
            ModelState.AddModelError(nameof(model.MaDT), "Mã đối tác đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        _db.DoiTacs.Add(ToEntity(model));
        await _db.SaveChangesAsync();
        Success("Đã thêm đối tác.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var entity = await _db.DoiTacs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        return View(ToViewModel(entity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DoiTacEditViewModel model)
    {
        Normalize(model);
        var entity = await _db.DoiTacs.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        if (ModelState.IsValid && await _db.DoiTacs.AnyAsync(x => x.Id != model.Id && x.MaDT == model.MaDT))
            ModelState.AddModelError(nameof(model.MaDT), "Mã đối tác đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        entity.MaDT = model.MaDT;
        entity.TenDT = model.TenDT;
        entity.DiaChi = model.DiaChi;
        entity.SDT = model.SDT;
        entity.Email = model.Email;
        entity.BrandName = model.BrandName;
        if (!string.IsNullOrWhiteSpace(model.Password))
            entity.Password = model.Password;
        await _db.SaveChangesAsync();
        Success("Đã cập nhật thông tin đối tác.");
        return RedirectToAction(nameof(Index));
    }

    private static void Normalize(DoiTacEditViewModel model)
    {
        model.MaDT = model.MaDT.Trim();
        model.TenDT = model.TenDT.Trim();
        model.DiaChi = model.DiaChi?.Trim();
        model.SDT = model.SDT?.Trim();
        model.Email = model.Email?.Trim();
        model.BrandName = model.BrandName?.Trim();
        model.Password = model.Password?.Trim();
    }

    private static DoiTac ToEntity(DoiTacEditViewModel model) => new()
    {
        MaDT = model.MaDT,
        TenDT = model.TenDT,
        DiaChi = model.DiaChi,
        SDT = model.SDT,
        Email = model.Email,
        BrandName = model.BrandName,
        Password = model.Password
    };

    private static DoiTacEditViewModel ToViewModel(DoiTac entity) => new()
    {
        Id = entity.Id,
        MaDT = entity.MaDT,
        TenDT = entity.TenDT,
        DiaChi = entity.DiaChi,
        SDT = entity.SDT,
        Email = entity.Email,
        BrandName = entity.BrandName
    };
}
