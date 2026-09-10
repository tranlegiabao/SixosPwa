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
        int page = 1)
    {
        page = SafePage(page);
        var query = _db.TaiKhoans.AsNoTracking().AsQueryable();

        // Đồng bộ ô tìm kiếm số điện thoại
        if (string.IsNullOrWhiteSpace(sdt) && !string.IsNullOrWhiteSpace(q))
        {
            sdt = q;
        }

        // 1. Lọc theo Số điện thoại
        if (!string.IsNullOrWhiteSpace(sdt))
        {
            sdt = sdt.Trim();
            var phonesMatching = await _db.BenhNhans.AsNoTracking()
                .Where(b => b.SDT != null && b.SDT.Contains(sdt))
                .Select(b => b.IdTaiKhoan)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToListAsync();

            query = query.Where(x => x.SDT.Contains(sdt) || phonesMatching.Contains(x.Id));
        }

        // 2. Lọc theo CCCD
        if (!string.IsNullOrWhiteSpace(cccd))
        {
            cccd = cccd.Trim();
            var patientMatchingCccd = _db.BenhNhans.AsNoTracking().Where(b => b.CCCD.Contains(cccd));
            var accountIdsFromCccd = await patientMatchingCccd
                .Where(b => b.IdTaiKhoan != null)
                .Select(b => b.IdTaiKhoan!.Value)
                .Distinct()
                .ToListAsync();
            var phonesFromCccd = await patientMatchingCccd
                .Where(b => !string.IsNullOrEmpty(b.SDT))
                .Select(b => b.SDT!)
                .Distinct()
                .ToListAsync();
            var legacyIdsFromCccd = await patientMatchingCccd
                .Select(b => (long?)b.Id)
                .Distinct()
                .ToListAsync();

            query = query.Where(x =>
                accountIdsFromCccd.Contains(x.Id) ||
                (x.IdBenhNhan != null && legacyIdsFromCccd.Contains(x.IdBenhNhan)) ||
                (!string.IsNullOrEmpty(x.SDT) && phonesFromCccd.Contains(x.SDT)));
        }

        // 3. Lọc theo Mã bệnh nhân tại cơ sở
        if (!string.IsNullOrWhiteSpace(maBN))
        {
            maBN = maBN.Trim();
            var patientIdsFromMaBN = _db.BenhNhans.AsNoTracking()
                .Where(b => _db.BenhNhanCoSos.AsNoTracking()
                    .Where(cs => cs.MaBN != null && cs.MaBN.Contains(maBN))
                    .Select(cs => cs.IdBenhNhan)
                    .Contains(b.Id));

            var accountIdsFromMaBN = await patientIdsFromMaBN
                .Where(b => b.IdTaiKhoan != null)
                .Select(b => b.IdTaiKhoan!.Value)
                .Distinct()
                .ToListAsync();
            var phonesFromMaBN = await patientIdsFromMaBN
                .Where(b => !string.IsNullOrEmpty(b.SDT))
                .Select(b => b.SDT!)
                .Distinct()
                .ToListAsync();
            var legacyIdsFromMaBN = await patientIdsFromMaBN
                .Select(b => (long?)b.Id)
                .Distinct()
                .ToListAsync();

            query = query.Where(x =>
                accountIdsFromMaBN.Contains(x.Id) ||
                (x.IdBenhNhan != null && legacyIdsFromMaBN.Contains(x.IdBenhNhan)) ||
                (!string.IsNullOrEmpty(x.SDT) && phonesFromMaBN.Contains(x.SDT)));
        }

        // 4. Lọc theo Loại cơ sở (Phòng khám, Bệnh viện, Nha khoa, Nhà thuốc) hoặc từng cơ sở
        if (!string.IsNullOrWhiteSpace(loaiCS))
        {
            loaiCS = loaiCS.Trim();
            List<long> facilityIds = new();
            if (loaiCS.StartsWith("cs:", StringComparison.OrdinalIgnoreCase) && long.TryParse(loaiCS[3..], out var specificCsId))
            {
                facilityIds.Add(specificCsId);
            }
            else
            {
                var nhomIds = await _db.DMNhomCSs.AsNoTracking()
                    .Where(n => n.MaNhom.ToLower() == loaiCS.ToLower())
                    .Select(n => (long?)n.ID)
                    .ToListAsync();

                facilityIds = await _db.DMCSKCBs.AsNoTracking()
                    .Where(cs => nhomIds.Contains(cs.IdNhomCS))
                    .Select(cs => cs.Id)
                    .ToListAsync();
            }

            if (facilityIds.Count > 0)
            {
                var accountIdsInDoiTac = await _db.TaiKhoanDoiTacs.AsNoTracking()
                    .Where(td => facilityIds.Contains(td.IdCoSo))
                    .Select(td => td.IdTaiKhoan)
                    .Distinct()
                    .ToListAsync();

                var patientIdsInCoSo = _db.BenhNhanCoSos.AsNoTracking()
                    .Where(cs => facilityIds.Contains(cs.IdCoSo))
                    .Select(cs => cs.IdBenhNhan);

                var patientsInCoSo = _db.BenhNhans.AsNoTracking()
                    .Where(bn => patientIdsInCoSo.Contains(bn.Id));

                var accountIdsFromCoSo = await patientsInCoSo
                    .Where(bn => bn.IdTaiKhoan != null)
                    .Select(bn => bn.IdTaiKhoan!.Value)
                    .Distinct()
                    .ToListAsync();

                var phonesFromCoSo = await patientsInCoSo
                    .Where(bn => !string.IsNullOrEmpty(bn.SDT))
                    .Select(bn => bn.SDT!)
                    .Distinct()
                    .ToListAsync();

                var legacyIdsFromCoSo = await patientsInCoSo
                    .Select(bn => (long?)bn.Id)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(x =>
                    accountIdsInDoiTac.Contains(x.Id) ||
                    accountIdsFromCoSo.Contains(x.Id) ||
                    (x.IdBenhNhan != null && legacyIdsFromCoSo.Contains(x.IdBenhNhan)) ||
                    (!string.IsNullOrEmpty(x.SDT) && phonesFromCoSo.Contains(x.SDT)));
            }
        }

        // 5. Lọc theo Vai trò
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Role == "Admin");
        else if (string.Equals(role, "BenhNhan", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Role != "Admin");

        bool hasFilter = !string.IsNullOrWhiteSpace(loaiCS) ||
                         !string.IsNullOrWhiteSpace(cccd) ||
                         !string.IsNullOrWhiteSpace(sdt) ||
                         !string.IsNullOrWhiteSpace(maBN) ||
                         !string.IsNullOrWhiteSpace(role);

        int total = 0;
        List<TaiKhoan> items = new();

        if (hasFilter)
        {
            total = await query.CountAsync();
            items = await query.OrderByDescending(x => x.Id)
                .Skip((page - 1) * DefaultPageSize)
                .Take(DefaultPageSize)
                .ToListAsync();
        }

        var danhSachCoSo = await _db.DMCSKCBs.AsNoTracking().OrderBy(x => x.TenCoSo).ToListAsync();

        return View(new TaiKhoanListViewModel
        {
            Items = items.Select(x =>
            {
                x.Role = NormalizeRole(x.Role);
                return x;
            }).ToList(),
            DanhSachCoSo = danhSachCoSo,
            Query = q,
            Role = role,
            LoaiCS = loaiCS,
            CCCD = cccd,
            SDT = sdt,
            MaBN = maBN,
            Page = page,
            PageSize = DefaultPageSize,
            TotalItems = total
        });
    }

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
            0, model.SDT, null, model.Role, null, null);
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
            null,
            entity.IdBenhNhan);
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
            return Json(new { success = false, message = "Vui lòng nhập đầy đủ họ tên hồ sơ." });

        var bn = await _db.BenhNhans.FirstOrDefaultAsync(x => x.Id == req.Id);
        if (bn == null)
            return Json(new { success = false, message = "Không tìm thấy hồ sơ bệnh nhân." });

        bn.TenBN = req.TenBN.Trim();
        bn.CCCD = (req.CCCD ?? "").Trim();
        bn.SDT = string.IsNullOrWhiteSpace(req.SDT) ? null : req.SDT.Trim();
        bn.NgaySinh = req.NgaySinh;
        bn.GioiTinh = req.GioiTinh;
        bn.DiaChi = string.IsNullOrWhiteSpace(req.DiaChi) ? null : req.DiaChi.Trim();
        bn.HoTenKhongDau = ChuanHoaTen.BoDau(req.TenBN);

        // Cập nhật Mã cơ sở (MaBN trong DM_BenhNhanCoSo)
        var newMaBN = string.IsNullOrWhiteSpace(req.MaBN) ? null : req.MaBN.Trim();
        var coSoRecord = await _db.BenhNhanCoSos.FirstOrDefaultAsync(x => x.IdBenhNhan == bn.Id);
        string? tenCoSo = null;

        if (coSoRecord != null)
        {
            if (string.IsNullOrEmpty(newMaBN))
            {
                _db.BenhNhanCoSos.Remove(coSoRecord);
            }
            else
            {
                coSoRecord.MaBN = newMaBN;
                var cs = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == coSoRecord.IdCoSo);
                tenCoSo = cs?.TenCoSo;
            }
        }
        else if (!string.IsNullOrEmpty(newMaBN))
        {
            var defaultCoSo = await _db.DMCSKCBs.OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (defaultCoSo != null)
            {
                _db.BenhNhanCoSos.Add(new BenhNhanCoSo
                {
                    IdBenhNhan = bn.Id,
                    IdCoSo = defaultCoSo.Id,
                    MaBN = newMaBN,
                    DaMoTaiLieu = false
                });
                tenCoSo = defaultCoSo.TenCoSo;
            }
        }

        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "Đã lưu thông tin hồ sơ thành công.",
            data = new
            {
                id = bn.Id,
                tenBN = bn.TenBN,
                cccd = bn.CCCD,
                sdt = bn.SDT ?? "",
                ngaySinh = bn.NgaySinh?.ToString("yyyy-MM-dd") ?? "",
                ngaySinhVn = bn.NgaySinh?.ToString("dd/MM/yyyy") ?? "—",
                gioiTinh = bn.GioiTinh ?? "1",
                gioiTinhVn = bn.GioiTinh == "1" ? "Nam" : (bn.GioiTinh == "2" ? "Nữ" : "Khác"),
                diaChi = bn.DiaChi ?? "",
                maBN = newMaBN ?? "",
                tenCoSo = tenCoSo ?? ""
            }
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

        var cosos = await _db.BenhNhanCoSos.Where(x => x.IdBenhNhan == bn.Id).ToListAsync();
        if (cosos.Any())
        {
            _db.BenhNhanCoSos.RemoveRange(cosos);
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
            return Json(new { success = false, message = "Vui lòng nhập họ tên hồ sơ." });

        var bn = new SixosPwa.Models.BenhNhan
        {
            IdTaiKhoan = req.IdTaiKhoan,
            TenBN = req.TenBN.Trim(),
            CCCD = (req.CCCD ?? "").Trim(),
            SDT = string.IsNullOrWhiteSpace(req.SDT) ? null : req.SDT.Trim(),
            NgaySinh = req.NgaySinh,
            GioiTinh = req.GioiTinh ?? "1",
            DiaChi = string.IsNullOrWhiteSpace(req.DiaChi) ? null : req.DiaChi.Trim(),
            HoTenKhongDau = ChuanHoaTen.BoDau(req.TenBN),
            NgayTao = DateTime.Now
        };

        _db.BenhNhans.Add(bn);
        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "Đã tạo hồ sơ mới thành công.",
            data = new
            {
                id = bn.Id,
                idTaiKhoan = bn.IdTaiKhoan,
                tenBN = bn.TenBN,
                cccd = bn.CCCD,
                sdt = bn.SDT ?? "",
                ngaySinh = bn.NgaySinh?.ToString("yyyy-MM-dd") ?? "",
                ngaySinhVn = bn.NgaySinh?.ToString("dd/MM/yyyy") ?? "—",
                gioiTinh = bn.GioiTinh ?? "1",
                gioiTinhVn = bn.GioiTinh == "1" ? "Nam" : (bn.GioiTinh == "2" ? "Nữ" : "Khác"),
                diaChi = bn.DiaChi ?? ""
            }
        });
    }
}
