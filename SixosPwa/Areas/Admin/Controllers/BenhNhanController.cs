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
    public async Task<IActionResult> Index(string? q, string? maCoSo, int page = 1)
    {
        page = SafePage(page);
        var query = _db.BenhNhans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            // MaBN nay nam o DM_BenhNhanCoSo (ho so tai tung co so), khong con
            // tren bang con nguoi.
            q = q.Trim();
            query = query.Where(x => x.TenBN.Contains(q)
                || (x.SDT != null && x.SDT.Contains(q))
                || x.CCCD.Contains(q)
                || _db.BenhNhanCoSos.Any(h => h.IdBenhNhan == x.Id && h.MaBN.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(maCoSo))
        {
            var maCoSoLoc = maCoSo.Trim();
            query = query.Where(x => _db.BenhNhanCoSos.Any(h => h.IdBenhNhan == x.Id
                && _db.DMCSKCBs.Any(cs => cs.Id == h.IdCoSo && cs.MaCoSo == maCoSoLoc)));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.TenBN)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        var idBenhNhan = items.Select(x => x.Id).ToList();
        var hoSo = await (
            from h in _db.BenhNhanCoSos.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals cs.Id
            where idBenhNhan.Contains(h.IdBenhNhan)
            orderby h.Id
            select new { h.IdBenhNhan, h.MaBN, cs.MaCoSo }).ToListAsync();

        var maCoSoTheoBN = hoSo.GroupBy(x => x.IdBenhNhan)
            .ToDictionary(g => g.Key, g => g.First().MaCoSo);
        var maBNTheoBN = hoSo.GroupBy(x => x.IdBenhNhan)
            .ToDictionary(g => g.Key, g => g.First().MaBN);

        return View(new BenhNhanListViewModel
        {
            MaCoSoTheoBenhNhan = maCoSoTheoBN,
            MaBNTheoBenhNhan = maBNTheoBN,
            Items = items,
            Query = q,
            MaCoSo = maCoSo,
            Page = page,
            PageSize = DefaultPageSize,
            TotalItems = total
        });
    }
}
