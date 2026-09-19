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
    public async Task<IActionResult> Index(string? q, string? maCoSo, int page = 1, int pageSize = 50)
    {
        page = SafePage(page);
        pageSize = pageSize is 20 or 50 or 100 or 500 ? pageSize : 50;

        var query = _db.BenhNhans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            // MaBN nay nam o DM_BenhNhanCoSo (ho so tai tung co so), khong con
            // tren bang con nguoi.
            q = q.Trim();
            query = query.Where(x => x.TenBN.Contains(q)
                || (x.SDT != null && x.SDT.Contains(q))
                || x.CCCD.Contains(q)
                || (x.MaBN != null && x.MaBN.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(maCoSo))
        {
            var maCoSoLoc = maCoSo.Trim();
            query = query.Where(x => _db.DMCSKCBs.Any(cs => cs.Id == x.IdCoSo && cs.MaCoSo == maCoSoLoc));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.TenBN)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var idBenhNhan = items.Select(x => x.Id).ToList();
        // Dot 1B: mot dong DA LA ho so tai co so nen khong con tu noi.
        var hoSo = await (
            from h in _db.BenhNhans.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals (long?)cs.Id
            where idBenhNhan.Contains(h.Id)
            orderby h.Id
            select new { IdBenhNhan = h.Id, h.MaBN, cs.MaCoSo }).ToListAsync();

        var maCoSoTheoBN = hoSo.GroupBy(x => x.IdBenhNhan)
            .ToDictionary(g => g.Key, g => g.First().MaCoSo);
        var maBNTheoBN = hoSo.GroupBy(x => x.IdBenhNhan)
            .ToDictionary(g => g.Key, g => g.First().MaBN);

        var model = new BenhNhanListViewModel
        {
            MaCoSoTheoBenhNhan = maCoSoTheoBN,
            MaBNTheoBenhNhan = maBNTheoBN,
            Items = items,
            Query = q,
            MaCoSo = maCoSo,
            Page = page,
            PageSize = pageSize,
            TotalItems = total
        };

        if (IsAjaxRequest())
        {
            Response.Headers["X-Total-Pages"] = model.TotalPages.ToString();
            Response.Headers["X-Current-Page"] = model.Page.ToString();
            Response.Headers["X-Total-Items"] = model.TotalItems.ToString();
            Response.Headers["X-Page-Size"] = model.PageSize.ToString();
            return PartialView("_BenhNhanTableBody", model);
        }

        return View(model);
    }

    [HttpGet]
    public Task<IActionResult> TaiTrang(string? q, string? maCoSo, int page = 1, int pageSize = 50)
    {
        return Index(q, maCoSo, page, pageSize);
    }

    private bool IsAjaxRequest() =>
        Request.Headers["X-Requested-With"] == "XMLHttpRequest"
        || Request.Query.ContainsKey("ajax");
}
