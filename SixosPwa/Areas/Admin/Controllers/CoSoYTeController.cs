using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class CoSoYTeController : AdminControllerBase
{
    private static readonly string[] AllowedTypes = { "benhvien", "pkdk", "nhakhoa", "phongmach", "nhathuoc" };
    private readonly ApplicationDbContext _db;

    public CoSoYTeController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, string? loaiCS, int page = 1)
    {
        page = SafePage(page);
        var query = _db.DMCSKCBs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => (x.MaCoSo != null && x.MaCoSo.Contains(q))
                || (x.TenCoSo != null && x.TenCoSo.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(loaiCS) && AllowedTypes.Contains(loaiCS))
            query = query.Where(x => x.LoaiCS == loaiCS);

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.TenCoSo)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        return View(new CoSoYTeListViewModel
        {
            Items = items,
            Query = q,
            LoaiCS = loaiCS,
            Page = page,
            PageSize = DefaultPageSize,
            TotalItems = total
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new CoSoYTeEditViewModel { XacMinh = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CoSoYTeEditViewModel model)
    {
        Normalize(model);
        ValidateType(model.LoaiCS);
        ValidateImageUrl(model.Img);
        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && await _db.DMCSKCBs.AnyAsync(x => x.MaCoSo == model.MaCoSo))
            ModelState.AddModelError(nameof(model.MaCoSo), "Mã cơ sở đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        _db.DMCSKCBs.Add(ToEntity(model));
        await _db.SaveChangesAsync();
        Success("Đã thêm cơ sở y tế.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var entity = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        return View(ToViewModel(entity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CoSoYTeEditViewModel model)
    {
        Normalize(model);
        ValidateType(model.LoaiCS);
        ValidateImageUrl(model.Img);
        var entity = await _db.DMCSKCBs.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && await _db.DMCSKCBs.AnyAsync(x => x.Id != model.Id && x.MaCoSo == model.MaCoSo))
            ModelState.AddModelError(nameof(model.MaCoSo), "Mã cơ sở đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        entity.MaCoSo = model.MaCoSo;
        entity.TenCoSo = model.TenCoSo;
        entity.DiaChi = model.DiaChi;
        entity.LoaiCS = model.LoaiCS;
        entity.TGLamViec = model.TGLamViec;
        entity.XacMinh = model.XacMinh ? 1 : 0;
        entity.Img = model.Img;
        entity.QuangCao = model.QuangCao;
        await _db.SaveChangesAsync();
        Success("Đã cập nhật cơ sở y tế.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateType(string? type)
    {
        if (!string.IsNullOrWhiteSpace(type) && !AllowedTypes.Contains(type))
            ModelState.AddModelError(nameof(CoSoYTeEditViewModel.LoaiCS), "Loại cơ sở không hợp lệ.");
    }

    private void ValidateImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            ModelState.AddModelError(nameof(CoSoYTeEditViewModel.Img), "Chỉ chấp nhận URL http hoặc https.");
    }

    private static void Normalize(CoSoYTeEditViewModel model)
    {
        model.MaCoSo = model.MaCoSo?.Trim();
        model.TenCoSo = model.TenCoSo?.Trim();
        model.DiaChi = model.DiaChi?.Trim();
        model.LoaiCS = model.LoaiCS?.Trim().ToLowerInvariant();
        model.TGLamViec = model.TGLamViec?.Trim();
        model.Img = model.Img?.Trim();
    }

    private static DMCSKCB ToEntity(CoSoYTeEditViewModel model) => new()
    {
        MaCoSo = model.MaCoSo,
        TenCoSo = model.TenCoSo,
        DiaChi = model.DiaChi,
        LoaiCS = model.LoaiCS,
        TGLamViec = model.TGLamViec,
        XacMinh = model.XacMinh ? 1 : 0,
        Img = model.Img,
        QuangCao = model.QuangCao
    };

    private static CoSoYTeEditViewModel ToViewModel(DMCSKCB entity) => new()
    {
        Id = entity.Id,
        MaCoSo = entity.MaCoSo,
        TenCoSo = entity.TenCoSo,
        DiaChi = entity.DiaChi,
        LoaiCS = entity.LoaiCS,
        TGLamViec = entity.TGLamViec,
        XacMinh = entity.XacMinh == 1,
        Img = entity.Img,
        QuangCao = entity.QuangCao
    };
}
