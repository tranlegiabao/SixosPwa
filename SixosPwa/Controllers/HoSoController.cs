using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers;

/// <summary>
/// Man *Ho so cua toi* — mot tai khoan quan nhieu con nguoi (ADR 0019).
///
/// <para>
/// Doi ho so = PHAT LAI cookie voi claim <c>HoSoDangChon</c>, dung khuon
/// <c>DangKyOnlineUB</c> (<c>ThemIdXemThongTinBenhNhan</c> goi
/// <c>AddClaimsAsync</c>). KHONG luu lua chon vao CSDL: no la trang thai cua
/// PHIEN, khong phai cua con nguoi — hai thiet bi cua cung mot tai khoan phai
/// xem duoc hai ho so khac nhau cung luc.
/// </para>
/// </summary>
[Authorize]
public class HoSoController : Controller
{
    private readonly IHoSoBenhNhanService _hoSo;
    private readonly ILogger<HoSoController> _logger;

    public HoSoController(IHoSoBenhNhanService hoSo, ILogger<HoSoController> logger)
    {
        _hoSo = hoSo;
        _logger = logger;
    }

    [HttpGet("/benh-nhan/ho-so")]
    public async Task<IActionResult> Index(string? loi = null, string? xong = null)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        ViewBag.MaCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        ViewBag.Loi = loi;
        ViewBag.Xong = xong;

        if (idTaiKhoan is null)
        {
            return View(new List<HoSoCuaToi>());
        }

        var idDangChon = LayIdDangChon();
        return View(await _hoSo.LayDanhSachAsync(idTaiKhoan.Value, idDangChon));
    }

    /// <summary>Doi ho so dang xem.</summary>
    [HttpPost("/benh-nhan/ho-so/chon")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chon(long id)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        // 🔴 Cong chan. Thieu phep kiem nay thi go ID ho so nguoi khac vao la xem
        // duoc benh an cua ho — dung loai lo hong ma duong doc tai lieu tung mac.
        if (!await _hoSo.HoSoThuocTaiKhoanAsync(id, idTaiKhoan.Value))
        {
            _logger.LogWarning(
                "Tai khoan {IdTaiKhoan} thu chon ho so {IdHoSo} khong thuoc ve minh.",
                idTaiKhoan.Value, id);

            return RedirectToAction(nameof(Index), new { loi = "Hồ sơ này không thuộc tài khoản của bạn." });
        }

        await PhatLaiClaimAsync(id);

        // Chon xong la vao thang trang benh nhan — do la ly do nguoi ta bam.
        // Muon doi tiep thi quay lai bang o *Ho so cua toi* tren do.
        return Redirect("/benh-nhan");
    }

    [HttpGet("/benh-nhan/ho-so/them")]
    public IActionResult Them(string? loi = null)
    {
        ViewBag.Loi = loi;
        return View();
    }

    [HttpPost("/benh-nhan/ho-so/them")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Them(string cccd, string hoTen, DateTime? ngaySinh,
                                          string? sdt, string? gioiTinh)
    {
        var idTaiKhoan = await LayIdTaiKhoanAsync();

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var ketQua = await _hoSo.TaoAsync(idTaiKhoan.Value, maCoSo, cccd, hoTen, ngaySinh, sdt, gioiTinh);

        if (!ketQua.ThanhCong)
        {
            // Loi "ai khai truoc giu CCCD" phai hien NGUYEN VAN — no co chi duong
            // ra. Hien o chinh man Them de nguoi dung khong mat nhung gi vua go.
            return RedirectToAction(nameof(Them), new { loi = ketQua.ThongBao });
        }

        await PhatLaiClaimAsync(ketQua.IdBenhNhan);

        // Tang 2 thi phai dung lai o man *Sua ho so* de nguoi dung tu nhan ma —
        // day thang ve danh sach la nuot mat buoc xac nhan.
        if (ketQua.KetCuc == KetCucNoi.ChoXacNhan)
        {
            GiuUngVien(ketQua);
            return RedirectToAction(nameof(Sua), new { id = ketQua.IdBenhNhan });
        }

        return RedirectToAction(nameof(Index), new { xong = MaKetCuc(ketQua) });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Man *Sua ho so* — MOI o Dot 4 (ADR 0024 ve 1)
    //
    // 🔴 Thu duy nhat cuu duoc nhom ho so CHINH CHU: 14/19 tai khoan dang mang
    // ten la SO DIEN THOAI vi ho so cua ho tu de ra luc dang ky bang OTP, khong
    // bao gio di qua man *Them ho so*. Bat ky phuong an nao chi sua man *Them*
    // deu bo roi dung nhom dong nhat.
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("/benh-nhan/ho-so/sua")]
    public async Task<IActionResult> Sua(long id, string? loi = null, string? xong = null)
    {
        var idTaiKhoan = await LayIdTaiKhoanAsync();

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        // 🔴 Cong chan — service loc theo ca ID lan chu so huu, tra null khi ho so
        // khong phai cua tai khoan nay.
        var hoSo = await _hoSo.LayDeSuaAsync(id, idTaiKhoan.Value, maCoSo);

        if (hoSo is null)
        {
            _logger.LogWarning("Tai khoan {IdTaiKhoan} thu sua ho so {IdHoSo} khong thuoc ve minh.",
                idTaiKhoan.Value, id);

            return RedirectToAction(nameof(Index), new { loi = "Hồ sơ này không thuộc tài khoản của bạn." });
        }

        var ungVien = LayUngVienDaGiu(id);

        ViewBag.Loi = loi;
        ViewBag.Xong = xong;
        ViewBag.UngVien = ungVien;

        // 🔴 Ma nao DA co ho so khac nhan thi man KHOA lai, khong cho tick. Tra o
        // day chu khong nhet vao TempData luc luu: giua luc luu va luc bam xac nhan
        // co the co nguoi khac vua nhan mat ma do, va cai nguoi dung nhin thay phai
        // la trang thai BAY GIO.
        ViewBag.MaDaCoChu = ungVien is { Count: > 0 }
            ? await _hoSo.LayMaDaCoChuAsync(id, maCoSo, ungVien.Select(x => x.MaBN ?? string.Empty))
            : new List<string>();

        return View(hoSo);
    }

    [HttpPost("/benh-nhan/ho-so/sua")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sua(long id, string cccd, string hoTen, DateTime? ngaySinh,
                                         string? sdt, string? gioiTinh)
    {
        var idTaiKhoan = await LayIdTaiKhoanAsync();

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var ketQua = await _hoSo.SuaAsync(id, idTaiKhoan.Value, maCoSo, cccd, hoTen,
                                          ngaySinh, sdt, gioiTinh);

        if (!ketQua.ThanhCong)
        {
            return RedirectToAction(nameof(Sua), new { id, loi = ketQua.ThongBao });
        }

        if (ketQua.KetCuc == KetCucNoi.ChoXacNhan) GiuUngVien(ketQua);

        // O LAI man *Sua ho so*: chinh o day moi co khoi *Ma benh nhan tai co so*
        // de nguoi dung thay ket qua cua lan luu vua roi.
        return RedirectToAction(nameof(Sua), new { id, xong = MaKetCuc(ketQua) });
    }

    /// <summary>
    /// *Tang 2* — benh nhan chon DUNG MOT ma la cua minh roi bam nhan.
    /// Man hien radio chu khong phai checkbox: mot ho so &lt;-&gt; mot ma.
    /// </summary>
    [HttpPost("/benh-nhan/ho-so/xac-nhan-noi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhanNoi(long id, string? maBN)
    {
        var idTaiKhoan = await LayIdTaiKhoanAsync();

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var (thanhCong, thongBao, soMa) = await _hoSo.XacNhanNoiAsync(
            id, idTaiKhoan.Value, maCoSo, maBN);

        XoaUngVienDaGiu(id);

        return thanhCong
            ? RedirectToAction(nameof(Sua), new { id, xong = soMa > 0 ? "da-noi" : "khong-noi" })
            : RedirectToAction(nameof(Sua), new { id, loi = thongBao });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 🔴 *Go noi* DA RUT KHOI PHIA BENH NHAN (09/09) — dao ve 3 cua ADR 0024.
    //
    // Go noi la thao tac PHA HUY: thu tuc xoa tai lieu + dot kham cua ma do roi
    // nha ma ra cho nguoi khac nhan. De benh nhan tu bam thi mot lan bam nham la
    // mat benh an, ma chinh ho khong dung lai duoc — phai co nguoi cua co so vao
    // CSDL moi khoi phuc tu bak.GoNoi_*_V001.
    //
    // Viec nay chuyen han sang man ADMIN (nhanh rieng cua dong nghiep). O day
    // KHONG chi go cai nut: BO LUON action. An nut ma de cua mo thi ai doc HTML
    // cu cung goi tay duoc — dung nghia "an di" chu khong phai "dong lai".
    //
    // Phan LOI van con nguyen cho man Admin dung, dung xoa nham:
    //   - thu tuc  dbo.DM_BenhNhanCoSo_GoNoi
    //   - ba bang  bak.GoNoi_TaiLieu_V001 / _DotKham_V001 / _HoSoCoSo_V001
    //   - AdminStoredProcedureService.GoNoiAsync
    //   - IHoSoBenhNhanService.GoNoiAsync
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost("/benh-nhan/ho-so/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Xoa(long id)
    {
        var dinhDanh = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        if (idTaiKhoan is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var (thanhCong, thongBao) = await _hoSo.XoaAsync(id, idTaiKhoan.Value);

        if (!thanhCong)
        {
            return RedirectToAction(nameof(Index), new { loi = thongBao });
        }

        // Vua xoa dung ho so dang chon => bo claim di, de LayHoSoDangDungAsync
        // roi ve ho so dau tien thay vi tro toi mot ID khong con ton tai.
        if (LayIdDangChon() == id)
        {
            await PhatLaiClaimAsync(null);
        }

        return RedirectToAction(nameof(Index), new { xong = "1" });
    }

    private Task<long?> LayIdTaiKhoanAsync() =>
        _hoSo.LayIdTaiKhoanAsync(User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty);

    /// <summary>Ma trang thai cho man doc, khong phai cau chu — cau chu nam o view.</summary>
    private static string MaKetCuc(KetQuaLuuHoSo ketQua) => ketQua.KetCuc switch
    {
        KetCucNoi.DaGanImLang      => "da-noi",
        KetCucNoi.ChuaHoiDuocCoSo  => "chua-hoi-duoc",
        _                          => "1"
    };

    // ── Giu danh sach ung vien cua *Tang 2* qua mot lan chuyen huong ──────────
    //
    // 🔴 Dung TempData chu khong hoi lai HIS o man GET: hoi lai la mot cuoc goi
    // nua sang may khach cho cung mot cau hoi, va te hon — danh sach co the DOI
    // giua hai lan hoi, thanh ra nguoi dung tick mot dang roi nhan mot dang khac.
    // Khoa co kem ID ho so de danh sach cua ho so nay khong lot sang ho so kia.

    private string KhoaUngVien(long idHoSo) => $"UngVienNoi_{idHoSo}";

    private void GiuUngVien(KetQuaLuuHoSo ketQua)
    {
        if (ketQua.UngVien is null || ketQua.UngVien.Count == 0) return;

        TempData[KhoaUngVien(ketQua.IdBenhNhan)] =
            System.Text.Json.JsonSerializer.Serialize(ketQua.UngVien);
    }

    private List<SixosPwa.Services.His.HoSoHis>? LayUngVienDaGiu(long idHoSo)
    {
        // Peek chu khong doc dut: nguoi dung tai lai trang (F5) van con danh sach,
        // khong thi ho mat luon buoc xac nhan ma khong hieu vi sao.
        if (TempData.Peek(KhoaUngVien(idHoSo)) is not string json) return null;

        TempData.Keep(KhoaUngVien(idHoSo));

        try
        {
            return System.Text.Json.JsonSerializer
                .Deserialize<List<SixosPwa.Services.His.HoSoHis>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private void XoaUngVienDaGiu(long idHoSo) => TempData.Remove(KhoaUngVien(idHoSo));

    private long? LayIdDangChon() =>
        long.TryParse(User.FindFirst(LuongCongBenhNhan.ClaimHoSoDangChon)?.Value, out var id)
            ? id
            : null;

    /// <summary>
    /// Phat lai cookie: giu nguyen moi claim cu, chi thay claim *ho so dang chon*.
    ///
    /// Phai chep lai ca bo chu khong tao identity moi — mat claim <c>MaCoSo</c>
    /// hay dau an doi tac la phien tut xuong trang thai khac han (ADR 0016).
    /// </summary>
    private async Task PhatLaiClaimAsync(long? idHoSo)
    {
        var claims = User.Claims
            .Where(c => c.Type != LuongCongBenhNhan.ClaimHoSoDangChon)
            .ToList();

        if (idHoSo is not null)
        {
            claims.Add(new Claim(LuongCongBenhNhan.ClaimHoSoDangChon, idHoSo.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Giu dung thoi han cua luong dang nhap (DangNhapController): phat lai ma
        // quen IsPersistent thi phien tut ve cookie phien, dong trinh duyet la mat.
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
            });
    }
}
