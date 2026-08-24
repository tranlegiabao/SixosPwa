using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class BenhNhanController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;

    public BenhNhanController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? maDT, int page = 1)
    {
        page = SafePage(page);
        var query = _db.BenhNhans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(x => x.MaBN.Contains(q) || x.TenBN.Contains(q)
                || (x.SDT != null && x.SDT.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(maDT))
            query = query.Where(x => x.MaDT == maDT.Trim());

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.TenBN)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        return View(new BenhNhanListViewModel
        {
            Items = items,
            Query = q,
            MaDT = maDT,
            Page = page,
            PageSize = DefaultPageSize,
            TotalItems = total
        });
    }
}
