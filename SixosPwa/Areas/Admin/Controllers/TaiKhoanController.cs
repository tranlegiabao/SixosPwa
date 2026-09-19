using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class TaiKhoanController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _adminStoredProcedures;

    public TaiKhoanController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
    }

    public async Task<IActionResult> Index(
        string? loaiCS,
        string? cccd,
        string? sdt,
        string? maBN,
        string? q,
        string? role,
        string? loc,
        int page = 1,
        int pageSize = 50)
    {
        page = SafePage(page);
        pageSize = pageSize is 20 or 50 or 100 or 500 ? pageSize : 50;

        // Đồng bộ ô tìm kiếm số điện thoại
        if (string.IsNullOrWhiteSpace(sdt) && !string.IsNullOrWhiteSpace(q))
        {
            sdt = q;
        }

        var danhSachCoSo = await _db.DMCSKCBs.AsNoTracking().OrderBy(x => x.TenCoSo).ToListAsync();
        var danhMucGioiTinh = LayDanhMucGioiTinh();

        var daLoc = !string.IsNullOrWhiteSpace(loc) ||
                    !string.IsNullOrWhiteSpace(loaiCS) ||
                    !string.IsNullOrWhiteSpace(cccd) ||
                    !string.IsNullOrWhiteSpace(sdt) ||
                    !string.IsNullOrWhiteSpace(maBN) ||
                    !string.IsNullOrWhiteSpace(role) ||
                    !string.IsNullOrWhiteSpace(q);

        if (!daLoc)
        {
            var emptyModel = new TaiKhoanListViewModel
            {
                Items = Array.Empty<TaiKhoan>(),
                HoSoTheoTaiKhoan = new Dictionary<long, List<HoSoBenhNhanItemViewModel>>(),
                DanhSachCoSo = danhSachCoSo,
                DanhMucGioiTinh = danhMucGioiTinh,
                Query = q,
                Role = role,
                LoaiCS = loaiCS,
                CCCD = cccd,
                SDT = sdt,
                MaBN = maBN,
                Page = 1,
                PageSize = pageSize,
                TotalItems = 0,
                DaLoc = false
            };

            if (IsAjaxRequest())
            {
                Response.Headers["X-Total-Pages"] = "0";
                Response.Headers["X-Current-Page"] = "1";
                Response.Headers["X-Total-Items"] = "0";
                Response.Headers["X-Page-Size"] = pageSize.ToString();
                Response.Headers["X-Da-Loc"] = "0";
                return PartialView("_TaiKhoanTableBody", emptyModel);
            }

            return View(emptyModel);
        }

        var (items, hoSoTheoTaiKhoan, total) = await _adminStoredProcedures.LocTaiKhoanAsync(
            page, pageSize, sdt, cccd, maBN, role, loaiCS);

        var model = new TaiKhoanListViewModel
        {
            Items = items.Select(x =>
            {
                x.Role = NormalizeRole(x.Role);
                return x;
            }).ToList(),
            HoSoTheoTaiKhoan = hoSoTheoTaiKhoan,
            DanhSachCoSo = danhSachCoSo,
            DanhMucGioiTinh = danhMucGioiTinh,
            Query = q,
            Role = role,
            LoaiCS = loaiCS,
            CCCD = cccd,
            SDT = sdt,
            MaBN = maBN,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            DaLoc = true
        };

        if (IsAjaxRequest())
        {
            Response.Headers["X-Total-Pages"] = model.TotalPages.ToString();
            Response.Headers["X-Current-Page"] = model.Page.ToString();
            Response.Headers["X-Total-Items"] = model.TotalItems.ToString();
            Response.Headers["X-Page-Size"] = model.PageSize.ToString();
            Response.Headers["X-Da-Loc"] = "1";
            return PartialView("_TaiKhoanTableBody", model);
        }

        return View(model);
    }

    [HttpGet]
    public Task<IActionResult> TaiTrang(
        string? loaiCS,
        string? cccd,
        string? sdt,
        string? maBN,
        string? q,
        string? role,
        string? loc,
        int page = 1,
        int pageSize = 50)
    {
        return Index(loaiCS, cccd, sdt, maBN, q, role, loc, page, pageSize);
    }

    private bool IsAjaxRequest() =>
        Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        Request.Query.ContainsKey("isAjax");

    /// <summary>
    /// 🔴 Đợt A: bảng <c>DM_GioiTinh</c> đã bị xóa (chỉ còn <c>CHECK</c> trên
    /// <c>DM_BenhNhan.GioiTinh</c>), nên danh mục này là HẰNG trong C# — không còn
    /// câu SQL thô nào đọc bảng đó nữa. Mã giữ nguyên mã cũ của HIS để dữ liệu đã
    /// lưu không phải dịch lại: <c>1 = Nam</c>, <c>2 = Nữ</c>, <c>3 = Không xác định</c>.
    /// </summary>
    private static readonly List<DMGioiTinh> DanhMucGioiTinhCoDinh = new()
    {
        new DMGioiTinh { MaGioiTinh = "1", TenGioiTinh = "Nam" },
        new DMGioiTinh { MaGioiTinh = "2", TenGioiTinh = "Nữ" },
        new DMGioiTinh { MaGioiTinh = "3", TenGioiTinh = "Không xác định" }
    };

    private static List<DMGioiTinh> LayDanhMucGioiTinh() => DanhMucGioiTinhCoDinh;

    [HttpGet]
    public IActionResult Create() => View(new TaiKhoanEditViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaiKhoanEditViewModel model)
    {
        model.SDT = model.SDT.Trim();
        model.Role = NormalizeRole(model.Role.Trim());
        ValidateRole(model.Role);

        if (ModelState.IsValid && await _db.TaiKhoans.AnyAsync(x => x.SDT == model.SDT))
            ModelState.AddModelError(nameof(model.SDT), "Số điện thoại đã tồn tại.");

        if (!ModelState.IsValid) return View(model);

        // MatKhauNoiBo de null — phan bam chua thi hanh (Dinh chinh ADR 0009).
        var (result, _) = await _adminStoredProcedures.SaveTaiKhoanAsync(
            0, model.SDT, null, model.Role, null);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.SDT), result.Message ?? "Không thể tạo tài khoản.");
            return View(model);
        }

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
        model.Role = NormalizeRole(model.Role.Trim());
        ValidateRole(model.Role);

        var entity = await _db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.Id);
        if (entity == null) return NotFound();

        var currentPhone = User.Identity?.Name;
        if (entity.SDT == currentPhone && model.Role != "Admin")
            ModelState.AddModelError(nameof(model.Role), "Không thể tự hạ quyền tài khoản Admin đang đăng nhập.");

        if (NormalizeRole(entity.Role) == "Admin" && model.Role != "Admin"
            && await _db.TaiKhoans.CountAsync(x => x.Role == "Admin") <= 1)
            ModelState.AddModelError(nameof(model.Role), "Không thể hạ quyền Admin cuối cùng của hệ thống.");

        if (!ModelState.IsValid) return View(model);

        var (result, _) = await _adminStoredProcedures.SaveTaiKhoanAsync(
            model.Id,
            entity.SDT,
            entity.Email,
            NormalizeRole(model.Role),
            null);
        if (!result.Succeeded)
        {
            if (result.Code == 3) return NotFound();
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể cập nhật tài khoản.");
            return View(model);
        }

        Success("Đã cập nhật vai trò tài khoản.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateRole(string role)
    {
        if (!AllowedRoles.Contains(role))
            ModelState.AddModelError(nameof(TaiKhoanEditViewModel.Role), "Vai trò không hợp lệ.");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CapNhatHoSo([FromBody] CapNhatHoSoAdminRequest req)
    {
        if (req == null || req.Id <= 0 || string.IsNullOrWhiteSpace(req.TenBN))
            return Json(new { success = false, isWarning = true, message = "Vui lòng nhập đầy đủ họ tên hồ sơ." });

        var cccdMoi = (req.CCCD ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cccdMoi))
            return Json(new { success = false, isWarning = true, message = "Vui lòng nhập số căn cước công dân." });

        if (req.NgaySinh.HasValue && req.NgaySinh.Value.Date > DateTime.Today)
            return Json(new { success = false, isWarning = true, message = "Ngày sinh không được lớn hơn ngày hiện tại." });

        var bn = await _db.BenhNhans.FirstOrDefaultAsync(x => x.Id == req.Id);
        if (bn == null)
            return Json(new { success = false, message = "Không tìm thấy hồ sơ bệnh nhân." });

        // 🔴 KIỂM TRA: Nếu hồ sơ đang có mã bệnh nhân tại cơ sở thì KHÔNG cho sửa thông tin. Phải gỡ nối trước!
        // Dot 1B: chinh dong do LA ho so tai co so, giu dung mot ma (ADR 0032).
        var coSoRecord = bn;

        if (!string.IsNullOrEmpty(coSoRecord?.MaBN))
        {
            return Json(new { success = false, isWarning = true, message = "Hồ sơ đang liên kết mã bệnh nhân. Vui lòng bấm 'Gỡ đồng bộ' trước khi chỉnh sửa thông tin." });
        }

        var laCccdKhongCo = cccdMoi is "11111111111" or "111111111111";

        if (laCccdKhongCo && (!req.NgaySinh.HasValue || req.NgaySinh.Value.Date == new DateTime(1900, 1, 1)))
        {
            return Json(new { success = false, isWarning = true, message = "Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân." });
        }

        // Kiểm tra trùng lặp:
        if (!laCccdKhongCo)
        {
            var trungCccd = await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.CCCD == cccdMoi && x.Id != bn.Id);
            if (trungCccd)
            {
                return Json(new { success = false, isWarning = true, message = "Số căn cước này đã có hồ sơ khác tại cơ sở này." });
            }
        }
        else if (laCccdKhongCo && req.NgaySinh.HasValue && !string.IsNullOrEmpty(req.GioiTinh))
        {
            var tenKd = ChuanHoaTen.BoDau(req.TenBN);
            var trungNhanThan = await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.HoTenKhongDau == tenKd
                            && x.NgaySinh.HasValue && x.NgaySinh.Value.Date == req.NgaySinh.Value.Date
                            && x.GioiTinh == req.GioiTinh
                            && x.Id != bn.Id
                            && x.IdCoSo != null && x.IdCoSo == bn.IdCoSo);
            if (trungNhanThan)
            {
                return Json(new { success = false, isWarning = true, message = "Hồ sơ với thông tin này (Họ tên, ngày sinh, giới tính) đã có hồ sơ khác tại cơ sở này." });
            }
        }

        bn.TenBN = req.TenBN.Trim();
        bn.CCCD = cccdMoi;
        bn.SDT = string.IsNullOrWhiteSpace(req.SDT) ? null : req.SDT.Trim();
        bn.NgaySinh = req.NgaySinh;
        bn.GioiTinh = req.GioiTinh;
        bn.DiaChi = string.IsNullOrWhiteSpace(req.DiaChi) ? null : req.DiaChi.Trim();
        bn.HoTenKhongDau = ChuanHoaTen.BoDau(req.TenBN);

        // ───── Cơ sở khám chữa bệnh & Mã bệnh nhân (DM_BenhNhanCoSo) ─────
        var newMaBN = string.IsNullOrWhiteSpace(req.MaBN) ? null : req.MaBN.Trim();
        var idCoSoDich = req.IdCoSo ?? coSoRecord?.IdCoSo ?? 0;
        long? idCoSoHienThi = idCoSoDich > 0 ? idCoSoDich : coSoRecord?.IdCoSo;
        var maBNSauKhiLuu = coSoRecord?.MaBN;
        long? idHoSoCoSoResult = coSoRecord?.Id;
        string? canhBao = null;

        // Nếu có chọn cơ sở đích mà hồ sơ chưa có dòng cơ sở nào với cơ sở này:
        // Gọi tạo dòng tự khai trước
        if (idCoSoDich > 0)
        {
            var dongHienTai = await _db.BenhNhans
                .FirstOrDefaultAsync(x => x.Id == bn.Id && x.IdCoSo == idCoSoDich);

            if (dongHienTai == null)
            {
                var (kqTuKhai, idMoi) = await _adminStoredProcedures.TaoHoSoTuKhaiAsync(bn.Id, idCoSoDich);
                if (kqTuKhai.Succeeded)
                {
                    idHoSoCoSoResult = idMoi;
                }
            }
            else
            {
                idHoSoCoSoResult = dongHienTai.Id;
            }
        }

        // Nếu có nhập mã bệnh nhân mới:
        if (!string.IsNullOrEmpty(newMaBN))
        {
            if (idCoSoDich <= 0)
            {
                return Json(new
                {
                    success = false,
                    isWarning = true,
                    message = "Vui lòng chọn cơ sở khám chữa bệnh để gán mã bệnh nhân."
                });
            }

            var (ketQuaMa, idCoSoMoi) = await _adminStoredProcedures.SaveBenhNhanCoSoAsync(
                bn.Id, idCoSoDich, newMaBN, moCuaTaiLieu: true);

            // Code 3 = ho so DANG NOI mot ma KHAC. Hang rao chot 47 / ADR 0032:
            // cua Luu ho so CO Y tu choi doi ma da co. Nhung man nay ten la
            // "nhap ma benh nhan moi" — nguoi bam la bo phan ho tro va DA co y
            // doi — nen day la cho duy nhat duoc di tiep bang CUA DOI MA (co ghi
            // so). Bon noi goi _Save con lai KHONG duoc mo duong nay.
            if (ketQuaMa.Code == 3)
            {
                (ketQuaMa, idCoSoMoi) = await _adminStoredProcedures.DoiMaBenhNhanCoSoAsync(
                    bn.Id, idCoSoDich, newMaBN, lyDo: "Man Admin > Tai khoan");
            }

            if (!ketQuaMa.Succeeded)
            {
                return Json(new
                {
                    success = false,
                    message = ketQuaMa.Message ?? "Không nối được mã bệnh nhân tại cơ sở."
                });
            }

            idHoSoCoSoResult = idCoSoMoi > 0 ? idCoSoMoi : idHoSoCoSoResult;
            maBNSauKhiLuu = newMaBN;
            idCoSoHienThi = idCoSoDich;

            // 🔴 Khoi "dong bo dong mo coi" cu da bi go o dot A.
            // No tim tai lieu / dot kham CHUA gan IDBenhNhanCoSo roi doi chieu bang cot
            // MaBN nam tren chinh hai bang do. Ca hai cot MaBN nay da bi xoa
            // (QL_TaiLieuBenhNhan.MaBN, QL_DotKham.MaBN) va ma benh nhan gio chi con
            // suy ra duoc QUA IDBenhNhanCoSo — tuc la phai dung chinh cai dang thieu
            // de tim no. Khong con manh moi nao de noi lai, ma tu dot A moi duong ghi
            // deu bat buoc co IDBenhNhanCoSo ngay tu dau nen cung khong sinh them dong mo coi.
        }
        else if (!string.IsNullOrEmpty(coSoRecord?.MaBN))
        {
            canhBao = "Mã bệnh nhân giữ nguyên — muốn thao mã thì bấm nút Gỡ đồng bộ, "
                    + "để trống ô rồi Lưu không gỡ được.";
        }

        await _db.SaveChangesAsync();

        var tenCoSo = idCoSoHienThi == null
            ? null
            : await _db.DMCSKCBs.AsNoTracking()
                .Where(x => x.Id == idCoSoHienThi.Value)
                .Select(x => x.TenCoSo)
                .FirstOrDefaultAsync();

        // Kiểm tra xem hồ sơ này có phải hồ sơ chính của tài khoản không
        bool isPrimary = false;
        if (bn.IdCoSo.HasValue)
        {
            var tk = await _db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(t => t.Id == bn.IdCoSo.Value);
            if (tk != null)
            {
                // 🔴 Dot A: cot HT_TaiKhoan.IDBenhNhan da bi xoa (thi hanh not ADR 0019).
                // Quan he gio la 1-N theo chieu DM_BenhNhan.IdTaiKhoan, nen "ho so chinh"
                // chi con mot nghia doc duoc: tai khoan nay dang quan DUNG MOT ho so.
                var countHoSo = await _db.BenhNhans.CountAsync(x => x.IdCoSo == tk.Id);
                if (countHoSo == 1)
                {
                    isPrimary = true;
                }
            }
        }

        return Json(new
        {
            success = true,
            message = canhBao == null
                ? "Đã lưu thông tin hồ sơ thành công."
                : "Đã lưu thông tin hồ sơ. " + canhBao,
            data = new
            {
                id = bn.Id,
                idTaiKhoan = bn.IdCoSo,
                tenBN = bn.TenBN,
                cccd = bn.CCCD,
                sdt = bn.SDT ?? "",
                ngaySinh = bn.NgaySinh?.ToString("yyyy-MM-dd") ?? "",
                ngaySinhVn = bn.NgaySinh?.ToString("dd/MM/yyyy") ?? "—",
                gioiTinh = bn.GioiTinh ?? "1",
                gioiTinhVn = bn.GioiTinh == "1" ? "Nam" : (bn.GioiTinh == "2" ? "Nữ" : "Khác"),
                diaChi = bn.DiaChi ?? "",
                maBN = maBNSauKhiLuu ?? "",
                idCoSo = idCoSoHienThi,
                idHoSoCoSo = idHoSoCoSoResult,
                tenCoSo = tenCoSo ?? "",
                isPrimary = isPrimary
            }
        });
    }

    /// <summary>
    /// *Go noi* mot ma khoi mot ho so — cua duy nhat, va do ADMIN bam.
    ///
    /// <para>
    /// 🔴 KHONG xoa dong bang EF. Thu tuc <c>dbo.DM_BenhNhanCoSo_GoNoi</c> con phai
    /// sao luu tai lieu + dot kham sang <c>bak.GoNoi_*_V001</c> roi moi xoa, va phai de
    /// lai mot dong TU KHAI de ho so "tut ve *Ho so tu khai*" chu khong bien mat khoi co
    /// so. Xoa thang bang EF thi mat het ba viec do, ma khoa ngoai NO_ACTION cung chan
    /// khong cho xoa khi con tai lieu — nen duong cu vua sai vua se gay 500.
    /// </para>
    ///
    /// <para>
    /// Thu tuc chan theo CHU SO HUU ho so (<c>@IDTaiKhoan</c>) chu khong theo nguoi dang
    /// bam, nen o day truyen tai khoan cua chinh ho so. Quyen cua admin da duoc canh cua
    /// Area Admin giu — khong noi hai lop chan vao lam mot.
    /// </para>
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GoNoiHoSo([FromBody] GoNoiHoSoAdminRequest req)
    {
        if (req == null || req.IdHoSoCoSo <= 0)
            return Json(new { success = false, message = "Không xác định được dòng hồ sơ tại cơ sở cần gỡ." });

        var dong = await _db.BenhNhans.AsNoTracking()
            .Where(x => x.Id == req.IdHoSoCoSo)
            .Select(x => new { x.Id, IdBenhNhan = x.Id, x.MaBN })
            .FirstOrDefaultAsync();

        if (dong == null)
            return Json(new { success = false, message = "Không tìm thấy hồ sơ tại cơ sở." });

        if (string.IsNullOrEmpty(dong.MaBN))
            return Json(new { success = false, message = "Hồ sơ này chưa nối mã nào nên không có gì để gỡ." });

        // Dot 1B: "chu so huu" la cap (SDT x co so) cua chinh dong ho so do
        // (ADR 0034). Khu Admin di duong nay thay mat benh nhan nen lay SDT +
        // co so tu chinh dong, khong hoi phien.
        var neo = await _db.BenhNhans.AsNoTracking()
            .Where(b => b.Id == dong.Id)
            .Select(b => new { b.SDT, b.IdCoSo })
            .FirstOrDefaultAsync();

        if (neo is null || string.IsNullOrWhiteSpace(neo.SDT) || neo.IdCoSo is null)
        {
            return Json(new
            {
                success = false,
                message = "Hồ sơ này chưa có số điện thoại hoặc chưa gắn cơ sở nên chưa gỡ đồng bộ được."
            });
        }

        var ketQua = await _adminStoredProcedures.GoNoiAsync(req.IdHoSoCoSo, neo.SDT, neo.IdCoSo.Value);

        return Json(new
        {
            success = ketQua.Succeeded,
            message = ketQua.Succeeded
                ? $"Đã gỡ mã {dong.MaBN}. Tài liệu và đợt khám đi kèm mã này đã được sao lưu rồi gỡ theo."
                : (ketQua.Message ?? "Không gỡ đồng bộ được.")
        });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> XoaHoSo([FromBody] XoaHoSoAdminRequest req)
    {
        if (req == null || req.Id <= 0)
            return Json(new { success = false, message = "ID hồ sơ không hợp lệ." });

        var bn = await _db.BenhNhans.FirstOrDefaultAsync(x => x.Id == req.Id);
        if (bn == null)
            return Json(new { success = false, message = "Không tìm thấy hồ sơ bệnh nhân." });

        var cosos = await _db.BenhNhans.Where(x => x.Id == bn.Id).ToListAsync();
        if (cosos.Any())
        {
            _db.BenhNhans.RemoveRange(cosos);
            await _db.SaveChangesAsync();
        }

        _db.BenhNhans.Remove(bn);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Đã xóa hồ sơ bệnh nhân thành công." });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TaoHoSo([FromBody] TaoHoSoAdminRequest req)
    {
        if (req == null || req.IdTaiKhoan <= 0 || string.IsNullOrWhiteSpace(req.TenBN))
            return Json(new { success = false, isWarning = true, message = "Vui lòng nhập họ tên hồ sơ." });

        var cccdMoi = (req.CCCD ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cccdMoi))
            return Json(new { success = false, isWarning = true, message = "Vui lòng nhập số căn cước công dân." });

        if (req.NgaySinh.HasValue && req.NgaySinh.Value.Date > DateTime.Today)
            return Json(new { success = false, isWarning = true, message = "Ngày sinh không được lớn hơn ngày hiện tại." });

        var laCccdKhongCo = cccdMoi is "11111111111" or "111111111111";

        if (laCccdKhongCo && (!req.NgaySinh.HasValue || req.NgaySinh.Value.Date == new DateTime(1900, 1, 1)))
        {
            return Json(new { success = false, isWarning = true, message = "Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân." });
        }

        if (!laCccdKhongCo)
        {
            var trungCccd = await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.CCCD == cccdMoi);
            if (trungCccd)
            {
                return Json(new { success = false, isWarning = true, message = "Số căn cước này đã có hồ sơ khác tại cơ sở này." });
            }
        }
        else if (laCccdKhongCo && req.NgaySinh.HasValue && !string.IsNullOrEmpty(req.GioiTinh))
        {
            var tenKd = ChuanHoaTen.BoDau(req.TenBN);
            var trungNhanThan = await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.HoTenKhongDau == tenKd
                            && x.NgaySinh.HasValue && x.NgaySinh.Value.Date == req.NgaySinh.Value.Date
                            && x.GioiTinh == req.GioiTinh
);
            if (trungNhanThan)
            {
                return Json(new { success = false, isWarning = true, message = "Hồ sơ với thông tin này (Họ tên, ngày sinh, giới tính) đã có hồ sơ khác tại cơ sở này." });
            }
        }

        var bn = new SixosPwa.Models.BenhNhan
        {
            // Dot 1B: khong con cot IDTaiKhoan; ho so thuoc ve cap (SDT x co so).
            TenBN = req.TenBN.Trim(),
            CCCD = cccdMoi,
            SDT = string.IsNullOrWhiteSpace(req.SDT) ? null : req.SDT.Trim(),
            NgaySinh = req.NgaySinh,
            GioiTinh = req.GioiTinh ?? "1",
            DiaChi = string.IsNullOrWhiteSpace(req.DiaChi) ? null : req.DiaChi.Trim(),
            HoTenKhongDau = ChuanHoaTen.BoDau(req.TenBN),
            NgayTao = DateTime.Now
        };

        _db.BenhNhans.Add(bn);
        await _db.SaveChangesAsync();

        long? idHoSoCoSoMoi = null;
        string? tenCoSoMoi = null;

        if (req.IdCoSo.HasValue && req.IdCoSo.Value > 0)
        {
            var (kqTuKhai, idMoi) = await _adminStoredProcedures.TaoHoSoTuKhaiAsync(bn.Id, req.IdCoSo.Value);
            if (kqTuKhai.Succeeded)
            {
                idHoSoCoSoMoi = idMoi;
            }

            tenCoSoMoi = await _db.DMCSKCBs.AsNoTracking()
                .Where(x => x.Id == req.IdCoSo.Value)
                .Select(x => x.TenCoSo)
                .FirstOrDefaultAsync();
        }

        return Json(new
        {
            success = true,
            message = "Đã tạo hồ sơ mới thành công.",
            data = new
            {
                id = bn.Id,
                idTaiKhoan = bn.IdCoSo,
                tenBN = bn.TenBN,
                cccd = bn.CCCD,
                sdt = bn.SDT ?? "",
                ngaySinh = bn.NgaySinh?.ToString("yyyy-MM-dd") ?? "",
                ngaySinhVn = bn.NgaySinh?.ToString("dd/MM/yyyy") ?? "—",
                gioiTinh = bn.GioiTinh ?? "1",
                gioiTinhVn = bn.GioiTinh == "1" ? "Nam" : (bn.GioiTinh == "2" ? "Nữ" : "Khác"),
                diaChi = bn.DiaChi ?? "",
                idCoSo = req.IdCoSo,
                idHoSoCoSo = idHoSoCoSoMoi,
                tenCoSo = tenCoSoMoi ?? ""
            }
        });
    }
}
