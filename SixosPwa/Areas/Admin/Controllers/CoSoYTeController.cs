using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class CoSoYTeController : AdminControllerBase
{
    private static readonly string[] AllowedTypes = { "benhvien", "pkdk", "nhakhoa", "phongmach", "nhathuoc" };
    private static readonly Regex ManagedFacilityLogoRegex = new(
        "<div\\b(?=[^>]*\\bdata-cskcb-facility-logo\\s*=\\s*(['\"])true\\1)[^>]*>.*?</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _adminStoredProcedures;
    private readonly IFtpService _ftp;
    private readonly IDonAnhService _donAnh;
    /// <summary>Chi de bam nut "Thu ket noi kho". Lop nay CHI DOC (ADR 0030).</summary>
    private readonly IKhoCoSoService _khoCoSo;

    /// <summary>
    /// Canh bao KHONG chan viec Luu — vd FTP hong dung luc tai anh. Noi vao cuoi
    /// cau thong bao thanh cong nen hien duoc o ca nhanh AJAX lan nhanh thuong,
    /// khong phai dung toi .js nao.
    /// </summary>
    private readonly List<string> _canhBao = new();

    public CoSoYTeController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures,
        IFtpService ftp,
        IDonAnhService donAnh,
        IKhoCoSoService khoCoSo)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
        _ftp = ftp;
        _donAnh = donAnh;
        _khoCoSo = khoCoSo;
    }

    private string KemCanhBao(string thongBao) =>
        _canhBao.Count == 0 ? thongBao : $"{thongBao} {string.Join(" ", _canhBao)}";

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
        var model = new CoSoYTeEditViewModel();
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
            model.AnhBia = await SaveImageAsync(model.ImageFile, model.MaCoSo, KhoAnh.ThuMucHinhAnh, nameof(model.ImageFile));
        model.LogoRemoved = model.LogoRemoved
            && model.LogoFile == null
            && string.IsNullOrWhiteSpace(model.LogoUrlInput);
        model.Logo = model.LogoRemoved
            ? null
            : await ResolveImageAsync(
                model.LogoFile,
                model.LogoUrlInput,
                model.Logo,
                model.MaCoSo,
                KhoAnh.ThuMucLogo,
                nameof(model.LogoFile));
        var advertisingImageUrl = await ResolveAdvertisingImageAsync(model, null);
        model.QcAnh = advertisingImageUrl;
        // Giu nguyen net cu cua SaveAdvertisingAsync: so tien = 0 thi quang cao TAT,
        // ca noi dung lan anh deu ve null. Chi khac la gio no la cot cua chinh co so.
        if (model.QcSoTienDaTra.GetValueOrDefault() <= 0) model.QcNoiDung = null;
        ApplyOperatingHours(model);
        ValidateType(model.LoaiCS);
        ValidateAdvertisingAmount(model.QcSoTienDaTra);
        ValidateImageUrl(model.AnhBia, nameof(model.AnhBia), "/anh/", "/static/img_cs/", "/uploads/co-so-y-te/");
        ValidateImageUrl(model.Logo, nameof(model.Logo), "/anh/", "/static/logo_cs/", "/uploads/co-so-y-te/logo/");
        ValidateImageUrl(advertisingImageUrl, nameof(model.QcAnhUrlInput), "/anh/", "/static/img_qc_kcb/");
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

            // 🔴 Dot A: DM_CSKCB_QuangCao_Save bi bo — noi dung / anh quang cao la
            // cot QcNoiDung / QcAnh cua chinh DM_CSKCB, da luu xong o DM_CSKCB_Save
            // ngay tren. Khong con luot ghi thu hai nao o day.
        }

        if (createdFacilityForAdvertising != null)
        {
            var topicContents = await BuildLogoSynchronizedContentsAsync(
                createdFacilityForAdvertising.Id,
                model,
                model.Logo,
                includeActiveEditorContent: model.TopicId > 0);
            foreach (var topicContent in topicContents)
            {
                var contentResult = await SaveContentAsync(
                    createdFacilityForAdvertising.Id,
                    topicContent.TopicId,
                    topicContent.Updated);
                if (!contentResult.Succeeded)
                {
                    if (IsAjaxRequest()) return AjaxFailure(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                    Error(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                    return RedirectToAction(nameof(Edit), new { id = createdFacilityForAdvertising.Id, topicId = model.TopicId });
                }
            }
        }

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = KemCanhBao("Đã thêm cơ sở y tế."),
                id = createdFacilityForAdvertising?.Id ?? 0,
                editUrl = createdFacilityForAdvertising == null
                    ? null
                    : Url.Action(nameof(Edit), new { id = createdFacilityForAdvertising.Id, topicId = model.TopicId })
            });
        }
        Success(KemCanhBao("Đã thêm cơ sở y tế."));
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
        await PopulateContentEditorAsync(model, topicId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CoSoYTeEditViewModel model)
    {
        var entity = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        // Ghi nho anh cu de don SAU KHI luu thanh cong. Khong duoc xoa som: doan
        // giai quyet anh nam truoc SaveCoSoYTeAsync, thu tuc do van co the that
        // bai — xoa truoc la mat anh trong khi DB con tro toi no.
        var logoCu = entity.Logo;
        var anhCoSoCu = entity.AnhBia;
        var anhQuangCaoCu = entity.QcAnh;

        Normalize(model);
        await ApDungLoaiCoSoAsync(model);
        if (model.ImageFile != null)
            model.AnhBia = await SaveImageAsync(model.ImageFile, model.MaCoSo, KhoAnh.ThuMucHinhAnh, nameof(model.ImageFile)) ?? entity.AnhBia;
        else
            model.AnhBia = entity.AnhBia;
        model.LogoRemoved = model.LogoRemoved
            && model.LogoFile == null
            && string.IsNullOrWhiteSpace(model.LogoUrlInput);
        model.Logo = model.LogoRemoved
            ? null
            : await ResolveImageAsync(
                model.LogoFile,
                model.LogoUrlInput,
                entity.Logo,
                model.MaCoSo,
                KhoAnh.ThuMucLogo,
                nameof(model.LogoFile));
        var advertisingImageUrl = await ResolveAdvertisingImageAsync(model, entity.QcAnh);
        model.QcAnh = advertisingImageUrl;
        // Giu nguyen net cu cua SaveAdvertisingAsync: so tien = 0 thi quang cao TAT,
        // ca noi dung lan anh deu ve null. Chi khac la gio no la cot cua chinh co so.
        if (model.QcSoTienDaTra.GetValueOrDefault() <= 0) model.QcNoiDung = null;
        ApplyOperatingHours(model);
        ValidateType(model.LoaiCS);
        ValidateAdvertisingAmount(model.QcSoTienDaTra);
        ValidateImageUrl(model.AnhBia, nameof(model.AnhBia), "/anh/", "/static/img_cs/", "/uploads/co-so-y-te/");
        ValidateImageUrl(model.Logo, nameof(model.Logo), "/anh/", "/static/logo_cs/", "/uploads/co-so-y-te/logo/");
        ValidateImageUrl(advertisingImageUrl, nameof(model.QcAnhUrlInput), "/anh/", "/static/img_qc_kcb/");

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

        // 🔴 Dot A: HT_KhoFtpCoSo_Save bi bo — cau hinh kho la cot Ftp_* cua chinh
        // DM_CSKCB, da luu cung luot DM_CSKCB_Save ngay tren. Khong con luot ghi thu
        // hai, nen cung khong con canh bao "kho chua luu" rieng.

        var loiGioLamViec = await LuuGioLamViecAsync(model.Id, model);
        if (loiGioLamViec != null)
        {
            if (IsAjaxRequest()) return AjaxFailure(loiGioLamViec);
            Error(loiGioLamViec);
            return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
        }

        // Ban HTML truoc khi sua — dung de biet anh nao vua bi admin xoa khoi bai.
        var noiDungCu = new List<(string? Cu, string? Moi)>();
        var topicContents = await BuildLogoSynchronizedContentsAsync(
            model.Id,
            model,
            model.Logo,
            includeActiveEditorContent: string.Equals(
                model.ActiveSection,
                "noiDungChiTiet",
                StringComparison.OrdinalIgnoreCase));
        foreach (var topicContent in topicContents)
        {
            noiDungCu.Add((topicContent.Existing, topicContent.Updated));

            var contentResult = await SaveContentAsync(
                model.Id,
                topicContent.TopicId,
                topicContent.Updated);
            if (!contentResult.Succeeded)
            {
                if (IsAjaxRequest()) return AjaxFailure(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                Error(contentResult.Message ?? "Không thể lưu nội dung HTML.");
                return RedirectToAction(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection });
            }
        }

        // Toi day moi ba thu tuc luu deu da thanh cong => DB dang giu gia tri MOI,
        // nen anh cu nao khong con dong nao tro toi thi don duoc.
        await _donAnh.DonAsync(logoCu, model.Logo);
        await _donAnh.DonAsync(anhCoSoCu, model.AnhBia);
        await _donAnh.DonAsync(anhQuangCaoCu, advertisingImageUrl);
        foreach (var (cu, moi) in noiDungCu)
            await _donAnh.DonTheoHtmlAsync(cu, moi);

        if (IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = KemCanhBao("Đã cập nhật cơ sở y tế."),
                id = model.Id,
                editUrl = Url.Action(nameof(Edit), new { id = model.Id, topicId = model.TopicId, section = model.ActiveSection })
            });
        }
        Success(KemCanhBao("Đã cập nhật cơ sở y tế."));
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

        // Doc anh cua co so TRUOC khi xoa. DM_CSKCB_Delete xoa ca dong cua co so
        // lan moi dong DM_CSKCB_NoiDung cua no, nen sau khi
        // goi thu tuc thi khong con cach nao biet no da dung nhung anh gi.
        var coSo = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        var baiViet = coSo == null
            ? new List<string?>()
            : await _db.NDCSKCBs.AsNoTracking()
                .Where(x => x.IdCoSo == id)
                .Select(x => x.NoiDung)
                .ToListAsync();

        var result = await _adminStoredProcedures.DeleteCoSoYTeAsync(id);
        if (result.Succeeded)
        {
            // Xoa co so xong ma khong don thi logo, anh quang cao va moi anh nhung
            // trong bai viet cua no deu thanh anh mo coi tren kho — dung loai rac
            // ma DonAnhService sinh ra de dep, chi la o mot cua khac.
            //
            // Chay SAU khi thu tuc thanh cong, y nhu duong Sua: luc nay dong cua co
            // so da bien khoi DB nen phep do cheo trong DonAnhService tra dung ket
            // qua — anh nao con co so KHAC dung thi van duoc giu lai.
            await _donAnh.DonAsync(coSo?.Logo, null);
            await _donAnh.DonAsync(coSo?.AnhBia, null);
            await _donAnh.DonAsync(coSo?.QcAnh, null);
            foreach (var noiDung in baiViet)
                await _donAnh.DonTheoHtmlAsync(noiDung, null);

            Success("Đã xóa cơ sở y tế.");
        }
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
            AnhBia = model.AnhBia ?? storedFacility?.AnhBia,
            Logo = model.LogoRemoved
                ? null
                : await ReadPreviewImageAsync(model.LogoFile, model.LogoUrlInput, model.Logo ?? storedFacility?.Logo),
            HienThiCongKhai = model.HienThiCongKhai
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

        foreach (var topic in topics.Where(x =>
                     !string.IsNullOrWhiteSpace(x.MaChuDe)
                     && NDCSKCB.AllowedLoaiND.Contains(x.MaChuDe, StringComparer.OrdinalIgnoreCase)))
        {
            contents.TryGetValue(topic.MaChuDe!, out var topicContent);
            contents[topic.MaChuDe!] = SynchronizeManagedFacilityLogo(topicContent, facility.Logo) ?? string.Empty;
        }

        ViewData["Title"] = "Xem trước cơ sở y tế";
        ViewData["CoSoYTe"] = facility;
        ViewData["MaCoSo"] = facility.MaCoSo;
        ViewData["Slug"] = facility.Slug;
        ViewData["TenCoSo"] = facility.TenCoSo ?? "Cơ sở y tế";
        ViewData["DiaChi"] = facility.DiaChi ?? "Đang cập nhật";
        ViewData["Type"] = model.LoaiCS ?? "benhvien";
        ViewData["Img"] = facility.AnhBia;
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
        var previewItems = await LoadHomePreviewAdsAsync();
        var previewImage = await ReadPreviewImageAsync(
            model.QuangCaoImageFile,
            model.QcAnhUrlInput,
            model.QcAnh ?? storedFacility?.QcAnh);
        var previewContent = string.IsNullOrWhiteSpace(model.QcNoiDung)
            ? storedFacility?.QcNoiDung
            : model.QcNoiDung;

        if (model.QcSoTienDaTra.GetValueOrDefault() > 0
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

        return View("~/Views/Home/ThongTinBenhNhan.cshtml", new List<DotKham>());
    }

    private async Task<string?> ResolveAdvertisingImageAsync(
        CoSoYTeEditViewModel model,
        string? existingImage)
    {
        if (model.QcSoTienDaTra.GetValueOrDefault() <= 0)
            return null;

        return await ResolveImageAsync(
            model.QuangCaoImageFile,
            model.QcAnhUrlInput,
            existingImage,
            model.MaCoSo,
            KhoAnh.ThuMucQuangCao,
            nameof(model.QuangCaoImageFile));
    }

    private async Task<string?> ResolveImageAsync(
        IFormFile? imageFile,
        string? urlInput,
        string? fallback,
        string? maCoSo,
        string thuMuc,
        string propertyName)
    {
        // Tai len that bai (FTP hong) thi GIU anh cu, dung de cot ve null.
        if (imageFile != null)
            return await SaveImageAsync(imageFile, maCoSo, thuMuc, propertyName) ?? fallback;

        return string.IsNullOrWhiteSpace(urlInput) ? fallback : urlInput.Trim();
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
            ModelState.AddModelError(nameof(CoSoYTeEditViewModel.QcSoTienDaTra), "Số tiền quảng cáo phải là số nguyên VNĐ.");
    }

    /// <summary>
    /// Tai anh len kho FTP dung chung, tra ve URL de cat vao cot DB.
    ///
    /// Hai loai that bai KHAC HAN nhau, dung gop lam mot:
    ///  - Sai kich thuoc / sai duoi tep la LOI NGUOI DUNG  -> ModelState, chan Luu.
    ///  - FTP hong la SU CO HA TANG                        -> canh bao, VAN Luu,
    ///    va nguoi goi phai giu lai gia tri cu (xem ResolveImageAsync). Truoc ban
    ///    va, ham nay tra null khi loi con noi goi thi gan thang vao cot => FTP
    ///    hong mot lan la XOA TRANG logo trong DB.
    /// </summary>
    private async Task<string?> SaveImageAsync(
        IFormFile imageFile,
        string? maCoSo,
        string thuMuc,
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

        try
        {
            var duongDanFtp = await _ftp.UploadFileAsync(imageFile, KhoAnh.ThuMucFtp(maCoSo, thuMuc));
            return KhoAnh.UrlTuDuongDanFtp(duongDanFtp);
        }
        catch (Exception)
        {
            _canhBao.Add("Chưa tải được ảnh lên máy chủ FTP, ảnh cũ được giữ nguyên.");
            return null;
        }
    }

    private static void Normalize(CoSoYTeEditViewModel model)
    {
        model.MaCoSo = model.MaCoSo?.Trim();
        model.Slug = string.IsNullOrWhiteSpace(model.Slug)
            ? null
            : model.Slug.Trim().ToLowerInvariant();
        model.TenCoSo = model.TenCoSo?.Trim();
        model.DiaChi = model.DiaChi?.Trim();
        model.LoaiCS = model.LoaiCS?.Trim().ToLowerInvariant();
        model.TGLamViec = model.TGLamViec?.Trim();
        model.NgayLamViec = model.NgayLamViec?.Trim();
        model.GioMoCua = model.GioMoCua?.Trim();
        model.GioDongCua = model.GioDongCua?.Trim();
        model.AnhBia = model.AnhBia?.Trim();
        model.KetNoi_UrlChuyenHuong = model.KetNoi_UrlChuyenHuong?.Trim();
        model.KetNoi_BaseUrlHIS = model.KetNoi_BaseUrlHIS?.Trim();
        model.Ftp_Host = model.Ftp_Host?.Trim();
        model.Ftp_ThuMucGoc = model.Ftp_ThuMucGoc?.Trim();
        // 🔴 Ba o bi mat KHONG duoc ep ve chuoi rong: rong va NULL o day cung mot
        // nghia "khong doi", va stored phan biet bang NULL. Ep "" la GHI DE mat khau
        // bang chuoi rong — dung kho ngay lap tuc.
        model.Ftp_TaiKhoan = RongThanhNull(model.Ftp_TaiKhoan);
        model.Ftp_MatKhau = RongThanhNull(model.Ftp_MatKhau);
        model.KetNoi_KhoaGoiHIS = RongThanhNull(model.KetNoi_KhoaGoiHIS);
        model.Logo = model.Logo?.Trim();
        model.LogoUrlInput = model.LogoUrlInput?.Trim();
        model.QcNoiDung = model.QcNoiDung?.Trim();
        model.QcAnh = model.QcAnh?.Trim();
        model.QcAnhUrlInput = model.QcAnhUrlInput?.Trim();
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

    private static string? SynchronizeManagedFacilityLogo(string? html, string? logoUrl)
    {
        var contentWithoutManagedLogo = ManagedFacilityLogoRegex.Replace(html ?? string.Empty, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(logoUrl))
            return string.IsNullOrWhiteSpace(contentWithoutManagedLogo) ? null : contentWithoutManagedLogo;

        var encodedUrl = WebUtility.HtmlEncode(logoUrl.Trim());
        var logoHtml = $"<div data-cskcb-facility-logo=\"true\" style=\"text-align:center;margin:0 0 16px\">"
            + $"<img data-cskcb-facility-logo-image=\"true\" src=\"{encodedUrl}\" alt=\"Logo cơ sở\" "
            + "style=\"display:inline-block;max-width:180px;width:auto;height:auto;object-fit:contain\"></div>";
        return logoHtml + contentWithoutManagedLogo;
    }

    private async Task<List<(long TopicId, string? Existing, string? Updated)>> BuildLogoSynchronizedContentsAsync(
        long idCoSo,
        CoSoYTeEditViewModel model,
        string? logoUrl,
        bool includeActiveEditorContent)
    {
        var topics = (await _db.DMChuDes.AsNoTracking()
                .OrderBy(x => x.ID)
                .ToListAsync())
            .Where(x => !string.IsNullOrWhiteSpace(x.MaChuDe)
                && NDCSKCB.AllowedLoaiND.Contains(x.MaChuDe, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var topicIds = topics.Select(x => x.ID).ToList();
        var existingContents = await _db.NDCSKCBs.AsNoTracking()
            .Where(x => x.IdCoSo == idCoSo && topicIds.Contains(x.IdChuDe))
            .OrderBy(x => x.Id)
            .ToListAsync();
        var existingByTopic = existingContents
            .GroupBy(x => x.IdChuDe)
            .ToDictionary(x => x.Key, x => x.First().NoiDung);
        var submittedContents = ParseTopicContents(model.TopicContentsJson);

        if (includeActiveEditorContent && model.TopicId > 0 && !submittedContents.ContainsKey(model.TopicId))
            submittedContents[model.TopicId] = model.NoiDung;

        var result = new List<(long TopicId, string? Existing, string? Updated)>();
        foreach (var topic in topics)
        {
            existingByTopic.TryGetValue(topic.ID, out var existing);
            var hasSubmitted = submittedContents.TryGetValue(topic.ID, out var submitted);
            if (!hasSubmitted && !existingByTopic.ContainsKey(topic.ID) && string.IsNullOrWhiteSpace(logoUrl))
                continue;

            var source = hasSubmitted ? submitted : existing;
            result.Add((topic.ID, existing, SynchronizeManagedFacilityLogo(source, logoUrl)));
        }

        return result;
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
            .Where(x => x.QcSoTienDaTra.GetValueOrDefault() > 0)
            .OrderByDescending(x => x.QcSoTienDaTra)
            .Take(5)
            .ToListAsync();
        // Dot A: noi dung / anh quang cao doc thang tren dong co so, khong con
        // bang con DM_CSKCB_QuangCao de ghep.
        return facilities.Select(facility => new TopCSKCBQC
        {
            TenCoSo = facility.TenCoSo ?? string.Empty,
            NoiDung = facility.QcNoiDung ?? string.Empty,
            Img = facility.QcAnh ?? facility.AnhBia ?? string.Empty
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

    // ====================== Kho phieu co so (ADR 0030) ======================
    //
    // 🔴 Dot A: bang HT_KhoFtpCoSo bi gop thang vao DM_CSKCB (cot Ftp_*), va hai
    // stored HT_KhoFtpCoSo_Save / HT_KhoaApiCoSo_Save bi bo. Duong GHI cau hinh kho
    // gio di chung mot luot voi DM_CSKCB_Save, nen o day khong con ham nap/ghi rieng.
    //
    // Ba cot Ftp_TaiKhoan / Ftp_MatKhau / KetNoi_KhoaGoiHIS la COT BI MAT: chung KHONG
    // duoc map vao thuc the EF DMCSKCB (59 cho doc bang nay qua EF, trang cong khai nap
    // tron thuc the). Hai he qua phai song chung:
    //   - Man Sua KHONG hien lai gia tri cu => o de trong, trong = "khong doi" (stored
    //     nhan NULL thi giu nguyen gia tri cu).
    //   - Nut "Thu ket noi" KHONG con doc duoc ban dang luu => admin phai GO DU
    //     tai khoan + mat khau moi thu duoc.

    /// <summary>
    /// Nut <i>Thu ket noi kho</i>. Day la cho DUY NHAT kiem duoc cau hinh truoc khi
    /// benh nhan bam mo: che do Tro duong khong co "luc nhan" nao ben cong ca —
    /// stored ben HIS ghi THANG vao HIS_CSKH, khong goi HTTP toi cong (chot 41).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThuKetNoiKho(
        long id, string? host, string? taiKhoan, string? matKhau, string? thuMucGoc)
    {
        // 🔴 Thu ĐUNG CAI DANG GO tren man, KHONG doc dong dang luu trong DB.
        // Ban dau lam nguoc, va hau qua la go mat khau sai vao o van bao "dat" —
        // vi no dang thu ban cu. Ca diem cua nut nay la bat sai TRUOC khi luu.
        //
        // Sau dot A dieu do thanh BAT BUOC chu khong con la lua chon: tai khoan va
        // mat khau la cot bi mat, may chu khong doc lai duoc qua EF. "De trong" gio
        // KHONG con nghia "giu cai cu" nua — trong la khong thu duoc.
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(taiKhoan)
            || string.IsNullOrWhiteSpace(matKhau))
            return Json(new { success = false, message = "Nhập đủ Máy chủ, Tài khoản và Mật khẩu rồi hãy thử." });

        var dat = await _khoCoSo.ThuKetNoiAsync(
            new ThongSoKho(host.Trim(), taiKhoan.Trim(), matKhau, thuMucGoc?.Trim()));

        if (!dat)
            return Json(new { success = false, message = "Chưa kết nối được tới kho của cơ sở. Kiểm tra lại máy chủ, tài khoản, mật khẩu." });

        // Co so chua duoc luu lan nao thi chua co IDCoSo de gan moc vao.
        if (id <= 0)
            return Json(new { success = true, luuTruoc = true, message = "Kết nối đạt. Bấm Lưu cơ sở để ghi nhận, rồi mới bật được kho." });

        // 🔴 Chố "thử đạt một đằng rồi lưu một đằng khác" phải bịt NGAY Ở ĐÂY, không giao
        // được cho tầng stored. Lý do: DM_CSKCB_Save nhận @Ftp_TaiKhoan/@Ftp_MatKhau = NULL
        // theo nghĩa "giữ nguyên giá trị cũ" (màn Admin không hiện lại mật khẩu nên không gửi
        // lại) ⇒ nó KHÔNG phân biệt được "không đổi" với "đổi rồi nhưng chưa lưu".
        // Hậu quả nếu đóng dấu vô điều kiện: admin gõ host/tài khoản/mật khẩu mới → Thử đạt
        // → Ftp_NgayThuDat ghi ngay → admin bỏ trang (hoặc xoá ô mật khẩu rồi Lưu) ⇒ Ftp_Active
        // bật được với bộ thông số CHƯA TẮNG thử; tệ hơn, cơ sở chưa lưu lần nào thì Ftp_Host
        // vẫn NULL ⇒ KhoCoSoService.LayKhoAsync ném "Cơ sở chưa khai báo kho phiếu" cho MỌI
        // tài liệu bệnh nhân. Đây chính là ý của guard `trungVoiBanLuu` cũ, khôi phục lại.
        if (!await TrungVoiKhoDangLuuAsync(id, host, taiKhoan, matKhau, thuMucGoc))
            return Json(new
            {
                success = true,
                luuTruoc = true,
                message = "Kết nối đạt, nhưng đây là thông số chưa lưu. Bấm Lưu thay đổi rồi thử lại để ghi nhận, sau đó mới bật được kho."
            });

        var ghi = await _adminStoredProcedures.GhiNhanThuDatKhoFtpAsync(id);
        return ghi.Succeeded
            ? Json(new { success = true, message = "Kết nối kho đạt. Đã ghi nhận mốc thử đạt — bật được kho rồi." })
            : Json(new { success = false, message = ghi.Message ?? "Kết nối đạt nhưng không ghi nhận được." });
    }

    /// <summary>
    /// Bộ thông số vừa gõ trên màn có <b>trùng đúng bản đang lưu</b> trong DB không.
    ///
    /// <para>
    /// 🔴 So sánh ĐẶT TRONG CÂU SQL chứ không đọc giá trị về C#: <c>Ftp_TaiKhoan</c> /
    /// <c>Ftp_MatKhau</c> là <b>cột bí mật</b> (lưu thô, cố ý) — hợp đồng đợt A chỉ cho 3 chỗ đọc
    /// chúng. Kéo về controller chỉ để <c>==</c> là thêm chỗ thứ 4 một cách vô ích; ở đây SQL chỉ
    /// trả về đúng một bit 0/1, mật khẩu không rời khỏi máy chủ.
    /// </para>
    /// <para>
    /// Dùng collation <c>Latin1_General_BIN2</c>: collation mặc định KHÔNG phân biệt hoa thường,
    /// mà mật khẩu FTP thì có — so lỏng là đóng dấu cho một chuỗi khác với bản đang lưu.
    /// </para>
    /// </summary>
    private async Task<bool> TrungVoiKhoDangLuuAsync(
        long idCoSo, string? host, string? taiKhoan, string? matKhau, string? thuMucGoc)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT CASE WHEN
        ISNULL(Ftp_Host,      N'') COLLATE Latin1_General_BIN2 = @host
    AND ISNULL(Ftp_TaiKhoan,  N'') COLLATE Latin1_General_BIN2 = @taiKhoan
    AND ISNULL(Ftp_MatKhau,   N'') COLLATE Latin1_General_BIN2 = @matKhau
    AND ISNULL(Ftp_ThuMucGoc, N'') COLLATE Latin1_General_BIN2 = @thuMucGoc
    THEN 1 ELSE 0 END
FROM dbo.DM_CSKCB
WHERE ID = @idCoSo;";

        ThemThamSo(cmd, "@idCoSo", System.Data.DbType.Int64, idCoSo);
        ThemThamSo(cmd, "@host", System.Data.DbType.String, (host ?? "").Trim());
        ThemThamSo(cmd, "@taiKhoan", System.Data.DbType.String, (taiKhoan ?? "").Trim());
        ThemThamSo(cmd, "@matKhau", System.Data.DbType.String, matKhau ?? "");
        ThemThamSo(cmd, "@thuMucGoc", System.Data.DbType.String, (thuMucGoc ?? "").Trim());

        var ketQua = await cmd.ExecuteScalarAsync();
        return ketQua != null && ketQua != DBNull.Value && Convert.ToInt32(ketQua) == 1;
    }

    private static void ThemThamSo(
        System.Data.Common.DbCommand cmd, string ten, System.Data.DbType kieu, object giaTri)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = ten;
        p.DbType = kieu;
        p.Value = giaTri;
        cmd.Parameters.Add(p);
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
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file, [FromForm] string? maCoSo)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Không có file nào được tải lên.");
        }

        var url = await SaveImageAsync(file, maCoSo, KhoAnh.ThuMucNoiDung, "file");
        if (url == null)
        {
            // Gom ca hai nguon loi: ModelState (sai kich thuoc / sai duoi tep) va
            // _canhBao (FTP hong). Chi lay ModelState thi luc FTP hong se tra ve
            // chuoi RONG, TinyMCE bao "Tai anh that bai" ma khong noi vi sao.
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                .Concat(_canhBao)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (errors.Count == 0) errors.Add("Không tải được ảnh lên máy chủ FTP.");
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
            Tinh = entity.Tinh,
            PhuongXa = entity.PhuongXa,
            SDT = entity.SDT,
            Email = entity.Email,
            SelectedNhomCSId = entity.IdNhomCS,
            HienThiCongKhai = entity.HienThiCongKhai,
            AnhBia = entity.AnhBia,
            Logo = entity.Logo,
            IDCongTy = entity.IDCongTy,

            // Quang cao: gop tu bang con DM_CSKCB_QuangCao vao thang cot cua co so.
            QcSoTienDaTra = entity.QcSoTienDaTra,
            QcNoiDung = entity.QcNoiDung,
            QcAnh = entity.QcAnh,

            // Ket noi HIS: gop tu bang con DM_DoiTacApi.
            KetNoi_UrlChuyenHuong = entity.KetNoi_UrlChuyenHuong,
            KetNoi_BaseUrlHIS = entity.KetNoi_BaseUrlHIS,
            KetNoi_Active = entity.KetNoi_Active,

            // Kho FTP: gop tu bang con HT_KhoFtpCoSo.
            // 🔴 Ftp_TaiKhoan / Ftp_MatKhau / KetNoi_KhoaGoiHIS CO Y de trong — cot bi
            // mat, khong map vao EF va khong duoc phun ra HTML. Trong = "khong doi".
            Ftp_Host = entity.Ftp_Host,
            Ftp_ThuMucGoc = entity.Ftp_ThuMucGoc,
            Ftp_Active = entity.Ftp_Active,
            Ftp_NgayThuDat = entity.Ftp_NgayThuDat
        };

    /// <summary>Chuoi rong/trang = KHONG nhap, tra NULL de stored hieu la "giu nguyen".</summary>
    private static string? RongThanhNull(string? giaTri) =>
        string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Trim();

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


