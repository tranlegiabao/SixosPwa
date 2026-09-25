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

    public IActionResult Index(
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
        return RedirectToAction("Index", "BenhNhan", new { loaiCS, cccd, sdt, maBN, q, loc, page, pageSize });
    }

    [HttpGet]
    public IActionResult TaiTrang(
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
        return RedirectToAction("Index", "BenhNhan", new { loaiCS, cccd, sdt, maBN, q, loc, page, pageSize });
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

        // MatKhauNoiBo để null — phần băm chưa thi hành (Đính chính ADR 0009).
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
        // Đợt 1B: chính dòng đó LÀ hồ sơ tại cơ sở, giữ đúng một mã (ADR 0032).
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

            // Code 3 = hồ sơ ĐANG NỐI một mã KHÁC. Hàng rào chốt 47 / ADR 0032:
            // cửa Lưu hồ sơ CỐ Ý từ chối đổi mã đã có. Nhưng màn này tên là
            // "nhập mã bệnh nhân mới" — người bấm là bộ phận hỗ trợ và ĐÃ cố ý
            // đổi — nên đây là chỗ duy nhất được đi tiếp bằng CỬA ĐỔI MÃ (có ghi
            // sổ). Bốn nơi gọi _Save còn lại KHÔNG được mở đường này.
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

            // 🔴 Khối "đồng bộ dòng mồ côi" cũ đã bị gỡ ở đợt A.
            // Nó tìm tài liệu / đợt khám CHƯA gắn IDBenhNhanCoSo rồi đối chiếu bằng cột
            // MaBN nằm trên chính hai bảng đó. Cả hai cột MaBN này đã bị xóa
            // (QL_TaiLieuBenhNhan.MaBN, QL_DotKham.MaBN) và mã bệnh nhân giờ chỉ còn
            // suy ra được QUA IDBenhNhanCoSo — tức là phải dùng chính cái đang thiếu
            // để tìm nó. Không còn manh mối nào để nối lại, mà từ đợt A mọi đường ghi
            // đều bắt buộc có IDBenhNhanCoSo ngay từ đầu nên cũng không sinh thêm dòng mồ côi.
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

        // "Hồ sơ chính" sau đợt 1B: số điện thoại này đang giữ ĐÚNG MỘT hồ sơ
        // TẠI CƠ SỞ NÀY (ADR 0036 — phạm vi là cặp SDT x cơ sở).
        // 🔴 Bản trung gian của đợt này tra HT_TaiKhoan bằng ID CƠ SỞ rồi đếm
        // DM_BenhNhan theo ID TÀI KHOẢN — hai lần nhầm bảng, ra kết quả đúng
        // ngẫu nhiên khi hai dãy ID tình cờ trùng.
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
                // Không còn khái niệm "tài khoản" cho bệnh nhân (ADR 0036).
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

    /// <summary>
    /// *Gỡ nối* một mã khỏi một hồ sơ — cửa duy nhất, và do ADMIN bấm.
    ///
    /// <para>
    /// 🔴 KHÔNG xóa dòng bằng EF. Thủ tục <c>dbo.DM_BenhNhanCoSo_GoNoi</c> còn phải
    /// sao lưu tài liệu + đợt khám sang <c>bak.GoNoi_*_V001</c> rồi mới xóa, và phải để
    /// lại một dòng TỰ KHAI để hồ sơ "tụt về *Hồ sơ tự khai*" chứ không biến mất khỏi cơ
    /// sở. Xóa thẳng bằng EF thì mất hết ba việc đó, mà khóa ngoại NO_ACTION cũng chặn
    /// không cho xóa khi còn tài liệu — nên đường cũ vừa sai vừa sẽ gây 500.
    /// </para>
    ///
    /// <para>
    /// Thủ tục chặn theo CHỦ SỞ HỮU hồ sơ (<c>@IDTaiKhoan</c>) chứ không theo người đang
    /// bấm, nên ở đây truyền tài khoản của chính hồ sơ. Quyền của admin đã được cánh cửa
    /// Area Admin giữ — không nối hai lớp chặn vào làm một.
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

        // Đợt 1B: "chủ sở hữu" là cặp (SDT x cơ sở) của chính dòng hồ sơ đó
        // (ADR 0040). Khu Admin đi đường này thay mặt bệnh nhân nên lấy SDT +
        // cơ sở từ chính dòng, không hỏi phiên.
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

        // 🔴 Đợt 1B: một dòng ĐÃ LÀ hồ sơ tại cơ sở, không còn bảng liên kết để
        // dọn trước. Bản trung gian của đợt này đọc lại chính `bn` rồi RemoveRange
        // + Remove => DELETE hai lần cùng một dòng, lần hai ném
        // DbUpdateConcurrencyException. Xóa ĐÚNG MỘT LẦN.
        // Thông báo / push neo vào hồ sơ này phải đi trước (FK).
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

        // 🔴 Phạm vi là MỘT CƠ SỞ: khóa thật là UNIQUE(IDCoSo, CCCD) (ADR 0036),
        // không còn UNIQUE CCCD toàn hệ. Tra toàn hệ là chặn nhầm người đã có hồ
        // sơ ở cơ sở KHÁC — đúng thứ mà đợt 1B cố ý cho phép.
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
            // 🔴 PHẢI giới hạn trong CÙNG MỘT CƠ SỞ. Không có vế này thì một tên
            // phổ biến trùng ngày sinh + giới tính ở cơ sở KHÁC cũng bị chặn, và
            // lỗi báo "đã có hồ sơ khác tại cơ sở này" thành nói sai.
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
            // Đợt 1B: không còn cột IDTaiKhoan; hồ sơ thuộc về cặp (SDT x cơ sở).
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
                // Không còn khái niệm "tài khoản" cho bệnh nhân (ADR 0036).
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
