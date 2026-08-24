using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class CoSoYTeController : AdminControllerBase
{
    private static readonly string[] AllowedTypes = { "benhvien", "pkdk", "nhakhoa", "phongmach", "nhathuoc" };
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly AdminStoredProcedureService _adminStoredProcedures;

    public CoSoYTeController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        AdminStoredProcedureService adminStoredProcedures)
    {
        _db = db;
        _environment = environment;
        _adminStoredProcedures = adminStoredProcedures;
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
    public async Task<IActionResult> Create()
    {
        var model = new CoSoYTeEditViewModel { XacMinh = true };
        await PopulateContentEditorAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CoSoYTeEditViewModel model)
    {
        Normalize(model);
        if (model.ImageFile != null)
            model.Img = await SaveImageAsync(model.ImageFile, "static/img_cs", "/static/img_cs", nameof(model.ImageFile));
        model.Logo = await ResolveImageAsync(
            model.LogoFile,
            model.LogoUrlInput,
            model.Logo,
            "static/logo_cs",
            "/static/logo_cs",
            nameof(model.LogoFile));
        var advertisingImageUrl = await ResolveAdvertisingImageAsync(model, null);
        model.QuangCaoImg = advertisingImageUrl;
        ApplyOperatingHours(model);
        ValidateType(model.LoaiCS);
        ValidateAdvertisingAmount(model.QuangCao);
        ValidateImageUrl(model.Img, nameof(model.Img), "/static/img_cs/", "/uploads/co-so-y-te/");
        ValidateImageUrl(model.Logo, nameof(model.Logo), "/static/logo_cs/", "/uploads/co-so-y-te/logo/");
        ValidateImageUrl(advertisingImageUrl, nameof(model.QuangCaoImgUrlInput), "/static/img_qc_kcb/");
        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && await _db.DMCSKCBs.AnyAsync(x => x.MaCoSo == model.MaCoSo))
            ModelState.AddModelError(nameof(model.MaCoSo), "Mã cơ sở đã tồn tại.");

        if (!ModelState.IsValid)
        {
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var result = await _adminStoredProcedures.SaveCoSoYTeAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.MaCoSo), result.Message ?? "Không thể thêm cơ sở y tế.");
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var createdFacilityForAdvertising = await _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.TenCoSo == model.TenCoSo
                && (string.IsNullOrWhiteSpace(model.MaCoSo) || x.MaCoSo == model.MaCoSo))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        if (createdFacilityForAdvertising != null)
        {
            var advertisingResult = await SaveAdvertisingAsync(
                createdFacilityForAdvertising,
                model,
                advertisingImageUrl);
            if (!advertisingResult.Succeeded)
            {
                Error(advertisingResult.Message ?? "Khong the luu quang cao.");
                return RedirectToAction(nameof(Edit), new { id = createdFacilityForAdvertising.Id, topicId = model.TopicId });
            }
        }

        if (model.TopicId > 0)
        {
            var createdFacility = await _db.DMCSKCBs.AsNoTracking()
                .Where(x => x.TenCoSo == model.TenCoSo
                    && (string.IsNullOrWhiteSpace(model.MaCoSo) || x.MaCoSo == model.MaCoSo))
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (createdFacility != null)
            {
                var topicContents = ParseTopicContents(model.TopicContentsJson);
                if (!topicContents.ContainsKey(model.TopicId))
                    topicContents[model.TopicId] = model.NoiDung;

                foreach (var topicContent in topicContents)
                {
                var contentResult = await SaveContentAsync(createdFacility, topicContent.Key, topicContent.Value);
                if (!contentResult.Succeeded)
                {
                    Error(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                    return RedirectToAction(nameof(Edit), new { id = createdFacility.Id, topicId = model.TopicId });
                }
            }
            }
        }

        Success("Đã thêm cơ sở y tế.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, long? topicId = null, string? section = null)
    {
        var entity = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        var model = ToViewModel(entity);
        model.ActiveSection = section;
        var advertising = await GetAdvertisingAsync(entity.MaCoSo, entity.TenCoSo);
        model.NoiDungQuangCao = advertising?.NoiDung;
        model.QuangCaoImg = advertising?.Img;
        await PopulateContentEditorAsync(model, topicId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CoSoYTeEditViewModel model)
    {
        var entity = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        var existingAdvertising = await GetAdvertisingAsync(entity.MaCoSo, entity.TenCoSo);
        Normalize(model);
        if (model.ImageFile != null)
            model.Img = await SaveImageAsync(model.ImageFile, "static/img_cs", "/static/img_cs", nameof(model.ImageFile));
        else
            model.Img = entity.Img;
        model.Logo = await ResolveImageAsync(
            model.LogoFile,
            model.LogoUrlInput,
            entity.logo,
            "static/logo_cs",
            "/static/logo_cs",
            nameof(model.LogoFile));
        var advertisingImageUrl = await ResolveAdvertisingImageAsync(model, existingAdvertising?.Img);
        model.QuangCaoImg = advertisingImageUrl;
        ApplyOperatingHours(model);
        ValidateType(model.LoaiCS);
        ValidateAdvertisingAmount(model.QuangCao);
        ValidateImageUrl(model.Img, nameof(model.Img), "/static/img_cs/", "/uploads/co-so-y-te/");
        ValidateImageUrl(model.Logo, nameof(model.Logo), "/static/logo_cs/", "/uploads/co-so-y-te/logo/");
        ValidateImageUrl(advertisingImageUrl, nameof(model.QuangCaoImgUrlInput), "/static/img_qc_kcb/");

        if (ModelState.IsValid && !string.IsNullOrWhiteSpace(model.MaCoSo)
            && await _db.DMCSKCBs.AnyAsync(x => x.Id != model.Id && x.MaCoSo == model.MaCoSo))
            ModelState.AddModelError(nameof(model.MaCoSo), "Mã cơ sở đã tồn tại.");

        if (!ModelState.IsValid)
        {
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var result = await _adminStoredProcedures.SaveCoSoYTeAsync(
            model,
            entity.MaCoSo,
            entity.TenCoSo);
        if (!result.Succeeded)
        {
            if (result.Code == 3) return NotFound();
            ModelState.AddModelError(nameof(model.MaCoSo), result.Message ?? "Không thể cập nhật cơ sở y tế.");
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var advertisingResult = await SaveAdvertisingAsync(
            new DMCSKCB { MaCoSo = model.MaCoSo, TenCoSo = model.TenCoSo },
            model,
            advertisingImageUrl);
        if (!advertisingResult.Succeeded)
        {
            Error(advertisingResult.Message ?? "Khong the luu quang cao.");
            return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
        }

        if (string.Equals(model.ActiveSection, "noiDungChiTiet", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(model.TopicContentsJson))
        {
            var topicContents = ParseTopicContents(model.TopicContentsJson);
            if (string.Equals(model.ActiveSection, "noiDungChiTiet", StringComparison.OrdinalIgnoreCase)
                && model.TopicId > 0
                && !topicContents.ContainsKey(model.TopicId))
                topicContents[model.TopicId] = model.NoiDung;

            foreach (var topicContent in topicContents)
            {
            var contentResult = await SaveContentAsync(
                new DMCSKCB { MaCoSo = model.MaCoSo, TenCoSo = model.TenCoSo },
                topicContent.Key,
                topicContent.Value);
            if (!contentResult.Succeeded)
            {
                Error(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
            }
        }
        }

        Success("Đã cập nhật cơ sở y tế.");
        return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
    }

    private async Task<QCKCB?> GetAdvertisingAsync(string? maCoSo, string? tenCoSo)
    {
        var query = _db.QCKCBs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(maCoSo))
            query = query.Where(x => x.MaCoSo == maCoSo);
        else
            query = query.Where(x => (x.MaCoSo == null || x.MaCoSo == "") && x.TenCoSo == tenCoSo);

        return await query.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
    }

    private async Task<string?> ResolveAdvertisingImageAsync(
        CoSoYTeEditViewModel model,
        string? existingImage)
    {
        if (model.QuangCao.GetValueOrDefault() <= 0)
            return null;

        return await ResolveImageAsync(
            model.QuangCaoImageFile,
            model.QuangCaoImgUrlInput,
            existingImage,
            "static/img_qc_kcb",
            "/static/img_qc_kcb",
            nameof(model.QuangCaoImageFile));
    }

    private async Task<string?> ResolveImageAsync(
        IFormFile? imageFile,
        string? urlInput,
        string? fallback,
        string storageFolder,
        string publicPrefix,
        string propertyName)
    {
        if (imageFile != null)
            return await SaveImageAsync(imageFile, storageFolder, publicPrefix, propertyName);

        return string.IsNullOrWhiteSpace(urlInput) ? fallback : urlInput.Trim();
    }

    private Task<AdminStoredProcedureResult> SaveAdvertisingAsync(
        DMCSKCB facility,
        CoSoYTeEditViewModel model,
        string? imageUrl)
    {
        var enabled = model.QuangCao.GetValueOrDefault() > 0;
        return _adminStoredProcedures.SaveQCKCBAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            enabled ? model.NoiDungQuangCao : null,
            enabled ? imageUrl : null,
            enabled);
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
        model.SoToaNha = model.SoToaNha?.Trim();
        model.LoaiCS = model.LoaiCS?.Trim().ToLowerInvariant();
        model.TGLamViec = model.TGLamViec?.Trim();
        model.NgayLamViec = model.NgayLamViec?.Trim();
        model.GioMoCua = model.GioMoCua?.Trim();
        model.GioDongCua = model.GioDongCua?.Trim();
        model.Img = model.Img?.Trim();
        model.Logo = model.Logo?.Trim();
        model.LogoUrlInput = model.LogoUrlInput?.Trim();
        model.NoiDungQuangCao = model.NoiDungQuangCao?.Trim();
        model.QuangCaoImg = model.QuangCaoImg?.Trim();
        model.QuangCaoImgUrlInput = model.QuangCaoImgUrlInput?.Trim();
        model.ActiveSection = model.ActiveSection?.Trim();
        model.TopicContentsJson = model.TopicContentsJson?.Trim();
    }

    private static Dictionary<long, string?> ParseTopicContents(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<long, string?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<long, string?>>(json)
                ?.Where(item => item.Key > 0)
                .ToDictionary(item => item.Key, item => item.Value)
                ?? new Dictionary<long, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<long, string?>();
        }
    }

    private void ApplyOperatingHours(CoSoYTeEditViewModel model)
    {
        var hasSelection = !string.IsNullOrWhiteSpace(model.NgayLamViec)
            || !string.IsNullOrWhiteSpace(model.GioMoCua)
            || !string.IsNullOrWhiteSpace(model.GioDongCua);

        if (!hasSelection)
        {
            model.TGLamViec = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(model.NgayLamViec))
            ModelState.AddModelError(nameof(model.NgayLamViec), "Vui lòng chọn ngày hoạt động.");
        var validOpenTime = OperatingHours.TryParseTime(model.GioMoCua, out var openTime);
        var validCloseTime = OperatingHours.TryParseTime(model.GioDongCua, out var closeTime);
        if (!validOpenTime)
            ModelState.AddModelError(nameof(model.GioMoCua), "Vui lòng chọn giờ mở cửa.");
        if (!validCloseTime)
            ModelState.AddModelError(nameof(model.GioDongCua), "Vui lòng chọn giờ đóng cửa.");

        if (!ModelState.IsValid || !validOpenTime || !validCloseTime)
            return;

        if (closeTime <= openTime)
        {
            ModelState.AddModelError(nameof(model.GioDongCua), "Giờ đóng cửa phải sau giờ mở cửa.");
            return;
        }

        model.TGLamViec = OperatingHours.Encode(
            model.NgayLamViec!,
            openTime.ToString("HH:mm"),
            closeTime.ToString("HH:mm"));
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Không có file nào được tải lên.");
        }

        var url = await SaveImageAsync(file, "static/img_cs", "/static/img_cs", "file");
        if (url == null)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(string.Join("\n", errors));
        }

        return Json(new { url = url });
    }

    [HttpGet]
    public async Task<IActionResult> GetContent(long id, long topicId)
    {
        var facility = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (facility == null || topicId <= 0) return NotFound();

        var noiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            topicId);
        return Json(new { noiDung = noiDung ?? string.Empty });
    }

    private async Task PopulateContentEditorAsync(
        CoSoYTeEditViewModel model,
        long? topicId = null,
        bool loadSelectedContent = true)
    {
        model.NhomCSList = await _db.DMNhomCSs.AsNoTracking().ToListAsync();
        model.ChuDeList = await _db.DMChuDes.AsNoTracking().ToListAsync();
        model.FacilityList = await _db.DMCSKCBs.AsNoTracking().ToListAsync();
        model.SelectedFacilityId = model.Id > 0 ? model.Id : null;
        model.SelectedNhomCSId = model.Id > 0
            ? model.NhomCSList.FirstOrDefault(x =>
                string.Equals(x.LoaiCS, model.LoaiCS, StringComparison.OrdinalIgnoreCase))?.ID
            : null;
        model.SelectedTopicId = topicId ?? model.ChuDeList.FirstOrDefault()?.ID;
        model.TopicId = model.SelectedTopicId ?? 0;

        if (loadSelectedContent && model.Id > 0 && model.TopicId > 0)
        {
            model.NoiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(
                model.MaCoSo,
                model.TenCoSo,
                model.TopicId);
        }
    }

    private Task<AdminStoredProcedureResult> SaveContentAsync(
        DMCSKCB facility,
        long topicId,
        string? noiDung) =>
        _adminStoredProcedures.SaveNoiDungCskcbAsync(
            facility.MaCoSo,
            facility.TenCoSo,
            topicId,
            noiDung);

    private static CoSoYTeEditViewModel ToViewModel(DMCSKCB entity)
    {
        OperatingHours.TryParse(entity.TGLamViec, out var operatingHours);
        var storedDays = string.IsNullOrWhiteSpace(entity.NgayLamViec)
            ? operatingHours?.Days
            : entity.NgayLamViec;
        var storedOpenTime = entity.GioMoCua?.ToString(@"hh\:mm") ?? operatingHours?.OpenTime;
        var storedCloseTime = entity.GioDongCua?.ToString(@"hh\:mm") ?? operatingHours?.CloseTime;
        return new CoSoYTeEditViewModel
        {
            Id = entity.Id,
            MaCoSo = entity.MaCoSo,
            TenCoSo = entity.TenCoSo,
            DiaChi = entity.DiaChi,
            SoToaNha = entity.SoToaNha,
            Tinh = entity.Tinh,
            PhuongXa = entity.PhuongXa,
            LoaiCS = entity.LoaiCS,
            TGLamViec = entity.TGLamViec,
            NgayLamViec = storedDays,
            GioMoCua = storedOpenTime,
            GioDongCua = storedCloseTime,
            XacMinh = entity.XacMinh == 1,
            Img = entity.Img,
            Logo = entity.logo,
            QuangCao = entity.QuangCao
        };
    }

}

