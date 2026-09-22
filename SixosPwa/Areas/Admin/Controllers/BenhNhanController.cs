using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Areas.Admin.Controllers;

public sealed class BenhNhanController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _adminStoredProcedures;

    public BenhNhanController(
        ApplicationDbContext db,
        AdminStoredProcedureService adminStoredProcedures)
    {
        _db = db;
        _adminStoredProcedures = adminStoredProcedures;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? loaiCS,
        string? cccd,
        string? sdt,
        string? maBN,
        string? q,
        string? loc,
        int page = 1,
        int pageSize = 50)
    {
        page = SafePage(page);
        pageSize = pageSize is 20 or 50 or 100 or 500 ? pageSize : 50;

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
                    !string.IsNullOrWhiteSpace(q);

        if (!daLoc)
        {
            var emptyModel = new BenhNhanListViewModel
            {
                Items = Array.Empty<BenhNhanNhomItemViewModel>(),
                DanhSachCoSo = danhSachCoSo,
                DanhMucGioiTinh = danhMucGioiTinh,
                Query = q,
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
                return PartialView("_BenhNhanTableBody", emptyModel);
            }

            return View(emptyModel);
        }

        // 🔴 MỌI câu đọc phục vụ giao diện PHẢI lọc IdCoSo != null (ADR 0040)
        var query = _db.BenhNhans.AsNoTracking().Where(x => x.IdCoSo != null).AsQueryable();

        if (!string.IsNullOrWhiteSpace(loaiCS))
        {
            var trimCs = loaiCS.Trim();
            if (trimCs.StartsWith("cs:", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(trimCs.Substring(3), out var idCs))
            {
                query = query.Where(x => x.IdCoSo == idCs);
            }
            else if (long.TryParse(trimCs, out var idCsDirect))
            {
                query = query.Where(x => x.IdCoSo == idCsDirect);
            }
            else
            {
                query = query.Where(x => _db.DMCSKCBs.Any(cs => cs.Id == x.IdCoSo && cs.MaCoSo == trimCs));
            }
        }

        if (!string.IsNullOrWhiteSpace(cccd))
        {
            var trimCccd = cccd.Trim();
            query = query.Where(x => x.CCCD.Contains(trimCccd));
        }

        if (!string.IsNullOrWhiteSpace(sdt))
        {
            var trimSdt = sdt.Trim();
            query = query.Where(x => x.SDT != null && x.SDT.Contains(trimSdt));
        }

        if (!string.IsNullOrWhiteSpace(maBN))
        {
            var trimMa = maBN.Trim();
            query = query.Where(x => x.MaBN != null && x.MaBN.Contains(trimMa));
        }

        // Gom nhóm theo Số điện thoại (với hồ sơ không có SĐT thì gom theo no-phone:{Id})
        var distinctKeysQuery = query
            .Select(x => string.IsNullOrWhiteSpace(x.SDT) ? ("no-phone:" + x.Id) : x.SDT!)
            .Distinct();

        var total = await distinctKeysQuery.CountAsync();
        var paginatedKeys = await distinctKeysQuery
            .OrderBy(k => k)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var phoneList = paginatedKeys.Where(k => !k.StartsWith("no-phone:")).ToList();
        var noPhoneIdList = paginatedKeys.Where(k => k.StartsWith("no-phone:"))
            .Select(k => long.Parse(k.Substring("no-phone:".Length)))
            .ToList();

        var patientRecords = await query
            .Where(x => (x.SDT != null && phoneList.Contains(x.SDT)) || noPhoneIdList.Contains(x.Id))
            .OrderBy(x => x.TenBN)
            .ToListAsync();

        var coSoIds = patientRecords.Where(p => p.IdCoSo.HasValue).Select(p => p.IdCoSo!.Value).Distinct().ToList();
        var coSoMap = await _db.DMCSKCBs.AsNoTracking()
            .Where(cs => coSoIds.Contains(cs.Id))
            .ToDictionaryAsync(cs => cs.Id, cs => cs.TenCoSo);

        var groupItems = new List<BenhNhanNhomItemViewModel>();
        foreach (var key in paginatedKeys)
        {
            List<BenhNhan> matched;
            string displayPhone;
            long groupId;

            if (key.StartsWith("no-phone:"))
            {
                var id = long.Parse(key.Substring("no-phone:".Length));
                matched = patientRecords.Where(p => p.Id == id).ToList();
                displayPhone = "Chưa có SĐT";
                groupId = id;
            }
            else
            {
                matched = patientRecords.Where(p => p.SDT == key).ToList();
                displayPhone = key;
                groupId = matched.FirstOrDefault()?.Id ?? Math.Abs((long)key.GetHashCode());
            }

            var hosoList = matched.Select(p => new HoSoBenhNhanItemViewModel
            {
                Id = p.Id,
                TenBN = p.TenBN,
                CCCD = p.CCCD,
                SDT = p.SDT,
                Email = p.Email,
                DiaChi = p.DiaChi,
                NgaySinh = p.NgaySinh,
                GioiTinh = p.GioiTinh,
                MaBN = p.MaBN,
                IdCoSo = p.IdCoSo,
                IdHoSoCoSo = p.Id,
                TenCoSo = p.IdCoSo.HasValue && coSoMap.TryGetValue(p.IdCoSo.Value, out var tcs) ? tcs : null
            }).ToList();

            groupItems.Add(new BenhNhanNhomItemViewModel
            {
                Id = groupId,
                SDT = displayPhone,
                DanhSachHoSo = hosoList
            });
        }

        var model = new BenhNhanListViewModel
        {
            Items = groupItems,
            DanhSachCoSo = danhSachCoSo,
            DanhMucGioiTinh = danhMucGioiTinh,
            Query = q,
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
            return PartialView("_BenhNhanTableBody", model);
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
        string? loc,
        int page = 1,
        int pageSize = 50)
    {
        return Index(loaiCS, cccd, sdt, maBN, q, loc, page, pageSize);
    }

    private bool IsAjaxRequest() =>
        Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
        Request.Query.ContainsKey("isAjax") ||
        Request.Query.ContainsKey("ajax");

    private static readonly List<DMGioiTinh> DanhMucGioiTinhCoDinh = new()
    {
        new DMGioiTinh { MaGioiTinh = "1", TenGioiTinh = "Nam" },
        new DMGioiTinh { MaGioiTinh = "2", TenGioiTinh = "Nữ" },
        new DMGioiTinh { MaGioiTinh = "3", TenGioiTinh = "Không xác định" }
    };

    private static List<DMGioiTinh> LayDanhMucGioiTinh() => DanhMucGioiTinhCoDinh;

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

        if (!laCccdKhongCo)
        {
            var trungCccd = await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.CCCD == cccdMoi && x.Id != bn.Id && x.IdCoSo == bn.IdCoSo);
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

        var newMaBN = string.IsNullOrWhiteSpace(req.MaBN) ? null : req.MaBN.Trim();
        var idCoSoDich = req.IdCoSo ?? coSoRecord?.IdCoSo ?? 0;
        long? idCoSoHienThi = idCoSoDich > 0 ? idCoSoDich : coSoRecord?.IdCoSo;
        var maBNSauKhiLuu = coSoRecord?.MaBN;
        long? idHoSoCoSoResult = coSoRecord?.Id;
        string? canhBao = null;

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

            if (ketQuaMa.Code == 3)
            {
                (ketQuaMa, idCoSoMoi) = await _adminStoredProcedures.DoiMaBenhNhanCoSoAsync(
                    bn.Id, idCoSoDich, newMaBN, lyDo: "Man Admin > Benh nhan");
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
        }
        else if (!string.IsNullOrEmpty(coSoRecord?.MaBN))
        {
            canhBao = "Mã bệnh nhân giữ nguyên — muốn tháo mã thì bấm nút Gỡ đồng bộ, "
                    + "để trống ô rồi Lưu không gỡ được.";
        }

        await _db.SaveChangesAsync();

        var tenCoSo = idCoSoHienThi == null
            ? null
            : await _db.DMCSKCBs.AsNoTracking()
                .Where(x => x.Id == idCoSoHienThi.Value)
                .Select(x => x.TenCoSo)
                .FirstOrDefaultAsync();

        bool isPrimary = false;
        if (bn.IdCoSo.HasValue && !string.IsNullOrWhiteSpace(bn.SDT))
        {
            var soHoSo = await _db.BenhNhans.AsNoTracking()
                .CountAsync(x => x.IdCoSo == bn.IdCoSo.Value && x.SDT == bn.SDT);
            isPrimary = soHoSo == 1;
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
                idHoSo = bn.Id,
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

        var thongBao = await _db.ThongBaos.Where(x => x.IdNguoiNhan == bn.Id).ToListAsync();
        if (thongBao.Count > 0) _db.ThongBaos.RemoveRange(thongBao);

        var push = await _db.PushDangKys.Where(x => x.IdBenhNhan == bn.Id).ToListAsync();
        if (push.Count > 0) _db.PushDangKys.RemoveRange(push);

        _db.BenhNhans.Remove(bn);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Đã xóa hồ sơ bệnh nhân thành công." });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> TaoHoSo([FromBody] TaoHoSoAdminRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.TenBN))
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

        var idCoSoTao = req.IdCoSo ?? 0;

        if (!laCccdKhongCo && idCoSoTao > 0)
        {
            var trungCccd = await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.CCCD == cccdMoi && x.IdCoSo == idCoSoTao);
            if (trungCccd)
            {
                return Json(new { success = false, isWarning = true, message = "Số căn cước này đã có hồ sơ khác tại cơ sở này." });
            }
        }
        else if (laCccdKhongCo && req.NgaySinh.HasValue && !string.IsNullOrEmpty(req.GioiTinh))
        {
            var tenKd = ChuanHoaTen.BoDau(req.TenBN);
            var trungNhanThan = idCoSoTao <= 0 ? false : await _db.BenhNhans.AsNoTracking()
                .AnyAsync(x => x.HoTenKhongDau == tenKd
                            && x.NgaySinh.HasValue && x.NgaySinh.Value.Date == req.NgaySinh.Value.Date
                            && x.GioiTinh == req.GioiTinh
                            && x.IdCoSo == idCoSoTao);
            if (trungNhanThan)
            {
                return Json(new { success = false, isWarning = true, message = "Hồ sơ với thông tin này (Họ tên, ngày sinh, giới tính) đã có hồ sơ khác tại cơ sở này." });
            }
        }

        var bn = new SixosPwa.Models.BenhNhan
        {
            TenBN = req.TenBN.Trim(),
            CCCD = cccdMoi,
            SDT = string.IsNullOrWhiteSpace(req.SDT) ? null : req.SDT.Trim(),
            NgaySinh = req.NgaySinh,
            GioiTinh = req.GioiTinh ?? "1",
            DiaChi = string.IsNullOrWhiteSpace(req.DiaChi) ? null : req.DiaChi.Trim(),
            HoTenKhongDau = ChuanHoaTen.BoDau(req.TenBN),
            IdCoSo = idCoSoTao > 0 ? idCoSoTao : null,
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
                idHoSo = bn.Id,
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
