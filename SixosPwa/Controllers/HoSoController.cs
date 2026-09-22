using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SixosPwa.Services;
using SixosPwa.Services.Partner;

namespace SixosPwa.Controllers;

/// <summary>
/// Màn *Hồ sơ của tôi* — một tài khoản quản nhiều con người (ADR 0019).
///
/// <para>
/// Đổi hồ sơ = PHÁT LẠI cookie với claim <c>HoSoDangChon</c>, đúng khuôn
/// <c>DangKyOnlineUB</c> (<c>ThemIdXemThongTinBenhNhan</c> gọi
/// <c>AddClaimsAsync</c>). KHÔNG lưu lựa chọn vào CSDL: nó là trạng thái của
/// PHIÊN, không phải của con người — hai thiết bị của cùng một tài khoản phải
/// xem được hai hồ sơ khác nhau cùng lúc.
/// </para>
/// </summary>
[Authorize]
public class HoSoController : Controller
{
    private readonly IHoSoBenhNhanService _hoSo;
    private readonly IHTConfigService _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HoSoController> _logger;

    public HoSoController(
        IHoSoBenhNhanService hoSo,
        IHTConfigService config,
        IMemoryCache cache,
        ILogger<HoSoController> logger)
    {
        _hoSo = hoSo;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    // ── Chặn dò danh tính ────────────────────────────────────────────────────
    //
    // 🔴 Hai action dưới đây đều nhận HỌ TÊN + NGÀY SINH + GIỚI TÍNH rồi hỏi HIS.
    // Ba ô đó không phải bí mật, nên không chặn tốc độ thì một tài khoản có thể
    // rà soát cả danh sách bệnh nhân của cơ sở. Bên HIS đã chặn dò Mã BN
    // (SPWA_TraCuuTheoMaBN, 20 lần/15 phút) nhưng KHÔNG ai chặn dò danh tính.
    //
    // Đếm theo CẢ tài khoản LẪN địa chỉ IP: khóa theo tài khoản thì kẻ tấn công mở
    // tài khoản mới, khóa theo IP thì cả phòng khám chung một IP bị vạ lây.
    private const int SoPhutCuaSo   = 5;
    private const int NguongTaiKhoan = 10;
    private const int NguongIp       = 30;

    private bool QuaNhanh(string viec, string dinhDanh, int nguong)
    {
        var khoa = $"RL_{viec}_{dinhDanh}";
        var dem = _cache.TryGetValue(khoa, out int cu) ? cu + 1 : 1;

        // Đặt lại hạn mỗi lần ghi thì cửa sổ trượt theo — cố ý: kẻ đang dò liên tục
        // sẽ bị khóa cho tới khi nó chịu im trọn 5 phút.
        _cache.Set(khoa, dem, TimeSpan.FromMinutes(SoPhutCuaSo));

        return dem > nguong;
    }

    private bool BiChanDo(string viec, string? khoaPhien)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        var quaTaiKhoan = !string.IsNullOrWhiteSpace(khoaPhien)
                          && QuaNhanh(viec, $"tk{khoaPhien}", NguongTaiKhoan);
        var quaIp = QuaNhanh(viec, $"ip{ip}", NguongIp);

        if (quaTaiKhoan || quaIp)
        {
            _logger.LogWarning("Chan do {Viec}: so {Sdt} / IP {Ip}", viec, khoaPhien, ip);
            return true;
        }

        return false;
    }

    private const string LoiChanDo =
        "Bạn thao tác hơi nhanh. Vui lòng chờ ít phút rồi thử lại.";

    [HttpGet("/benh-nhan/ho-so")]
    public async Task<IActionResult> Index(string? loi = null, string? xong = null)
    {
        var dinhDanh = SdtPhien();
        var maCoSoPhien = MaCoSoPhien();
        var coLoiVao = await _hoSo.CoLoiVaoAsync(dinhDanh, maCoSoPhien);

        ViewBag.MaCoSo = maCoSoPhien;
        ViewBag.Loi = loi;
        ViewBag.Xong = xong;

        if (!coLoiVao)
        {
            return View(new List<HoSoCuaToi>());
        }

        var idDangChon = LayIdDangChon();
        return View(await _hoSo.LayDanhSachAsync(
            dinhDanh, maCoSoPhien,
            User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value,
            idDangChon));
    }

    [HttpPost("/benh-nhan/ho-so/chon")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chon(long id)
    {
        var dinhDanh = SdtPhien();
        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        // 🔴 Cổng chặn. Thiếu phép kiểm này thì gõ ID hồ sơ người khác vào là xem
        // được bệnh án của họ — đúng loại lỗ hổng mà đường đọc tài liệu từng mắc.
        if (!await _hoSo.HoSoThuocTaiKhoanAsync(id, dinhDanh, MaCoSoPhien()))
        {
            _logger.LogWarning(
                "Tai khoan {Sdt} thu chon ho so {IdHoSo} khong thuoc ve minh.",
                dinhDanh, id);

            return RedirectToAction(nameof(Index), new { loi = "Hồ sơ này không thuộc tài khoản của bạn." });
        }

        await PhatLaiClaimAsync(id);

        // Chọn xong là vào thẳng trang bệnh nhân — đó là lý do người ta bấm.
        // Muốn đổi tiếp thì quay lại bằng ô *Hồ sơ của tôi* trên đó.
        return Redirect("/benh-nhan");
    }

    [HttpGet("/benh-nhan/ho-so/them")]
    public IActionResult Them(string? loi = null)
    {
        // Luồng thêm hồ sơ người thân đã tạm đóng theo yêu cầu
        return RedirectToAction(nameof(Index));
        /*
        ViewBag.Loi = loi;
        return View();
        */
    }

    [HttpPost("/benh-nhan/ho-so/them")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Them(string cccd, string hoTen, DateTime? ngaySinh,
                                          string? sdt, string? gioiTinh)
    {
        // Luồng thêm hồ sơ người thân đã tạm đóng theo yêu cầu
        await Task.CompletedTask;
        return RedirectToAction(nameof(Index));
        /*
        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        if (BiChanDo("them-ho-so", SdtPhien()))
        {
            return RedirectToAction(nameof(Them), new { loi = LoiChanDo });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var ketQua = await _hoSo.TaoAsync(SdtPhien(), maCoSo, cccd, hoTen, ngaySinh, sdt, gioiTinh);

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
        */
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Màn *Sửa hồ sơ* — MỚI ở Đợt 4 (ADR 0024 vế 1)
    //
    // 🔴 Thứ duy nhất cứu được nhóm hồ sơ CHÍNH CHỦ: 14/19 tài khoản đang mang
    // tên là SỐ ĐIỆN THOẠI vì hồ sơ của họ tự đẻ ra lúc đăng ký bằng OTP, không
    // bao giờ đi qua màn *Thêm hồ sơ*. Bất kỳ phương án nào chỉ sửa màn *Thêm*
    // đều bỏ rơi đúng nhóm đông nhất.
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("/benh-nhan/ho-so/sua")]
    public async Task<IActionResult> Sua(long id, string? loi = null, string? xong = null)
    {
        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        // 🔴 Cổng chặn — service lọc theo cả ID lẫn chủ sở hữu, trả null khi hồ sơ
        // không phải của tài khoản này.
        var hoSo = await _hoSo.LayDeSuaAsync(id, SdtPhien(), maCoSo);

        if (hoSo is null)
        {
            _logger.LogWarning("So {Sdt} thu sua ho so {IdHoSo} khong thuoc ve minh.",
                SdtPhien(), id);

            return RedirectToAction(nameof(Index), new { loi = "Hồ sơ này không thuộc tài khoản của bạn." });
        }

        var ungVien = LayUngVienDaGiu(id);

        ViewBag.Loi = loi;
        ViewBag.Xong = xong;
        ViewBag.UngVien = ungVien;
        ViewBag.ChoPhepGoNoi = await _config.KiemTraHieuLucAsync("GONOI");

        // 🔴 Mã nào ĐÃ có hồ sơ khác nhận thì màn KHÓA lại, không cho tick. Tra ở
        // đây chứ không nhét vào TempData lúc lưu: giữa lúc lưu và lúc bấm xác nhận
        // có thể có người khác vừa nhận mất mã đó, và cái người dùng nhìn thấy phải
        // là trạng thái BÂY GIỜ.
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
        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        // 🔴 Chặn bệnh nhân sửa thông tin khi hồ sơ đang có mã nối. Phải gỡ nối trước.
        var hoSoHienTai = await _hoSo.LayDeSuaAsync(id, SdtPhien(), maCoSo);
        if (hoSoHienTai is null)
        {
            return RedirectToAction(nameof(Index), new { loi = "Hồ sơ không hợp lệ hoặc không thuộc tài khoản của bạn." });
        }

        if (hoSoHienTai.DaNoiHIS)
        {
            return RedirectToAction(nameof(Sua), new { id, loi = "Hồ sơ đang liên kết mã bệnh nhân. Vui lòng bấm 'Gỡ đồng bộ' trước khi chỉnh sửa thông tin." });
        }

        var ketQua = await _hoSo.SuaAsync(id, SdtPhien(), maCoSo, cccd, hoTen,
                                          ngaySinh, sdt, gioiTinh);

        if (!ketQua.ThanhCong)
        {
            return RedirectToAction(nameof(Sua), new { id, loi = ketQua.ThongBao });
        }

        // 🔴 *Chờ xác nhận* là ngoại lệ DUY NHẤT phải Ở LẠI màn Sửa: danh sách ứng
        // viên để bệnh nhân tự nhận mã chỉ về ở đây. Đẩy họ về *Hồ sơ của tôi* lúc
        // này là cắt đứt tầng 2 — họ không còn đường nào nhận mã nữa.
        if (ketQua.KetCuc == KetCucNoi.ChoXacNhan)
        {
            GiuUngVien(ketQua);
            return RedirectToAction(nameof(Sua), new { id, xong = MaKetCuc(ketQua) });
        }

        // Lưu xong là VỀ *Hồ sơ của tôi*. Người dùng vào màn Sửa để sửa một hồ sơ,
        // sửa xong thì việc đã hết; giữ họ lại ở cái form vừa nộp chỉ để đọc một
        // dòng báo thành công là bắt họ tự tìm đường ra.
        return RedirectToAction(nameof(Index), new { xong = MaKetCuc(ketQua) });
    }

    /// <summary>
    /// *Tầng 2* — bệnh nhân chọn ĐÚNG MỘT mã là của mình rồi bấm nhận.
    /// Màn hiện radio chứ không phải checkbox: một hồ sơ &lt;-&gt; một mã.
    /// </summary>
    [HttpPost("/benh-nhan/ho-so/xac-nhan-noi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XacNhanNoi(long id, string? maBN)
    {
        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        if (BiChanDo("xac-nhan-noi", SdtPhien()))
        {
            return RedirectToAction(nameof(Sua), new { id, loi = LoiChanDo });
        }

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var (thanhCong, thongBao, soMa) = await _hoSo.XacNhanNoiAsync(
            id, SdtPhien(), maCoSo, maBN);

        XoaUngVienDaGiu(id);

        // Nhận xong là hết việc ở màn Sửa => về *Hồ sơ của tôi*, cùng đường ra với
        // lúc bấm Lưu. Lỗi thì PHẢI ở lại — vd mã vừa bị người khác nhận mất, người
        // dùng còn phải đọc câu chỉ đường sang bộ phận hỗ trợ.
        return thanhCong
            ? RedirectToAction(nameof(Index), new { xong = soMa > 0 ? "da-noi" : "khong-noi" })
            : RedirectToAction(nameof(Sua), new { id, loi = thongBao });
    }

    /// <summary>
    /// Gỡ nối — thao tác xóa mã bệnh nhân khỏi hồ sơ.
    /// Điều khiển bởi cấu hình HT_Config (MaChucNang = 'GONOI').
    /// </summary>
    [HttpPost("/benh-nhan/ho-so/go-noi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoNoi(long id, long idHoSoCoSo)
    {
        var choPhep = await _config.KiemTraHieuLucAsync("GONOI");
        if (!choPhep)
        {
            return RedirectToAction(nameof(Sua), new { id, loi = "Chức năng gỡ đồng bộ hiện đang tạm khóa." });
        }

        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var (thanhCong, thongBao) = await _hoSo.GoNoiAsync(idHoSoCoSo, SdtPhien(), MaCoSoPhien());

        return thanhCong
            ? RedirectToAction(nameof(Sua), new { id, xong = "da-go" })
            : RedirectToAction(nameof(Sua), new { id, loi = thongBao });
    }

    [HttpPost("/benh-nhan/ho-so/xoa")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Xoa(long id)
    {
        var dinhDanh = SdtPhien();
        var coLoiVao = await CoLoiVaoAsync();

        if (!coLoiVao)
        {
            return RedirectToAction(nameof(Index), new { loi = "Không tìm thấy tài khoản." });
        }

        var (thanhCong, thongBao) = await _hoSo.XoaAsync(id, dinhDanh, MaCoSoPhien());

        if (!thanhCong)
        {
            return RedirectToAction(nameof(Index), new { loi = thongBao });
        }

        // Vừa xóa đúng hồ sơ đang chọn => bỏ claim đi, để LayHoSoDangDungAsync
        // rơi về hồ sơ đầu tiên thay vì trỏ tới một ID không còn tồn tại.
        if (LayIdDangChon() == id)
        {
            await PhatLaiClaimAsync(null);
        }

        return RedirectToAction(nameof(Index), new { xong = "1" });
    }

    /// <summary>Số điện thoại của phiên — từ đợt 1B đây là danh tính đăng nhập.</summary>
    private string SdtPhien() => User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;

    private string? MaCoSoPhien() => User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

    /// <summary>
    /// 🔴 C7a — GÁC ROUTE theo đúng luật màn đăng nhập (C7b). Nút *Thêm hồ sơ* đã
    /// ẩn ở Views/HoSo/Index.cshtml nhưng ROUTE vẫn sống; thiếu cổng này thì gõ
    /// thẳng URL là tạo được hồ sơ ở cơ sở mình chưa từng khám.
    /// </summary>
    private Task<bool> CoLoiVaoAsync() => _hoSo.CoLoiVaoAsync(SdtPhien(), MaCoSoPhien());

    /// <summary>Mã trạng thái cho màn đọc, không phải câu chữ — câu chữ nằm ở view.</summary>
    private static string MaKetCuc(KetQuaLuuHoSo ketQua) => ketQua.KetCuc switch
    {
        KetCucNoi.DaGanImLang      => "da-noi",
        KetCucNoi.ChuaHoiDuocCoSo  => "chua-hoi-duoc",
        _                          => "1"
    };

    // ── Giữ danh sách ứng viên của *Tầng 2* qua một lần chuyển hướng ──────────
    //
    // 🔴 Dùng TempData chứ không hỏi lại HIS ở màn GET: hỏi lại là một cuộc gọi
    // nữa sang máy khách cho cùng một câu hỏi, và tệ hơn — danh sách có thể ĐỔI
    // giữa hai lần hỏi, thành ra người dùng tick một đằng rồi nhận một đằng khác.
    // Khóa có kèm ID hồ sơ để danh sách của hồ sơ này không lọt sang hồ sơ kia.

    private string KhoaUngVien(long idHoSo) => $"UngVienNoi_{idHoSo}";

    private void GiuUngVien(KetQuaLuuHoSo ketQua)
    {
        if (ketQua.UngVien is null || ketQua.UngVien.Count == 0) return;

        TempData[KhoaUngVien(ketQua.IdBenhNhan)] =
            System.Text.Json.JsonSerializer.Serialize(ketQua.UngVien);
    }

    private List<SixosPwa.Services.His.HoSoHis>? LayUngVienDaGiu(long idHoSo)
    {
        // Peek chứ không đọc dứt: người dùng tải lại trang (F5) vẫn còn danh sách,
        // không thì họ mất luôn bước xác nhận mà không hiểu vì sao.
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
    /// Phát lại cookie: giữ nguyên mọi claim cũ, chỉ thay claim *hồ sơ đang chọn*.
    ///
    /// Phải chép lại cả bộ chứ không tạo identity mới — mất claim <c>MaCoSo</c>
    /// hay dấu ấn đối tác là phiên tụt xuống trạng thái khác hẳn (ADR 0016).
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

        // Giữ đúng thời hạn của luồng đăng nhập (DangNhapController): phát lại mà
        // quên IsPersistent thì phiên tụt về cookie phiên, đóng trình duyệt là mất.
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
