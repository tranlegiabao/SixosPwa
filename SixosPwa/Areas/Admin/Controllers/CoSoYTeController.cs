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

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private IActionResult AjaxFailure(string message) =>
        BadRequest(new { success = false, message });

    private IActionResult AjaxValidationFailure()
    {
        var errors = ModelState
            .Where(item => item.Value?.Errors.Count > 0)
            .ToDictionary(
                item => item.Key,
                item => item.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray());

        return UnprocessableEntity(new
        {
            success = false,
            message = "Vui lòng kiểm tra lại thông tin đã nhập.",
            errors
        });
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
        {
            // Nhom co so nay la khoa ngoai sang DM_NhomCS, khong con la chuoi trong bang co so.
            var idNhom = await _db.DMNhomCSs.AsNoTracking()
                .Where(nc => nc.MaNhom == loaiCS)
                .Select(nc => (long?)nc.ID)
                .FirstOrDefaultAsync();
            query = query.Where(x => x.IdNhomCS == idNhom);
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.TenCoSo)
            .Skip((page - 1) * DefaultPageSize)
            .Take(DefaultPageSize)
            .ToListAsync();

        var maNhom = await _db.DMNhomCSs.AsNoTracking()
            .ToDictionaryAsync(x => x.ID, x => x.MaNhom);

        return View(new CoSoYTeListViewModel
        {
            MaNhomTheoId = maNhom,
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
        await ApDungLoaiCoSoAsync(model);
        if (string.IsNullOrWhiteSpace(model.Slug))
            ModelState.AddModelError(nameof(model.Slug), "Vui lòng nhập đường dẫn cố định.");

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
            if (IsAjaxRequest()) return AjaxValidationFailure();
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var result = await _adminStoredProcedures.SaveCoSoYTeAsync(model);
        if (!result.Succeeded)
        {
            if (IsAjaxRequest()) return AjaxFailure(result.Message ?? "Không thể thêm cơ sở y tế.");
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
            var loiGio = await LuuGioLamViecAsync(createdFacilityForAdvertising.Id, model);
            if (loiGio != null)
            {
                if (IsAjaxRequest()) return AjaxFailure(loiGio);
                Error(loiGio);
                return RedirectToAction(nameof(Edit), new { id = createdFacilityForAdvertising.Id, topicId = model.TopicId });
            }

            var advertisingResult = await SaveAdvertisingAsync(
                createdFacilityForAdvertising.Id,
                model,
                advertisingImageUrl);
            if (!advertisingResult.Succeeded)
            {
                if (IsAjaxRequest()) return AjaxFailure(advertisingResult.Message ?? "Không thể lưu quảng cáo.");
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
                var contentResult = await SaveContentAsync(createdFacility.Id, topicContent.Key, topicContent.Value);
                if (!contentResult.Succeeded)
                {
                    if (IsAjaxRequest()) return AjaxFailure(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                    Error(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                    return RedirectToAction(nameof(Edit), new { id = createdFacility.Id, topicId = model.TopicId });
                }
            }
            }
        }

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = "Đã thêm cơ sở y tế.",
                id = createdFacilityForAdvertising?.Id ?? 0,
                editUrl = createdFacilityForAdvertising == null
                    ? null
                    : Url.Action(nameof(Edit), new { id = createdFacilityForAdvertising.Id, topicId = model.TopicId })
            });
        }
        Success("Đã thêm cơ sở y tế.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, long? topicId = null, string? section = null)
    {
        var entity = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        // Ban ASYNC moi nap LoaiCS + gio lam viec tu bang con DM_CSKCB_GioLamViec.
        // Ban dong bo de ca bon truong nay NULL, ma man Sua co bind ca bon =>
        // o gio trang va "Loai hinh" tut ve "Chua phan loai", bam Luu la XOA MAT
        // loai co so. Da dinh o Dot 3.
        var model = await ToViewModelAsync(entity);
        model.ActiveSection = section;
        var advertising = await GetAdvertisingAsync(entity.Id);
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

        var existingAdvertising = await GetAdvertisingAsync(entity.Id);
        Normalize(model);
        await ApDungLoaiCoSoAsync(model);
        if (model.ImageFile != null)
            model.Img = await SaveImageAsync(model.ImageFile, "static/img_cs", "/static/img_cs", nameof(model.ImageFile));
        else
            model.Img = entity.Img;
        model.Logo = await ResolveImageAsync(
            model.LogoFile,
            model.LogoUrlInput,
            entity.Logo,
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
            if (IsAjaxRequest()) return AjaxValidationFailure();
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var result = await _adminStoredProcedures.SaveCoSoYTeAsync(model);
        if (!result.Succeeded)
        {
            if (result.Code == 3)
                return IsAjaxRequest()
                    ? NotFound(new { success = false, message = "Không tìm thấy cơ sở y tế." })
                    : NotFound();
            if (IsAjaxRequest()) return AjaxFailure(result.Message ?? "Không thể cập nhật cơ sở y tế.");
            ModelState.AddModelError(nameof(model.MaCoSo), result.Message ?? "Không thể cập nhật cơ sở y tế.");
            await PopulateContentEditorAsync(model, model.TopicId > 0 ? model.TopicId : null, loadSelectedContent: false);
            return View(model);
        }

        var loiGioLamViec = await LuuGioLamViecAsync(model.Id, model);
        if (loiGioLamViec != null)
        {
            if (IsAjaxRequest()) return AjaxFailure(loiGioLamViec);
            Error(loiGioLamViec);
            return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
        }

        var advertisingResult = await SaveAdvertisingAsync(model.Id, model, advertisingImageUrl);
        if (!advertisingResult.Succeeded)
        {
            if (IsAjaxRequest()) return AjaxFailure(advertisingResult.Message ?? "Không thể lưu quảng cáo.");
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
                model.Id,
                topicContent.Key,
                topicContent.Value);
            if (!contentResult.Succeeded)
            {
                if (IsAjaxRequest()) return AjaxFailure(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                Error(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
            }
        }
        }

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = "Đã cập nhật cơ sở y tế.",
                id = model.Id,
                editUrl = Url.Action(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection })
            });
        }
        Success("Đã cập nhật cơ sở y tế.");
        return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, bool confirmed, string? q, string? loaiCS, int page = 1)
    {
        if (!confirmed)
        {
            Error("Cần xác nhận trước khi xóa cơ sở y tế.");
            return RedirectToAction(nameof(Index), new { q, loaiCS, page = SafePage(page) });
        }

        var result = await _adminStoredProcedures.DeleteCoSoYTeAsync(id);
        if (result.Succeeded)
            Success("Đã xóa cơ sở y tế.");
        else
            Error(result.Message ?? "Không thể xóa cơ sở y tế.");

        return RedirectToAction(nameof(Index), new { q, loaiCS, page = SafePage(page) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(CoSoYTeEditViewModel model)
    {
        var storedFacility = model.Id > 0
            ? await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id)
            : null;
        var facility = new DMCSKCB
        {
            Id = model.Id,
            MaCoSo = model.MaCoSo ?? storedFacility?.MaCoSo ?? string.Empty,
            Slug = model.Slug ?? storedFacility?.Slug ?? string.Empty,
            TenCoSo = model.TenCoSo ?? storedFacility?.TenCoSo ?? string.Empty,
            DiaChi = model.DiaChi ?? storedFacility?.DiaChi,
            IdNhomCS = model.SelectedNhomCSId ?? storedFacility?.IdNhomCS,
            Img = model.Img ?? storedFacility?.Img,
            Logo = await ReadPreviewImageAsync(model.LogoFile, model.LogoUrlInput, model.Logo ?? storedFacility?.Logo),
            XacMinh = model.XacMinh
        };

        // Gio lam viec nay nam o bang con DM_CSKCB_GioLamViec, khong con la cot cua
        // bang co so. Man xem truoc chi can chuoi hien thi nen dung thang gia tri
        // dang nhap tren form.
        var moCuaXemTruoc = ParsePreviewTime(model.GioMoCua);
        var dongCuaXemTruoc = ParsePreviewTime(model.GioDongCua);
        var tgLamViecXemTruoc = model.TGLamViec;
        if (moCuaXemTruoc.HasValue && dongCuaXemTruoc.HasValue
            && !string.IsNullOrWhiteSpace(model.NgayLamViec))
        {
            tgLamViecXemTruoc = OperatingHours.Encode(
                model.NgayLamViec,
                moCuaXemTruoc.Value.ToString("HH:mm"),
                dongCuaXemTruoc.Value.ToString("HH:mm"));
        }

        var topics = await _db.DMChuDes.AsNoTracking().ToListAsync();
        var contents = await LoadPreviewContentsAsync(facility, topics);
        var draftContents = ParseTopicContents(model.TopicContentsJson);
        foreach (var draft in draftContents)
        {
            var topic = topics.FirstOrDefault(x => x.ID == draft.Key);
            if (!string.IsNullOrWhiteSpace(topic?.MaChuDe))
                contents[topic.MaChuDe!] = draft.Value ?? string.Empty;
        }

        if (model.TopicId > 0 && !draftContents.ContainsKey(model.TopicId))
        {
            var topic = topics.FirstOrDefault(x => x.ID == model.TopicId);
            if (!string.IsNullOrWhiteSpace(topic?.MaChuDe))
                contents[topic.MaChuDe!] = model.NoiDung ?? string.Empty;
        }

        ViewData["Title"] = "Xem trước cơ sở y tế";
        ViewData["CoSoYTe"] = facility;
        ViewData["MaCoSo"] = facility.MaCoSo;
        ViewData["Slug"] = facility.Slug;
        ViewData["TenCoSo"] = facility.TenCoSo ?? "Cơ sở y tế";
        ViewData["DiaChi"] = facility.DiaChi ?? "Đang cập nhật";
        ViewData["Type"] = model.LoaiCS ?? "benhvien";
        ViewData["Img"] = facility.Img;
        ViewData["Logo"] = facility.Logo;
        ViewData["TGLamViec"] = tgLamViecXemTruoc;
        ViewData["NoiDungCskcb"] = contents;
        ViewData["PreviewLoaiND"] = topics.FirstOrDefault(x => x.ID == model.TopicId)?.MaChuDe;
        ViewData["PreviewStatic"] = true;

        return View("~/Views/Home/ChiTietCoSo.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewHome(CoSoYTeEditViewModel model)
    {
        var storedFacility = model.Id > 0
            ? await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id)
            : null;
        var storedAdvertising = storedFacility == null
            ? null
            : await GetAdvertisingAsync(storedFacility.Id);

        var previewItems = await LoadHomePreviewAdsAsync();
        var previewImage = await ReadPreviewImageAsync(
            model.QuangCaoImageFile,
            model.QuangCaoImgUrlInput,
            model.QuangCaoImg ?? storedAdvertising?.Img);
        var previewContent = string.IsNullOrWhiteSpace(model.NoiDungQuangCao)
            ? storedAdvertising?.NoiDung
            : model.NoiDungQuangCao;

        if (model.QuangCao.GetValueOrDefault() > 0
            && !string.IsNullOrWhiteSpace(model.TenCoSo))
        {
            previewItems.RemoveAll(x => string.Equals(x.TenCoSo, model.TenCoSo, StringComparison.OrdinalIgnoreCase));
            previewItems.Insert(0, new TopCSKCBQC
            {
                TenCoSo = model.TenCoSo,
                NoiDung = previewContent ?? string.Empty,
                Img = previewImage ?? string.Empty
            });
        }

        ViewData["Title"] = "Xem trước trang Home";
        ViewData["TopCSKCB"] = previewItems;
        ViewData["PreviewStatic"] = true;

        return View("~/Views/Home/ThongTinBenhNhan.cshtml", new List<LichSuKham>());
    }

    /// <summary>
    /// Quang cao nay khoa theo IDCoSo. Truoc dot tai kien truc no phai do tim theo
    /// MaCoSo roi nga sang TenCoSo — mot bang khong co rang buoc nao noi ve co so.
    /// UK_DM_CSKCB_QuangCao bao dam moi co so nhieu nhat mot dong.
    /// </summary>
    private Task<QCKCB?> GetAdvertisingAsync(long idCoSo) =>
        _db.QCKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.IdCoSo == idCoSo);

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
        long idCoSo,
        CoSoYTeEditViewModel model,
        string? imageUrl)
    {
        var enabled = model.QuangCao.GetValueOrDefault() > 0;
        return _adminStoredProcedures.SaveQCKCBAsync(
            idCoSo,
            enabled ? model.NoiDungQuangCao : null,
            enabled ? imageUrl : null);
    }

    private void ValidateType(string? type)
    {
        if (!string.IsNullOrWhiteSpace(type) && !AllowedTypes.Contains(type))
            ModelState.AddModelError(nameof(CoSoYTeEditViewModel.LoaiCS), "Loại cơ sở không hợp lệ.");
    }

    /// <summary>
    /// Bac cau o "Loai hinh" tren form sang khoa ngoai ma thu tuc luu doc.
    ///
    /// O do bind vao <c>LoaiCS</c> (chuoi ma nhom, vd "pkdk"), nhung
    /// <c>SaveCoSoYTeAsync</c> lai gui <c>@IDNhomCS</c> lay tu
    /// <c>SelectedNhomCSId</c> — ma KHONG co o nao tren form dat gia tri do, nen
    /// no luon ve null sau model binding. Thu tuc <c>DM_CSKCB_Save</c> thi
    /// <c>SET IDNhomCS = @IDNhomCS</c> VO DIEU KIEN, khong bo qua null.
    ///
    /// Hau qua truoc ban va: MOI lan bam Luu deu xoa trang Loai hinh cua co so,
    /// ke ca khi khong ai dung toi o do. Dau vet con lai trong DB: nhung co so
    /// tung sua qua man nay (ID 1, 8, 11, 15, 18) deu co IDNhomCS = NULL, con
    /// nhung co so chua ai sua thi van giu nguyen gia tri seed.
    /// Nam sua
    /// </summary>
    private async Task ApDungLoaiCoSoAsync(CoSoYTeEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.LoaiCS))
        {
            // "Chua phan loai" — o day null moi la y dinh that cua nguoi dung.
            model.SelectedNhomCSId = null;
            return;
        }

        model.SelectedNhomCSId = await _db.DMNhomCSs.AsNoTracking()
            .Where(x => x.MaNhom == model.LoaiCS)
            .Select(x => (long?)x.ID)
            .FirstOrDefaultAsync();
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
        model.Slug = string.IsNullOrWhiteSpace(model.Slug)
            ? null
            : model.Slug.Trim().ToLowerInvariant();
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

    private async Task<Dictionary<string, string>> LoadPreviewContentsAsync(
        DMCSKCB facility,
        IReadOnlyCollection<DMChuDe> topics)
    {
        var items = await _db.NDCSKCBs.AsNoTracking()
            .Where(x => x.IdCoSo == facility.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        // Chu de nay la khoa ngoai IDChuDe, khong con la chuoi LoaiND tron hai he ma.
        var maTheoId = topics.ToDictionary(x => x.ID, x => x.MaChuDe);

        return items
            .Where(x => maTheoId.ContainsKey(x.IdChuDe))
            .Select(x => new { Item = x, Ma = maTheoId[x.IdChuDe] })
            .Where(x => NDCSKCB.AllowedLoaiND.Contains(x.Ma, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.Ma, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Item.NoiDung ?? "", StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<TopCSKCBQC>> LoadHomePreviewAdsAsync()
    {
        var facilities = await _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.QuangCao.GetValueOrDefault() > 0)
            .OrderByDescending(x => x.QuangCao)
            .Take(5)
            .ToListAsync();
        var facilityIds = facilities.Select(x => x.Id).ToList();
        var advertising = await _db.QCKCBs.AsNoTracking()
            .Where(x => facilityIds.Contains(x.IdCoSo))
            .OrderByDescending(x => x.Id)
            .ToListAsync();

        return facilities.Select(facility =>
        {
            var item = advertising.FirstOrDefault(x => x.IdCoSo == facility.Id);
            return new TopCSKCBQC
            {
                TenCoSo = facility.TenCoSo ?? string.Empty,
                NoiDung = item?.NoiDung ?? string.Empty,
                Img = item?.Img ?? facility.Img ?? string.Empty
            };
        }).ToList();
    }

    private static string? ResolvePreviewLoaiND(string? storedLoaiND, IReadOnlyDictionary<string, string> topicById)
    {
        var value = storedLoaiND?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (topicById.TryGetValue(value, out var loaiND)) return loaiND;
        return NDCSKCB.AllowedLoaiND.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
    }

    private static TimeSpan? ParsePreviewTime(string? value) =>
        OperatingHours.TryParseTime(value, out var time) ? time.ToTimeSpan() : null;

    private static async Task<string?> ReadPreviewImageAsync(IFormFile? imageFile, string? urlInput, string? fallback)
    {
        if (imageFile is { Length: > 0 })
        {
            await using var stream = new MemoryStream();
            await imageFile.CopyToAsync(stream);
            var contentType = string.IsNullOrWhiteSpace(imageFile.ContentType) ? "image/*" : imageFile.ContentType;
            return $"data:{contentType};base64,{Convert.ToBase64String(stream.ToArray())}";
        }

        return string.IsNullOrWhiteSpace(urlInput) ? fallback : urlInput.Trim();
    }

    // ------------------------------------------------------------------
    //  Gio lam viec — dich giua CUM CHU cua form va SO THU cua bang con
    // ------------------------------------------------------------------
    //  Man hinh cho chon 4 cum chu co san (xem _CoSoYTeOperatingHoursFields),
    //  con DM_CSKCB_GioLamViec luu tung ngay mot bang so Thu 0..6 (0 = Chu
    //  nhat). Hai the gioi nay phai co bo dich, neu khong thi luu xong doc lai
    //  se ra chuoi "0,1,2,..." khong khop <option> nao va o chon tut ve rong.

    private static readonly Dictionary<string, byte[]> CumNgayChuan = new()
    {
        ["Thứ 2 - Chủ nhật"] = new byte[] { 1, 2, 3, 4, 5, 6, 0 },
        ["Thứ 2 - Thứ 7"]    = new byte[] { 1, 2, 3, 4, 5, 6 },
        ["Thứ 2 - Thứ 6"]    = new byte[] { 1, 2, 3, 4, 5 },
        ["Thứ 7 - Chủ nhật"] = new byte[] { 6, 0 },
    };

    /// <summary>Cum chu tren form -> danh sach so Thu. Khong khop = ca tuan.</summary>
    private static byte[] SoThuTuCumNgay(string? cumNgay)
    {
        if (string.IsNullOrWhiteSpace(cumNgay)) return Array.Empty<byte>();
        if (CumNgayChuan.TryGetValue(cumNgay.Trim(), out var thu)) return thu;

        // Du lieu cu co the da luu dang "0,1,2" — van doc duoc.
        var tach = cumNgay.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => byte.TryParse(x, out var v) && v <= 6 ? (byte?)v : null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();
        return tach.Length > 0 ? tach : CumNgayChuan["Thứ 2 - Chủ nhật"];
    }

    /// <summary>Danh sach so Thu doc tu DB -> dung cum chu de form chon lai duoc.</summary>
    private static string? CumNgayTuSoThu(IEnumerable<byte> soThu)
    {
        var tap = soThu.Distinct().OrderBy(x => x).ToArray();
        if (tap.Length == 0) return null;

        foreach (var (cum, thu) in CumNgayChuan)
            if (thu.OrderBy(x => x).SequenceEqual(tap))
                return cum;

        return OperatingHours.Default.Days;   // khong khop cum nao: lay ca tuan
    }

    /// <summary>
    /// Ghi gio lam viec cua mot co so: xoa ngay khong con chon, roi upsert tung
    /// ngay con lai. Tra ve thong diep loi dau tien, null neu dat.
    /// </summary>
    private async Task<string?> LuuGioLamViecAsync(long idCoSo, CoSoYTeEditViewModel model)
    {
        // Khong chon gi = xoa sach gio cua co so do.
        if (string.IsNullOrWhiteSpace(model.TGLamViec))
        {
            var xoaHet = await _adminStoredProcedures.XoaGioLamViecAsync(idCoSo, null);
            return xoaHet.Succeeded ? null : (xoaHet.Message ?? "Không thể xoá giờ làm việc.");
        }

        var danhSachThu = SoThuTuCumNgay(model.NgayLamViec);
        if (danhSachThu.Length == 0
            || !OperatingHours.TryParseTime(model.GioMoCua, out var moCua)
            || !OperatingHours.TryParseTime(model.GioDongCua, out var dongCua))
            return null;   // ApplyOperatingHours da bat truong hop nay roi

        var xoa = await _adminStoredProcedures.XoaGioLamViecAsync(
            idCoSo, string.Join(",", danhSachThu));
        if (!xoa.Succeeded) return xoa.Message ?? "Không thể dọn giờ làm việc cũ.";

        // Goi lap tung ngay. Khong nguyen tu qua ca tuan, nhung moi lan la
        // upsert idempotent theo UNIQUE (IDCoSo, Thu) nen chay lai an toan.
        foreach (var thu in danhSachThu)
        {
            var kq = await _adminStoredProcedures.SaveGioLamViecAsync(
                idCoSo, thu, moCua.ToTimeSpan(), dongCua.ToTimeSpan());
            if (!kq.Succeeded) return kq.Message ?? "Không thể lưu giờ làm việc.";
        }
        return null;
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

        var noiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(facility.Id, topicId);

        if (noiDung == null)
        {
            var topic = await _db.DMChuDes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == topicId);
            if (topic != null)
            {
                var fallbackContents = await LoadPreviewContentsAsync(facility, new[] { topic });
                if (!string.IsNullOrWhiteSpace(topic.MaChuDe)
                    && fallbackContents.TryGetValue(topic.MaChuDe, out var fallbackContent))
                    noiDung = fallbackContent;
            }
        }

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
        model.SelectedNhomCSId ??= model.Id > 0
            ? model.NhomCSList.FirstOrDefault(x =>
                string.Equals(x.MaNhom, model.LoaiCS, StringComparison.OrdinalIgnoreCase))?.ID
            : null;
        model.SelectedTopicId = topicId ?? model.ChuDeList.FirstOrDefault()?.ID;
        model.TopicId = model.SelectedTopicId ?? 0;

        if (loadSelectedContent && model.Id > 0 && model.TopicId > 0)
        {
            model.NoiDung = await _adminStoredProcedures.GetNoiDungCskcbAsync(model.Id, model.TopicId);
        }
    }

    private Task<AdminStoredProcedureResult> SaveContentAsync(
        long idCoSo,
        long topicId,
        string? noiDung) =>
        _adminStoredProcedures.SaveNoiDungCskcbAsync(idCoSo, topicId, noiDung);

    /// <summary>
    /// Gio lam viec khong con nam trong bang co so — no o bang con DM_CSKCB_GioLamViec.
    /// Ban dung <see cref="ToViewModelAsync"/> khi can hien gio; ban dong bo nay de
    /// nhung cho chi can thong tin chung.
    /// </summary>
    private static CoSoYTeEditViewModel ToViewModel(DMCSKCB entity) =>
        new()
        {
            Id = entity.Id,
            MaCoSo = entity.MaCoSo,
            Slug = entity.Slug,
            TenCoSo = entity.TenCoSo,
            DiaChi = entity.DiaChi,
            SoToaNha = entity.SoToaNha,
            Tinh = entity.Tinh,
            PhuongXa = entity.PhuongXa,
            SDT = entity.SDT,
            Email = entity.Email,
            TenTM = entity.TenTM,
            SelectedNhomCSId = entity.IdNhomCS,
            XacMinh = entity.XacMinh,
            Active = entity.Active,
            Img = entity.Img,
            Logo = entity.Logo,
            QuangCao = entity.QuangCao
        };

    /// <summary>Nhu tren nhung nap them gio lam viec tu bang con.</summary>
    private async Task<CoSoYTeEditViewModel> ToViewModelAsync(DMCSKCB entity)
    {
        var model = ToViewModel(entity);
        model.LoaiCS = entity.IdNhomCS is null
            ? null
            : await _db.DMNhomCSs.AsNoTracking()
                .Where(x => x.ID == entity.IdNhomCS)
                .Select(x => x.MaNhom)
                .FirstOrDefaultAsync();

        var gio = await _db.CSKCBGioLamViecs.AsNoTracking()
            .Where(x => x.IdCoSo == entity.Id)
            .OrderBy(x => x.Thu)
            .ToListAsync();

        if (gio.Count > 0)
        {
            model.GioMoCua = gio[0].GioMoCua.ToString(@"hh\:mm");
            model.GioDongCua = gio[0].GioDongCua.ToString(@"hh\:mm");
            // Phai tra ve CUM CHU ("Thu 2 - Chu nhat"), khong phai "0,1,2,...":
            // form la mot <select> bon lua chon co san, chuoi so khong khop
            // <option> nao nen o chon se tut ve rong va bam Luu la mat gio.
            model.NgayLamViec = CumNgayTuSoThu(gio.Select(x => x.Thu));
            model.TGLamViec = OperatingHours.Encode(
                model.NgayLamViec ?? OperatingHours.Default.Days,
                model.GioMoCua, model.GioDongCua);
        }

        return model;
    }

}

