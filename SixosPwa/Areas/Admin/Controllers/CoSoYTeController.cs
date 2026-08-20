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
    private readonly IWebHostEnvironment _environment;

    public CoSoYTeController(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

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
        if (model.ImageFile != null)
            model.Img = await SaveImageAsync(model.ImageFile, "static/img_cs", "/static/img_cs", nameof(model.ImageFile));
        if (model.LogoFile != null)
            model.Logo = await SaveImageAsync(model.LogoFile, "static/logo_cs", "/static/logo_cs", nameof(model.LogoFile));
        Normalize(model);
        ValidateType(model.LoaiCS);
        ValidateAdvertisingAmount(model.QuangCao);
        ValidateImageUrl(model.Img, nameof(model.Img), "/static/img_cs/", "/uploads/co-so-y-te/");
        ValidateImageUrl(model.Logo, nameof(model.Logo), "/static/logo_cs/", "/uploads/co-so-y-te/logo/");
        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && await _db.DMCSKCBs.AnyAsync(x => x.MaCoSo == model.MaCoSo))
            ModelState.AddModelError(nameof(model.MaCoSo), "Mã cơ sở đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        var entity = ToEntity(model);
        _db.DMCSKCBs.Add(entity);
        UpdateNoiDung(entity, model, Array.Empty<NDCSKCB>());
        await _db.SaveChangesAsync();
        Success("Đã thêm cơ sở y tế.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var entity = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        var noiDung = await FindNoiDungAsync(entity.MaCoSo, entity.TenCoSo);
        return View(ToViewModel(entity, noiDung));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CoSoYTeEditViewModel model)
    {
        if (model.ImageFile != null)
            model.Img = await SaveImageAsync(model.ImageFile, "static/img_cs", "/static/img_cs", nameof(model.ImageFile));
        if (model.LogoFile != null)
            model.Logo = await SaveImageAsync(model.LogoFile, "static/logo_cs", "/static/logo_cs", nameof(model.LogoFile));
        Normalize(model);
        ValidateType(model.LoaiCS);
        ValidateAdvertisingAmount(model.QuangCao);
        ValidateImageUrl(model.Img, nameof(model.Img), "/static/img_cs/", "/uploads/co-so-y-te/");
        ValidateImageUrl(model.Logo, nameof(model.Logo), "/static/logo_cs/", "/uploads/co-so-y-te/logo/");
        var entity = await _db.DMCSKCBs.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();
        var noiDung = await FindNoiDungAsync(entity.MaCoSo, entity.TenCoSo);

        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && await _db.DMCSKCBs.AnyAsync(x => x.Id != model.Id && x.MaCoSo == model.MaCoSo))
            ModelState.AddModelError(nameof(model.MaCoSo), "Mã cơ sở đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        entity.MaCoSo = model.MaCoSo;
        entity.TenCoSo = model.TenCoSo;
        entity.DiaChi = model.DiaChi;
        entity.Tinh = model.Tinh;
        entity.Huyen = model.Huyen;
        entity.LoaiCS = model.LoaiCS;
        entity.TGLamViec = model.TGLamViec;
        entity.XacMinh = model.XacMinh ? 1 : 0;
        entity.Img = model.Img;
        entity.logo = model.Logo;
        entity.QuangCao = model.QuangCao;
        UpdateNoiDung(entity, model, noiDung);
        await _db.SaveChangesAsync();
        Success("Đã cập nhật cơ sở y tế.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateType(string? type)
    {
        if (!string.IsNullOrWhiteSpace(type) && !AllowedTypes.Contains(type))
            ModelState.AddModelError(nameof(CoSoYTeEditViewModel.LoaiCS), "Loại cơ sở không hợp lệ.");
    }

    private void ValidateImageUrl(string? imageUrl, string propertyName, params string[] localPrefixes)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        if (localPrefixes.Any(prefix => imageUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            ModelState.AddModelError(propertyName, "Chỉ chấp nhận URL http hoặc https.");
    }

    private void ValidateAdvertisingAmount(decimal? amount)
    {
        if (amount.HasValue && amount.Value != decimal.Truncate(amount.Value))
            ModelState.AddModelError(nameof(CoSoYTeEditViewModel.QuangCao), "Số tiền quảng cáo phải là số nguyên VNĐ.");
    }

    private async Task<string?> SaveImageAsync(
        IFormFile imageFile,
        string storageFolder,
        string publicPrefix,
        string propertyName)
    {
        const long maxFileSize = 5 * 1024 * 1024;
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        if (imageFile.Length == 0 || imageFile.Length > maxFileSize)
        {
            ModelState.AddModelError(propertyName, "Ảnh phải có dung lượng từ 1 byte đến 5 MB.");
            return null;
        }

        var extension = Path.GetExtension(imageFile.FileName);
        if (!allowedExtensions.Contains(extension))
        {
            ModelState.AddModelError(propertyName, "Chỉ hỗ trợ ảnh JPG, PNG, WEBP hoặc GIF.");
            return null;
        }

        var uploadDirectory = Path.Combine(_environment.WebRootPath, storageFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(uploadDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadDirectory, fileName);

        await using var stream = new FileStream(filePath, FileMode.CreateNew);
        await imageFile.CopyToAsync(stream);
        return $"{publicPrefix.TrimEnd('/')}/{fileName}";
    }

    private static void Normalize(CoSoYTeEditViewModel model)
    {
        model.MaCoSo = model.MaCoSo?.Trim();
        model.TenCoSo = model.TenCoSo?.Trim();
        model.DiaChi = model.DiaChi?.Trim();
        model.LoaiCS = model.LoaiCS?.Trim().ToLowerInvariant();
        model.TGLamViec = model.TGLamViec?.Trim();
        model.Img = model.Img?.Trim();
        model.Logo = model.Logo?.Trim();
    }

    private static DMCSKCB ToEntity(CoSoYTeEditViewModel model) => new()
    {
        MaCoSo = model.MaCoSo,
        TenCoSo = model.TenCoSo,
        DiaChi = model.DiaChi,
        Tinh = model.Tinh,
        Huyen = model.Huyen,
        LoaiCS = model.LoaiCS,
        TGLamViec = model.TGLamViec,
        XacMinh = model.XacMinh ? 1 : 0,
        Img = model.Img,
        logo = model.Logo,
        QuangCao = model.QuangCao
    };

    private async Task<List<NDCSKCB>> FindNoiDungAsync(string? maCoSo, string? tenCoSo)
    {
        if (!string.IsNullOrWhiteSpace(maCoSo))
        {
            return await _db.NDCSKCBs
                .Where(x => x.MaCoSo == maCoSo)
                .ToListAsync();
        }

        return await _db.NDCSKCBs
            .Where(x => (x.MaCoSo == null || x.MaCoSo == "") && x.TenCoSo == tenCoSo)
            .ToListAsync();
    }

    private void UpdateNoiDung(
        DMCSKCB coSo,
        CoSoYTeEditViewModel model,
        IReadOnlyCollection<NDCSKCB> existingItems)
    {
        var values = new (string LoaiND, string? NoiDung)[]
        {
            (NDCSKCB.GioiThieu, model.NoiDungGioiThieu),
            (NDCSKCB.DichVu, model.NoiDungDichVu),
            (NDCSKCB.DoiNgu, model.NoiDungDoiNgu),
            (NDCSKCB.TrangThietBi, model.NoiDungTrangThietBi),
            (NDCSKCB.LienHe, model.NoiDungLienHe)
        };

        foreach (var (loaiND, noiDung) in values)
        {
            var rows = existingItems.Where(x => x.LoaiND == loaiND).ToList();
            if (string.IsNullOrWhiteSpace(noiDung))
            {
                if (rows.Count > 0) _db.NDCSKCBs.RemoveRange(rows);
                continue;
            }

            var row = rows.FirstOrDefault();
            if (row == null)
            {
                _db.NDCSKCBs.Add(new NDCSKCB
                {
                    MaCoSo = coSo.MaCoSo,
                    TenCoSo = coSo.TenCoSo,
                    LoaiND = loaiND,
                    NoiDung = noiDung.Trim()
                });
            }
            else
            {
                row.MaCoSo = coSo.MaCoSo;
                row.TenCoSo = coSo.TenCoSo;
                row.NoiDung = noiDung.Trim();
                _db.NDCSKCBs.RemoveRange(rows.Skip(1));
            }
        }
    }

    private static CoSoYTeEditViewModel ToViewModel(
        DMCSKCB entity,
        IReadOnlyCollection<NDCSKCB> noiDung) => new()
    {
        Id = entity.Id,
        MaCoSo = entity.MaCoSo,
        TenCoSo = entity.TenCoSo,
        DiaChi = entity.DiaChi,
        Tinh = entity.Tinh,
        Huyen = entity.Huyen,
        LoaiCS = entity.LoaiCS,
        TGLamViec = entity.TGLamViec,
        XacMinh = entity.XacMinh == 1,
        Img = entity.Img,
        Logo = entity.logo,
        QuangCao = entity.QuangCao,
        NoiDungGioiThieu = GetNoiDung(noiDung, NDCSKCB.GioiThieu),
        NoiDungDichVu = GetNoiDung(noiDung, NDCSKCB.DichVu),
        NoiDungDoiNgu = GetNoiDung(noiDung, NDCSKCB.DoiNgu),
        NoiDungTrangThietBi = GetNoiDung(noiDung, NDCSKCB.TrangThietBi),
        NoiDungLienHe = GetNoiDung(noiDung, NDCSKCB.LienHe)
    };

    private static string? GetNoiDung(IEnumerable<NDCSKCB> noiDung, string loaiND) =>
        noiDung.FirstOrDefault(x => x.LoaiND == loaiND)?.NoiDung;
}
