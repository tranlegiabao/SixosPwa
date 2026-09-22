using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Security;
using SixosPwa.Services;
using SixosPwa.Services.Partner;
using WebPush;

namespace SixosPwa.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly AdminStoredProcedureService _thuTuc;
    private readonly ILuongCongBenhNhan _luong;
    private readonly IHoSoBenhNhanService _hoSo;
    private readonly Services.His.IHisDocService _his;
    private readonly Services.Partner.CuaCoSoService _cuaCoSo;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext db,
        IConfiguration config,
        AdminStoredProcedureService thuTuc,
        ILuongCongBenhNhan luong,
        IHoSoBenhNhanService hoSo,
        Services.His.IHisDocService his,
        Services.Partner.CuaCoSoService cuaCoSo)
    {
        _logger = logger;
        _db = db;
        _config = config;
        _thuTuc = thuTuc;
        _luong = luong;
        _hoSo = hoSo;
        _his = his;
        _cuaCoSo = cuaCoSo;
    }

    /// <summary>
    /// 🔴 TỪ ĐỢT 1B PHẢI TÁCH HAI NGHĨA — trước đây chung một hàm vì cả hai đều
    /// trỏ <c>HT_TaiKhoan</c>:
    ///   * NGƯỜI GỬI thông báo là ADMIN  -> <see cref="LayIdTaiKhoanAdminAsync"/>,
    ///     vẫn đọc <c>HT_TaiKhoan</c> (cột <c>HT_ThongBao.IDNguoiGui</c>).
    ///   * NGƯỜI NHẬN là BỆNH NHÂN       -> <see cref="LayIdHoSoAsync"/>,
    ///     đọc <c>DM_BenhNhan</c> theo CƠ SỞ (cột <c>IDNguoiNhan</c> và
    ///     <c>HT_PushDangKy.IDBenhNhan</c> nay trỏ sang bảng đó — ADR 0037).
    /// Dùng nhầm hàm là câu trả về RỖNG mà không báo lỗi gì.
    /// Các API JSON vẫn GIỮ nguyên tên khóa cũ vì JS trình duyệt đang đọc theo đó.
    /// </summary>
    private Task<long?> LayIdTaiKhoanAdminAsync(string sdt) =>
        _db.TaiKhoans.AsNoTracking()
            .Where(t => t.SDT == sdt)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Hồ sơ bệnh nhân mang số này TẠI CƠ SỞ của phiên (ADR 0036).
    /// 🔴 Lọc <c>IdCoSo != null</c>: dòng neo không được nhận thông báo/push.
    /// Khớp cả Email vì mọi câu đọc khác trong cổng đều khớp cả hai.
    /// </summary>
    private async Task<long?> LayIdHoSoAsync(string dinhDanh)
    {
        if (string.IsNullOrWhiteSpace(dinhDanh)) return null;

        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;

        var q = _db.BenhNhans.AsNoTracking()
            .Where(b => b.IdCoSo != null && (b.SDT == dinhDanh || b.Email == dinhDanh));

        if (!string.IsNullOrWhiteSpace(maCoSo))
            q = q.Where(b => _db.DMCSKCBs.Any(c => c.Id == b.IdCoSo && c.MaCoSo == maCoSo));

        return await q.OrderBy(b => b.Id).Select(b => (long?)b.Id).FirstOrDefaultAsync();
    }


    /// <summary>
    /// Hồ sơ của người đang đăng nhập TẠI CƠ SỞ của phiên. Một chỗ duy nhất —
    /// trước đây câu này chép ở hai màn và để lệch nhau.
    ///
    /// <para>
    /// 🔴 <c>orderby h.Id</c> là bắt buộc (V6a). Script 05 đã bỏ ràng buộc
    /// <c>UK_DM_BenhNhanCoSo_HoSo</c> vì nó trái dữ liệu thật (17,3% bệnh nhân
    /// Thiện Nam có >=2 MaBN tại CÙNG một cơ sở). Trước đây CSDL bảo đảm mỗi
    /// (người, cơ sở) một hồ sơ; nay chỉ còn một phép kiểm ở tầng ứng dụng
    /// (<see cref="Services.Partner.LuongCongBenhNhan"/>). Ngày nào có dòng thứ
    /// hai mà không sắp thứ tự thì SQL Server trả dòng nào là tùy kế hoạch truy
    /// vấn — màn sẽ đổi NGƯỜI giữa hai lần tải mà không báo gì.
    /// </para>
    /// <para>
    /// Còn phải chọn đúng MỘT hồ sơ cho tới khi có *hồ sơ đang chọn* (chốt 2 đợt
    /// 1, ADR 0019) — đó là V6b, đi cùng màn Nối hồ sơ. Tạm thời lấy hồ sơ cũ
    /// nhất và GHI CẢNH BÁO khi có nhiều hơn một, để ngày đó mình biết chứ không
    /// phải đoán.
    /// </para>
    /// <para>
    /// Lọc <c>DaMoTaiLieu</c> ngay tại đây: *Cửa tài liệu* (chốt 9 đợt 1,
    /// ADR 0020) là điều kiện để mở kết quả cận lâm sàng / đơn thuốc.
    /// </para>
    /// </summary>
    private async Task<HoSoDangDung?> LayHoSoDangDungAsync(string dinhDanh, string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(dinhDanh) || string.IsNullOrWhiteSpace(maCoSo))
        {
            ViewBag.SoHoSo = 0;
            return null;
        }

        // 🔴 KHÔNG khớp bằng CCCD (V5). CCCD gõ lúc đăng nhập không được xác
        // thực — OTP chỉ xác thực số điện thoại (A6 đợt 1). Khớp bằng CCCD
        // nghĩa là gõ CCCD người khác là xem được tài liệu của họ.
        //
        // Đợt 1B: phạm vi là cặp (SDT x cơ sở) — luật C2. Một dòng ĐÃ LÀ
        // "con người + hồ sơ tại cơ sở" nên không còn tự nối.
        // 🔴 h.IdCoSo != null là BẮT BUỘC: dòng neo không được hiện ở màn nào.
        var danhSach = await (
            from h in _db.BenhNhans.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals (long?)cs.Id
            where (h.SDT == dinhDanh || h.Email == dinhDanh)
                  && cs.MaCoSo == maCoSo
                  && h.DaMoTaiLieu
            orderby h.Id
            select new HoSoDangDung(h)).ToListAsync();

        ViewBag.SoHoSo = danhSach.Count;

        if (danhSach.Count == 0) return null;

        // *Hồ sơ đang chọn* (ADR 0019): claim quyết định đang xem hồ sơ nào.
        // Claim CHỈ được phát sau khi đã kiểm hồ sơ thuộc tài khoản
        // (HoSoController.Chon), nên ở đây tin được — nhưng vẫn lọc lại trong
        // danh sách đã trả về chứ không tra cứu thẳng theo claim.
        var idDangChon = User.FindFirst(LuongCongBenhNhan.ClaimHoSoDangChon)?.Value;

        if (long.TryParse(idDangChon, out var idChon))
        {
            var khop = danhSach.FirstOrDefault(x => x.BenhNhan.Id == idChon);
            if (khop is not null) return khop;

            // Hồ sơ đang chọn không có mặt ở cơ sở này — chuyển cơ sở hoặc vừa bị
            // xóa. Rơi về hồ sơ đầu tiên, đừng để màn trắng.
            _logger.LogInformation(
                "Ho so dang chon {IdChon} khong co o co so {MaCoSo}, dung ho so {IdThayThe}",
                idChon, maCoSo, danhSach[0].BenhNhan.Id);
        }
        else if (danhSach.Count > 1)
        {
            // Phiên cũ chưa mang claim. Vẫn tất định nhờ orderby, nhưng người
            // dùng chưa chọn được — họ vào *Hồ sơ của tôi* một lần là xong.
            _logger.LogInformation(
                "Tai khoan co {SoHoSo} ho so tai co so {MaCoSo} nhung phien chua co claim ho so dang chon.",
                danhSach.Count, maCoSo);
        }

        return danhSach[0];
    }

    // Action Index (trang bệnh nhân cũ) đã được gỡ bỏ ngày 2026-08-22 theo yêu cầu
    // của user: luồng đó không dùng nữa, thay bằng /benh-nhan. Lấy lại nếu cần:
    //   git show 224341a -- SixosPwa/Views/Home/Index.cshtml

    // Tham số vẫn tên phongKhamId để không phá URL đang chạy; thực chất nó là
    // ID CƠ SỞ — bảng PhongKham đã bị xóa ở đợt tái kiến trúc (W-05).
    public IActionResult TimBacSi(long phongKhamId)
    {
        ViewData["PhongKhamId"] = phongKhamId;
        var coSo = _db.DMCSKCBs.AsNoTracking().FirstOrDefault(p => p.Id == phongKhamId);
        ViewData["TenPhongKham"] = coSo?.TenCoSo ?? "Phòng khám";
        return View();
    }

    public IActionResult HoSoBenhAn(long phongKhamId)
    {
        ViewData["PhongKhamId"] = phongKhamId;
        var coSo = _db.DMCSKCBs.AsNoTracking().FirstOrDefault(p => p.Id == phongKhamId);
        ViewData["TenPhongKham"] = coSo?.TenCoSo ?? "Phòng khám";
        return View();
    }

    [HttpGet("/Home/DanhSachCoSo-{type}")]
    [AllowAnonymous]
    public async Task<IActionResult> DanhSachCoSo(string type)
    {
        string title = "Cơ sở y tế";
        switch (type)
        {
            case "benhvien": title = "Bệnh viện"; break;
            case "pkdk": title = "Phòng khám đa khoa"; break;
            case "nhakhoa": title = "Nha khoa"; break;
            case "phongmach": title = "Phòng mạch"; break;
            case "nhathuoc": title = "Nhà thuốc"; break;
        }
        ViewData["Title"] = title;
        ViewData["Type"] = type;

        var idNhom = await _db.DMNhomCSs.AsNoTracking()
            .Where(nc => nc.MaNhom == type)
            .Select(nc => (long?)nc.ID)
            .FirstOrDefaultAsync();

        var dsCoso = await _db.DMCSKCBs
            .Where(x => x.IdNhomCS == idNhom && x.HienThiCongKhai)
            // QcSoTienDaTra = SỐ TIỀN ĐÃ TRẢ, và là khóa xếp hạng quảng cáo (tên cũ: QuangCao).
            .OrderByDescending(x => x.QcSoTienDaTra)
            .ToListAsync();

        return View(dsCoso);
    }

    /// <summary>
    /// URL cố định của từng cơ sở. Slug do quản trị viên đặt tay (DMCSKCB.Slug),
    /// KHÔNG sinh từ tên, nên đổi tên cơ sở không làm gãy URL đã phát cho đối tác.
    /// </summary>
    [HttpGet("/DangKyOnline/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> ChiTietCoSo(string slug, string? canhBao = null)
    {
        var coSo = await _db.DMCSKCBs.FirstOrDefaultAsync(x => x.Slug == slug);

        if (coSo == null) return NotFound();
        if (!coSo.HienThiCongKhai) return await DayDiKhiCoSoAnAsync(coSo);

        await DoDuLieuCoSoAsync(coSo);
        await DoDoiChieuPhienCoSoAsync(coSo.MaCoSo, canhBao == "1");
        return View();
    }

    /// <summary>
    /// URL cũ khớp cơ sở bằng cách bỏ dấu tên. Giữ lại và chuyển hướng 301 sang
    /// /DangKyOnline/{slug} để mọi đường link đã phát đi không chết.
    /// </summary>
    [HttpGet("/Home/DangKyOnline/{ten?}")]
    [AllowAnonymous]
    public async Task<IActionResult> ChiTietCoSoTheoTen(string? ten, string? diaChi, string? type, string? img, string? logo)
    {
        if (!string.IsNullOrEmpty(ten))
        {
            var cleanTen = RemoveAccentsAndSpaces(ten);
            var allCS = await _db.DMCSKCBs.ToListAsync();
            var matchedCS = allCS.FirstOrDefault(x =>
                RemoveAccentsAndSpaces(x.TenCoSo ?? "").Equals(cleanTen, StringComparison.OrdinalIgnoreCase) ||
                (x.TenCoSo ?? "").Equals(ten, StringComparison.OrdinalIgnoreCase));

            if (matchedCS != null)
            {
                // Chặn ở đây chứ không đợi nhánh 301 phía dưới lộ hổng: cơ sở CHƯA có
                // slug sẽ render thẳng ở :142-144, không đi qua /DangKyOnline/{slug}.
                if (!matchedCS.HienThiCongKhai) return await DayDiKhiCoSoAnAsync(matchedCS);

                // Nam sửa 2026-08-24: trả lại 301 sang /DangKyOnline/{slug}. Render thẳng ở đây
                // thì ViewData thiếu Slug/MaCoSo, kéo theo hai nút bên trang cơ sở mất
                // tham số ?coSo= và luồng bàn giao sang đối tác chết. Xem ADR 0003.
                if (!string.IsNullOrWhiteSpace(matchedCS.Slug))
                {
                    return RedirectPermanent($"/DangKyOnline/{matchedCS.Slug}");
                }

                // Cơ sở chưa được đặt slug: vẫn hiện được trang, chỉ là không có
                // URL cố định. Quản trị viên đặt slug trong màn Admin/CoSoYTe.
                await DoDuLieuCoSoAsync(matchedCS);
                await DoDoiChieuPhienCoSoAsync(matchedCS.MaCoSo, tuDongMoCanhBao: false);
                return View(nameof(ChiTietCoSo));
            }
        }

        ViewData["TenCoSo"] = ten ?? "Cơ sở y tế";
        ViewData["DiaChi"] = diaChi ?? "Đang cập nhật";
        ViewData["Type"] = type ?? "benhvien";
        ViewData["Img"] = img ?? AnhCoSoMacDinh;
        ViewData["Logo"] = logo ?? LogoCoSoMacDinh;
        return View(nameof(ChiTietCoSo));
    }

    /// <summary>
    /// Trang chủ của bệnh nhân tại cơ sở KHÔNG có API riêng. Đợt 2026-08 mới chỉ
    /// đụng giao diện: ba thẻ dịch vụ đều dẫn tới màn "Đang cập nhật".
    /// </summary>
    [HttpGet("/benh-nhan")]
    public async Task<IActionResult> TrangBenhNhan(string? loi = null)
    {
        var cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        var coSo = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);

        // Phải lọc theo CẢ định danh LẪN cơ sở: một số điện thoại có thể có hồ sơ
        // ở nhiều cơ sở khác nhau (dữ liệu thật đang có trường hợp đó), không lọc
        // thì trang chào tên lấy từ hồ sơ của cơ sở KHÁC.
        var hoSoInfo = await LayHoSoDangDungAsync(dinhDanh, maCoSo);

        var benhNhan = hoSoInfo?.BenhNhan;
        var hoSoCoSo = hoSoInfo?.HoSo;

        int soLuongDonThuoc = 0;
        int soLuongKetQuaKham = 0;

        if (coSo != null && hoSoCoSo != null)
        {
            // Chỉ đếm bản MỚI NHẤT: kết quả bị sửa/ký lại giữ lại bản cũ làm đối
            // chứng nhưng không được đếm hai lần. Bỏ nhánh so theo MaBN — từ đợt 2
            // IdBenhNhan đã NOT NULL nên nó chỉ là đường vòng.
            var queryTl = _db.TaiLieuBenhNhans.AsNoTracking()
                .Where(t => t.IdCoSo == coSo.Id && t.IdBenhNhan == hoSoCoSo.Id && t.LaBanMoiNhat);

            // V9 — đếm theo THÀNH VIÊN TẬP, không phải "khác DON_THUOC". Mã lạ
            // (dữ liệu cũ, hoặc HIS gõ sai trước khi cửa API siết) không được
            // lặng lẽ nhảy vào nhóm Kết quả khám nữa.
            soLuongDonThuoc = await queryTl.CountAsync(t => t.LoaiTaiLieu == LoaiTaiLieu.DonThuoc);
            soLuongKetQuaKham = await queryTl.CountAsync(t => LoaiTaiLieu.MaCuaNhomKetQuaKham.Contains(t.LoaiTaiLieu));
        }

        ViewBag.SoLuongDonThuoc = soLuongDonThuoc;
        ViewBag.SoLuongKetQuaKham = soLuongKetQuaKham;

        ViewBag.MaCoSo = maCoSo;
        ViewBag.TenCoSo = coSo?.TenCoSo ?? "Cơ sở khám chữa bệnh";
        ViewBag.TenBenhNhan = benhNhan?.TenBN ?? dinhDanh;

        // Tự động tìm kiếm và nối mã bệnh nhân từ HIS nếu hồ sơ hiện tại chưa có MaBN
        if (coSo != null && hoSoCoSo != null && string.IsNullOrWhiteSpace(hoSoCoSo.MaBN) && benhNhan != null)
        {
            if (benhNhan.NgaySinh != null && !string.IsNullOrWhiteSpace(benhNhan.GioiTinh) && !string.IsNullOrWhiteSpace(benhNhan.TenBN))
            {
                try
                {
                    var traLoiHis = await _his.TraCuuHoSoAsync(coSo.Id, benhNhan.CCCD ?? "", benhNhan.TenBN, benhNhan.NgaySinh.Value, benhNhan.GioiTinh);
                    if (traLoiHis.DuLieu is { Count: 1 } && !string.IsNullOrWhiteSpace(traLoiHis.DuLieu[0].MaBN))
                    {
                        var maHis = traLoiHis.DuLieu[0].MaBN!.Trim();
                        await _thuTuc.SaveBenhNhanCoSoAsync(benhNhan.Id, coSo.Id, maHis, true);
                        hoSoCoSo.MaBN = maHis;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi tự động tra cứu mã HIS cho bệnh nhân {IdBenhNhan}", benhNhan.Id);
                }
            }
        }

        // 🔴 Khối *Mã hồ sơ* của giao diện mới (nhánh bk) đọc ViewBag.MaBN
        ViewBag.MaBN = hoSoCoSo?.MaBN;

        // Logo + đường ra khỏi trang bệnh nhân. Trước đây màn này không có lối nào
        // quay lại phần công khai, mà từ 2026-08-27 "/" lại đẩy ngược về đây, nên
        // thiếu nó là bệnh nhân bị nhốt. Trỏ tới DANH SÁCH cơ sở chứ không trỏ "/":
        // trỏ "/" là thành nút chết vì "/" sẽ đẩy về lại đây.
        //
        // UnescapeDataString bám theo ChiTietCoSo.cshtml:7 — URL trong cột Logo có
        // thể đã bị mã hóa một lần trước khi lưu.
        ViewBag.LogoCoSo = string.IsNullOrWhiteSpace(coSo?.Logo)
            ? null
            : Uri.UnescapeDataString(coSo.Logo);

        ViewBag.MaNhomCS = (coSo?.IdNhomCS is null
            ? null
            : await _db.DMNhomCSs.AsNoTracking()
                .Where(n => n.ID == coSo.IdNhomCS.Value)
                .Select(n => n.MaNhom)
                .FirstOrDefaultAsync())
            ?? "benhvien";
        // 🔴 HAI ô này thuộc về *Hồ sơ đang chọn*, không phải chủ tài khoản — đúng
        // một khuôn với ViewBag.TenBenhNhan và ViewBag.MaBN ngay trên. Trước đây
        // chúng đọc claim, nên tài khoản mở hồ sơ người thân thì thể hiện TÊN người
        // thân nhưng CCCD/điện thoại của NGƯỜI CẦM ĐIỆN THOẠI. Claim chỉ còn là
        // đường lùi cho hồ sơ chưa khai đủ (xem CONTEXT.md mục *Hồ sơ đang chọn*).
        ViewBag.DienThoai = benhNhan?.SDT ?? User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
        ViewBag.CccdCheBot = CheBotCccd(benhNhan?.CCCD ?? cccd);
        ViewBag.CoLoiKetNoi = loi == "khong-ket-noi-duoc";

        // Cơ sở dùng bộ màn của đối tác: mật khẩu là CỦA HỌ, bệnh nhân đổi trên
        // trang của cơ sở. Ẩn mục "Đổi mật khẩu" đi cho khỏi dẫn tới ngõ cụt. ADR 0014.
        // Sau đợt A, "dùng bộ màn của đối tác" không còn là KIỂU bàn cãi mà là DỮ LIỆU:
        // có KetNoi_UrlChuyenHuong (và KetNoi_Active) thì cơ sở có cửa riêng.
        ViewBag.DungManDoiTac = coSo != null && await _cuaCoSo.CoChuyenHuongAsync(coSo.Id);

        // ── CÁI VỎ của ô *Lịch khám của tôi* (ADR 0025) ───────────────────
        // 🔴 Cơ sở CHƯA NỐI thì ô BIẾN MẤT khỏi trang, không hiện rồi báo lỗi:
        // ở cơ sở chưa nối thì 100% số lần bấm là bấm vào thứ không dùng được.
        // Đã có tiền lệ ngay trong glossary — ô *Đăng ký khám theo gói* ẨN tới
        // giai đoạn 3. Phép kiểm này chỉ đọc CSDL cổng, KHÔNG gọi sang HIS:
        // hỏi HIS là việc của lúc người dùng BẤM mở ô.
        ViewBag.HienOLichKham = coSo != null && await _his.CoNoiHisAsync(coSo.Id);

        // ── Dai nhac hoan thien ho so (ADR 0024, chot 12) ──────────────────
        // 🔴 NHAC MEM, KHONG CHAN. Chan cung theo "thieu truong" se khoa luon
        // nhung ho so DA NOI MA ma con khuyet du lieu — dung trang thai cua tai
        // khoan nghiem thu 0363982926 (co MaBN 100992, thieu ngay sinh): mot man
        // dang chay va dang phuc vu tai lieu that bong thanh man chan.
        // Cho mốc nếu sau này đổi ý: Services/Partner/LuongCongBenhNhan.cs — cổng
        // hạ cánh duy nhất sau xác thực.
        ViewBag.HoSoThieuTruong = benhNhan != null
            && (LaTenTam(benhNhan.TenBN, dinhDanh)
                || benhNhan.NgaySinh is null
                || string.IsNullOrWhiteSpace(benhNhan.GioiTinh));

        ViewBag.IdHoSoDangXem = benhNhan?.Id;

        return View();
    }

    /// <summary>
    /// Tên hồ sơ mang SỐ ĐIỆN THOẠI chứ không phải tên người — 14/19 tài khoản đo
    /// trên cổng ngày 2026-09-09. Chúng tự đẻ ra lúc đăng ký bằng OTP, không bao
    /// giờ đi qua màn *Thêm hồ sơ*.
    /// </summary>
    private static bool LaTenTam(string? ten, string dinhDanh)
    {
        if (string.IsNullOrWhiteSpace(ten)) return true;

        var t = ten.Trim();

        if (string.Equals(t, dinhDanh?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

        // Toàn chữ số (có thể có dấu + ở đầu) => là số điện thoại, không phải tên.
        return t.TrimStart('+').All(char.IsDigit);
    }

    [HttpGet("/benh-nhan/tai-lieu")]
    public async Task<IActionResult> DanhSachTaiLieu(string? nhom = null)
    {
        var cccd = User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value;
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        var coSo = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);

        var hoSoInfo = await LayHoSoDangDungAsync(dinhDanh, maCoSo);

        ViewBag.MaCoSo = maCoSo;
        ViewBag.TenCoSo = coSo?.TenCoSo ?? "Cơ sở khám chữa bệnh";
        ViewBag.TenBenhNhan = hoSoInfo?.BenhNhan?.TenBN ?? dinhDanh;
        ViewBag.MaBN = hoSoInfo?.HoSo.MaBN;
        var nhomChuan = string.IsNullOrWhiteSpace(nhom) ? LoaiTaiLieu.NhomTatCa : nhom.Trim().ToLowerInvariant();
        ViewBag.Nhom = nhomChuan;
        ViewBag.TieuDeTrang = LoaiTaiLieu.TieuDeNhom(nhomChuan);

        List<TaiLieuBenhNhan> danhSach = new();
        if (coSo != null && hoSoInfo != null)
        {
            var idBnCoSo = hoSoInfo.HoSo.Id;

            var q = LocTaiLieu(coSo.Id, idBnCoSo, nhomChuan);

            // V9 phân trang: MỐC CHỤP. Mọi mẻ sau đều kèm t.Id <= mocId, nếu không thì tài
            // liệu HIS đẩy vào giữa lúc đang cuộn sẽ chen lên đầu => thẻ lặp hoặc nhảy cóc.
            var mocId = await q.MaxAsync(t => (long?)t.Id) ?? 0L;
            q = q.Where(t => t.Id <= mocId);

            ViewBag.MocId = mocId;
            ViewBag.TongSo = await q.CountAsync();

            danhSach = await q
                .OrderByDescending(t => t.NgayKham ?? t.NgayTao)
                .ThenByDescending(t => t.Id)
                .Take(KichThuocMeTaiLieu)
                .ToListAsync();
        }
        else
        {
            ViewBag.MocId = 0L;
            ViewBag.TongSo = 0;
        }

        return View(danhSach);
    }

    /// <summary>Kích thước một mẻ tài liệu (chốt 1 của plan 2026-09-17).</summary>
    private const int KichThuocMeTaiLieu = 50;

    /// <summary>
    /// Bộ lọc tài liệu dùng CHUNG cho mẻ đầu (DanhSachTaiLieu) và mẻ sau (MeTaiLieu).
    /// Lệch một chỗ là mẻ sau trả sai nhóm.
    /// </summary>
    private IQueryable<TaiLieuBenhNhan> LocTaiLieu(long idCoSo, long idBnCoSo, string nhomChuan)
    {
        var q = _db.TaiLieuBenhNhans.AsNoTracking()
            .Where(t => t.IdCoSo == idCoSo && t.IdBenhNhan == idBnCoSo && t.LaBanMoiNhat);

        if (nhomChuan == LoaiTaiLieu.NhomDonThuoc)
        {
            q = q.Where(t => t.LoaiTaiLieu == LoaiTaiLieu.DonThuoc);
        }
        else if (nhomChuan == LoaiTaiLieu.NhomKetQuaKham)
        {
            q = q.Where(t => LoaiTaiLieu.MaCuaNhomKetQuaKham.Contains(t.LoaiTaiLieu));
        }

        return q;
    }

    /// <summary>
    /// Mẻ tài liệu tiếp theo — trả về HTML partial thẻ tài liệu (không phải JSON), để mẻ sau
    /// dùng đúng khuôn markup với mẻ đầu.
    /// </summary>
    [HttpGet("/benh-nhan/tai-lieu/me")]
    public async Task<IActionResult> MeTaiLieu(string? nhom, int boQua, long mocId)
    {
        // Tự dùng lại danh tính — KHÔNG nhận idBenhNhan từ trình duyệt.
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        var coSo = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);
        var hoSoInfo = await LayHoSoDangDungAsync(dinhDanh, maCoSo);
        if (coSo == null || hoSoInfo == null) return PartialView("_TheTaiLieu", new List<TaiLieuBenhNhan>());

        var nhomChuan = string.IsNullOrWhiteSpace(nhom) ? LoaiTaiLieu.NhomTatCa : nhom.Trim().ToLowerInvariant();
        if (boQua < 0) boQua = 0;

        var danhSach = await LocTaiLieu(coSo.Id, hoSoInfo.HoSo.Id, nhomChuan)
            .Where(t => t.Id <= mocId)
            .OrderByDescending(t => t.NgayKham ?? t.NgayTao)
            .ThenByDescending(t => t.Id)
            .Skip(boQua)
            .Take(KichThuocMeTaiLieu)
            .ToListAsync();

        return PartialView("_TheTaiLieu", danhSach);
    }

    /// <summary>
    /// ADR 0033 — danh sách tài liệu CÙNG LOẠI (id + tên + ngày) cho trình xem tự đi,
    /// không phụ thuộc số thẻ đã nạp trên màn. KHÔNG kèm DuongDanFtp, KHÔNG tải PDF.
    /// </summary>
    [HttpGet("/benh-nhan/tai-lieu/cung-loai")]
    public async Task<IActionResult> DanhSachCungLoai(string loai)
    {
        var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        var dinhDanh = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

        var coSo = string.IsNullOrWhiteSpace(maCoSo)
            ? null
            : await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);
        var hoSoInfo = await LayHoSoDangDungAsync(dinhDanh, maCoSo);
        if (coSo == null || hoSoInfo == null) return Json(Array.Empty<object>());

        var idBnCoSo = hoSoInfo.HoSo.Id;
        var danhSach = await _db.TaiLieuBenhNhans.AsNoTracking()
            .Where(t => t.IdCoSo == coSo.Id && t.IdBenhNhan == idBnCoSo && t.LaBanMoiNhat
                        && t.LoaiTaiLieu == loai)
            .OrderByDescending(t => t.NgayKham ?? t.NgayTao)
            .ThenByDescending(t => t.Id)
            .Select(t => new
            {
                id = t.Id,
                ten = t.TenTaiLieu,
                ngay = (t.NgayKham ?? t.NgayTao).ToString("dd' thg 'MM")
            })
            .ToListAsync();

        return Json(danhSach);
    }

    /// <summary>
    /// Giờ làm việc này nằm ở bảng con DM_CSKCB_GioLamViec (một dòng mỗi thứ), không
    /// còn là cặp cột TGLamViec/NgayLamViec với nội dung ngược tên cột như trước.
    /// </summary>
    private async Task<string?> GetOperatingHoursValueAsync(DMCSKCB coSo)
    {
        var gio = await _db.CSKCBGioLamViecs.AsNoTracking()
            .Where(x => x.IdCoSo == coSo.Id)
            .OrderBy(x => x.Thu)
            .ToListAsync();

        if (gio.Count == 0) return null;

        return OperatingHours.Encode(
            FormatWorkingDays(gio.Select(x => x.Thu)),
            gio[0].GioMoCua.ToString(@"hh\:mm"),
            gio[0].GioDongCua.ToString(@"hh\:mm"));
    }

    /// <summary>
    /// DM_CSKCB_GioLamViec lưu Thứ theo quy ước 0 = Chủ nhật, 1..6 = Thứ 2..Thứ 7.
    /// Chuyển danh sách số trong DB thành nhãn dễ giao diện, không hiện "1,2,3...".
    /// </summary>
    private static string FormatWorkingDays(IEnumerable<byte> storedDays)
    {
        var days = storedDays
            .Select(day => day is 7 or 8 ? (byte)0 : day)
            .Where(day => day <= 6)
            .Distinct()
            .OrderBy(day => day)
            .ToArray();

        if (days.SequenceEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6 })) return "Thứ 2 - Chủ nhật";
        if (days.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6 })) return "Thứ 2 - Thứ 7";
        if (days.SequenceEqual(new byte[] { 1, 2, 3, 4, 5 })) return "Thứ 2 - Thứ 6";
        if (days.SequenceEqual(new byte[] { 0, 6 })) return "Thứ 7 - Chủ nhật";

        return string.Join(", ", days.Select(day => day switch
        {
            0 => "Chủ nhật",
            1 => "Thứ 2",
            2 => "Thứ 3",
            3 => "Thứ 4",
            4 => "Thứ 5",
            5 => "Thứ 6",
            6 => "Thứ 7",
            _ => string.Empty
        }));
    }

    [HttpGet("/benh-nhan/sap-co")]
    public async Task<IActionResult> SapCo(string? muc = null)
    {
        // Kế hoạch điều trị chỉ dành cho cơ sở nha khoa. Nếu cơ sở khác cố tình vào trực tiếp thì chuyển về /benh-nhan.
        if (string.Equals(muc, "ke-hoach-dieu-tri", StringComparison.OrdinalIgnoreCase))
        {
            var maCoSo = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
            var coSo = string.IsNullOrWhiteSpace(maCoSo)
                ? null
                : await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.MaCoSo == maCoSo);

            var maNhom = coSo?.IdNhomCS is null
                ? null
                : await _db.DMNhomCSs.AsNoTracking()
                    .Where(n => n.ID == coSo.IdNhomCS.Value)
                    .Select(n => n.MaNhom)
                    .FirstOrDefaultAsync();

            if (!string.Equals(maNhom, "nhakhoa", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(TrangBenhNhan));
            }
        }

        (ViewBag.TenMuc, ViewBag.BieuTuong) = muc switch
        {
            "lich-hen" => ("Lịch hẹn", "📅"),
            "don-thuoc" => ("Đơn thuốc", "💊"),
            "ket-qua-kham" => ("Kết quả khám bệnh", "📋"),
            "quan-ly-hoa-don" => ("Quản lý hóa đơn", "🧾"),
            "ke-hoach-dieu-tri" => ("Kế hoạch điều trị", "🩺"),
            "dat-goi-kham" => ("Đăng ký khám theo gói", "▤"),
            "lich-su-hen" => ("Lịch sử hẹn khám", "◷"),
            "ho-so-kham" => ("Tra cứu hồ sơ khám bệnh", "◫"),
            _ => ("Chức năng", "◌")
        };

        return View();
    }

    /// <summary>Che bớt số CCCD khi hiện trên màn: 0772•••••069.</summary>
    private static string CheBotCccd(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return "—";
        if (cccd.Length <= 7) return cccd;

        return $"{cccd[..4]}{new string('\u2022', cccd.Length - 7)}{cccd[^3..]}";
    }

    private const string AnhCoSoMacDinh = "https://images.unsplash.com/photo-1519494026892-80bbd2d6fd0d?w=800&q=80";
    private const string LogoCoSoMacDinh = "https://tse1.mm.bing.net/th/id/OIP.JgUNpJPll-8BkzE3XN6LggHaHa?r=0&pid=Api&P=0&h=180";

    /// <summary>
    /// Đổ dữ liệu một cơ sở ra ViewData cho trang chi tiết. MaCoSo và Slug là hai
    /// thứ hai nút "Đăng ký khám" / "Đăng nhập" phải mang theo — thiếu chúng thì
    /// màn đăng nhập không biết bệnh nhân đang ở cơ sở nào.
    /// </summary>
    private async Task DoDuLieuCoSoAsync(DMCSKCB coSo)
    {
        ViewData["CoSoYTe"] = coSo;
        ViewData["MaCoSo"] = coSo.MaCoSo;
        ViewData["Slug"] = coSo.Slug;
        ViewData["TenCoSo"] = coSo.TenCoSo;
        ViewData["DiaChi"] = coSo.DiaChi ?? "Đang cập nhật";
        ViewData["Type"] = (coSo.IdNhomCS is null
            ? null
            : await _db.DMNhomCSs.AsNoTracking()
                .Where(x => x.ID == coSo.IdNhomCS)
                .Select(x => x.MaNhom)
                .FirstOrDefaultAsync()) ?? "benhvien";
        ViewData["Img"] = coSo.AnhBia ?? AnhCoSoMacDinh;
        ViewData["Logo"] = coSo.Logo ?? LogoCoSoMacDinh;
        ViewData["TGLamViec"] = await GetOperatingHoursValueAsync(coSo);
        ViewData["NoiDungCskcb"] = await LoadNoiDungAsync(coSo);
    }

    /// <summary>
    /// So MaCoSo của phiên với cơ sở đang xem, để trang tự quyết định có bật modal
    /// chặn đăng nhập chéo cơ sở hay không. Chỉ so — KHÔNG tự đổi claim, việc đổi
    /// (nếu bệnh nhân đồng ý) đi qua DangXuat rồi Login bình thường.
    /// </summary>
    private async Task DoDoiChieuPhienCoSoAsync(string? maCoSoTrang, bool tuDongMoCanhBao)
    {
        ViewData["TuDongMoCanhBao"] = tuDongMoCanhBao;

        var maCoSoPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
        if (string.IsNullOrWhiteSpace(maCoSoPhien) || maCoSoPhien == maCoSoTrang)
        {
            ViewData["PhienCoSoKhac"] = null;
            return;
        }

        var coSoKhac = await _db.DMCSKCBs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaCoSo == maCoSoPhien);
        ViewData["PhienCoSoKhac"] = coSoKhac?.TenCoSo ?? "cơ sở khác";
    }

    /// <summary>
    /// Cơ sở đang ẨN (DM_CSKCB.HienThiCongKhai = 0) thì đẩy khách về danh sách công khai của
    /// nhóm nó — 302 trần, không bằng thông báo. Không suy được nhóm (IdNhomCS rỗng,
    /// ví dụ cơ sở ID=11) thì về trang chủ. Xem ADR 0013.
    /// </summary>
    private async Task<IActionResult> DayDiKhiCoSoAnAsync(DMCSKCB coSo)
    {
        if (coSo.IdNhomCS is not long idNhom) return Redirect("/");

        var maNhom = await _db.DMNhomCSs.AsNoTracking()
            .Where(nc => nc.ID == idNhom)
            .Select(nc => nc.MaNhom)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(maNhom)
            ? Redirect("/")
            : Redirect($"/Home/DanhSachCoSo-{maNhom}");
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadNoiDungAsync(DMCSKCB coSo)
    {
        var items = await _db.NDCSKCBs.AsNoTracking()
            .Where(x => x.IdCoSo == coSo.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var maTheoId = (await _db.DMChuDes.AsNoTracking().ToListAsync())
            .ToDictionary(x => x.ID, x => x.MaChuDe);

        return items
            .Where(x => maTheoId.ContainsKey(x.IdChuDe))
            .Select(x => new { Item = x, Ma = maTheoId[x.IdChuDe] })
            .Where(x => NDCSKCB.AllowedLoaiND.Contains(x.Ma, StringComparer.OrdinalIgnoreCase))
            .GroupBy(x => x.Ma, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Item.NoiDung ?? "", StringComparer.OrdinalIgnoreCase);
    }

    private static string RemoveAccentsAndSpaces(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        string normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        string cleanText = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

        cleanText = cleanText.Replace("đ", "d").Replace("Đ", "D");

        var finalSb = new System.Text.StringBuilder();
        foreach (char c in cleanText)
        {
            if (char.IsLetterOrDigit(c))
            {
                finalSb.Append(c);
            }
        }
        return finalSb.ToString();
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ThongTinBenhNhan()
    {
        // "/" là start_url của PWA, nên đây chính là chỗ bệnh nhân đáp xuống mỗi
        // lần MỞ NGUỘI app từ biểu tượng. Đã đăng nhập thì không còn lý do xem
        // trang quảng bá công khai — đi thẳng trang bệnh nhân.
        //
        // 🔴 CHỈ đẩy khi MỞ NGUỘI (khởi động lại app / vào bằng bookmark / gõ thẳng
        // URL) — lúc đó không có Referer cùng host. Bấm trong app tới "/" (ví dụ nút
        // logo ở header trang danh sách cơ sở trỏ về đây) thì CÓ Referer cùng host,
        // phải render "/" bình thường chứ KHÔNG bắt ngược về /benh-nhan. Đây đúng
        // phép thử mà "PWA Last Page Restore" trong _Layout.cshtml dùng.
        //
        // 🔴 Vế "có claim Cccd" là BẮT BUỘC. Khu Admin ký CẢ HAI cookie (xem
        // _Layout.cshtml), nên admin cũng tính là IsAuthenticated — nhưng họ không
        // có claim Cccd/MaCoSo, đẩy họ sang /benh-nhan là ra trang rỗng không biết
        // chào ai. Xem ADR 0016.
        var referer = Request.Headers["Referer"].ToString();
        var tuTrongApp = !string.IsNullOrEmpty(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var refUri)
            && string.Equals(refUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase);

        if (!tuTrongApp
            && User.Identity?.IsAuthenticated == true
            && !string.IsNullOrWhiteSpace(User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value))
        {
            // 🔴 Với cơ sở dùng BỘ MÀN CỦA ĐỐI TÁC (Ung Bướu), /benh-nhan là SAI đích.
            // Bệnh nhân đó sống ở TrangChu bên đối tác; trang bệnh nhân nội bộ chỉ là
            // một trạm dừng không ai muốn. Phiên đã mang dấu ấn thì mở nguội phải đi
            // THẲNG sang họ, đúng như lúc bấm nút trong app.
            //
            // Ủy thác cho /DangNhap/DiTiep chứ KHÔNG chép lại cây quyết định: nó đã
            // giữ đủ ba vế (có CCCD - có dấu ấn - đúng cơ sở của phiên) và gọi
            // DangNhapLaiBangMatKhauDaCatAsync để HỎI ĐỐI TÁC trước khi bàn giao
            // (ADR 0016 mục 2). Mọi nhánh thoát của nó đều là trang cuối — /benh-nhan,
            // /DangNhap/Login?coSo=, /DangNhap/BanGiao?coSo= — nên không thể vòng lại "/".
            //
            // Đối tác chết thì DiTiep rồi xuống ChonDichDenAsync => màn đăng nhập của
            // cơ sở kèm câu báo sự cố, KHÔNG phải /benh-nhan. Đó là đánh đổi đã biết
            // của ADR 0016 mục 2, không phải lỗ thủng mới.
            //
            // SỬA ADR 0016 mục 5 ngày 2026-08-27: trước đó MỌI phiên mở nguội đều về
            // /benh-nhan, kể cả phiên của cơ sở đối tác.
            //
            // 🔴 Đợt A: điều kiện "phiên có dấu ấn ClaimDoiTacXacThuc" đã bị bỏ. Đợt A
            // gỡ hết chỗ phát claim đó (danh sách claim trong Login và
            // CapPhienBenhNhanAsync), nên giữ nó lại là khóa chết cả nhánh này: cơ sở
            // có KetNoi_UrlChuyenHuong vẫn bị đem về /benh-nhan. Quyết định này này
            // đi bằng DỮ LIỆU (V10) — chỉ hỏi CuaCoSoService.
            var maCoSoPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
            if (!string.IsNullOrWhiteSpace(maCoSoPhien))
            {
                var idCoSoPhien = await _db.DMCSKCBs.AsNoTracking()
                    .Where(x => x.MaCoSo == maCoSoPhien)
                    .Select(x => (long?)x.Id)
                    .FirstOrDefaultAsync();
                if (idCoSoPhien is > 0 && await _cuaCoSo.CoChuyenHuongAsync(idCoSoPhien.Value))
                {
                    return Redirect("/DangNhap/DiTiep");
                }
            }

            return Redirect("/benh-nhan");
        }

        var topCSKCBList = new List<TopCSKCBQC>();
        try
        {
            var conn = _db.Database.GetDbConnection();
            bool wasClosed = conn.State == System.Data.ConnectionState.Closed;
            if (wasClosed)
            {
                await conn.OpenAsync();
            }

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                // Thủ tục cũ Top_CSKCB_QC đã bị V002 xóa. Bản mới sửa lỗi nhận dòng
                // của nó (W-03) và nhận @SoLuong thay vì khóa cứng TOP 5.
                cmd.CommandText = "DM_CSKCB_TopQuangCao";
                var pSoLuong = cmd.CreateParameter();
                pSoLuong.ParameterName = "@SoLuong";
                pSoLuong.Value = 5;
                cmd.Parameters.Add(pSoLuong);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    topCSKCBList.Add(new TopCSKCBQC
                    {
                        TenCoSo = reader["TenCoSo"]?.ToString() ?? "",
                        NoiDung = reader["NoiDung"]?.ToString() ?? "",
                        Img = reader["Img"]?.ToString() ?? ""
                    });
                }
            }
            finally
            {
                if (wasClosed && conn.State == System.Data.ConnectionState.Open)
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stored procedure DM_CSKCB_TopQuangCao");
        }

        ViewData["TopCSKCB"] = topCSKCBList;

        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
        {
            return View(new List<DotKham>());
        }

        ViewData["UserName"] = sdt;
        ViewData["UserRole"] = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "BenhNhan";

        // Hồ sơ con người. KHÔNG tự tạo ở đây: đây là một GET, mà khối tự tạo cũ
        // chính là nguồn của những dòng rác MaDT = 'DT001' (phát hiện C-02).
        var benhNhan = await _db.BenhNhans.FirstOrDefaultAsync(b => b.SDT == sdt);

        if (benhNhan is null)
        {
            // Tài khoản vừa đăng ký OTP nhưng chưa có hồ sơ — danh sách rỗng là
            // KẾT QUẢ ĐÚNG, đừng thay bằng dữ liệu mẫu.
            ViewData["MaBN"] = "";
            ViewData["TenBN"] = "";
            ViewData["DiaChi"] = "";
            ViewData["Email"] = "";
            return View(new List<DotKham>());
        }

        // Đợt 1B: mã nằm ngay trên chính dòng hồ sơ (ADR 0032 — một hồ sơ một mã).
        ViewData["MaBN"] = benhNhan.MaBN ?? "";
        ViewData["TenBN"] = benhNhan.TenBN;
        ViewData["DiaChi"] = benhNhan.DiaChi ?? "";
        ViewData["Email"] = benhNhan.Email ?? "";

        // Đợt 1B: một dòng = một hồ sơ tại một cơ sở, giữ ĐÚNG MỘT mã
        // (ADR 0032), nên đợt khám treo thẳng vào dòng đó.
        var dotKham = await _db.DotKhams
            .Where(dk => dk.IdBenhNhan == benhNhan.Id)
            .OrderByDescending(dk => dk.NgayGioVao)
            .ToListAsync();

        return View(dotKham);
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> GuiTinNhan()
    {
        // Đợt 1B (C16/PA-1): bảng DM_DoiTac đã bỏ, "công ty" này là một cột phẳng
        // trên DM_CSKCB. Màn chỉ còn cần danh sách TÊN CÔNG TY để lọc.
        var congTy = await _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.TenCongTy != null && x.TenCongTy != "")
            .Select(x => x.TenCongTy!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return View(congTy);
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public IActionResult LocDanhSachBN([FromBody] LocBNRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.TenCongTy))
            return Json(new { success = false, message = "Vui lòng chọn công ty." });

        try
        {
            // 🔴 Đợt 1B đã BỎ hẳn phép "xác thực đối tác" ở đây. Nó không xác thực gì
            // cả: trang tự điền mật khẩu vào ô (data-password in thẳng ra HTML), rồi
            // controller so lại đúng chuỗi vừa tự điền. Màn này vốn đã
            // [Authorize(Roles = "Admin")], đó mới là cửa thật.
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = "DM_BenhNhan_Loc";

            foreach (var (ten, giaTri) in new (string, object?)[]
                     { ("@TuKhoa", null), ("@IDCoSo", null), ("@TenCongTy", model.TenCongTy),
                       ("@Trang", 1), ("@CoTrang", 1000) })
            {
                var p = cmd.CreateParameter();
                p.ParameterName = ten;
                p.Value = giaTri ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            using var reader = cmd.ExecuteReader();

            var list = new List<object>();
            while (reader.Read())
            {
                list.Add(new
                {
                    id    = reader["IDBenhNhan"],
                    maBN  = reader["MaBN"].ToString(),
                    maCoSo = reader["MaCoSo"].ToString(),
                    tenCoSo = reader["TenCoSo"]?.ToString() ?? "",
                    sdt   = reader["SDT"]?.ToString() ?? "",
                    tenBN = reader["TenBN"].ToString(),
                    diaChi = reader["DiaChi"]?.ToString() ?? "",
                    email  = reader["Email"]?.ToString() ?? ""
                });
            }

            return Json(new { success = true, data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi stored procedure DM_BenhNhan_Loc");
            return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult VapidPublicKey()
    {
        var publicKey = _config["Vapid:PublicKey"] ?? "";
        return Json(new { publicKey });
    }

    // Lấy danh sách tài khoản bệnh nhân thật từ DB (cho GuiTinNhan dùng)
    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> DanhSachNguoiDung()
    {
        var danhSach = await _db.TaiKhoans
            .Where(t => t.Role != "Admin")
            .Select(t => new { t.Id, t.SDT, t.Role })
            .ToListAsync();
        return Json(danhSach);
    }

    [HttpPost]
    public async Task<IActionResult> DangKyPush([FromBody] PushSubscriptionRequest model)
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt) || string.IsNullOrEmpty(model.Endpoint))
            return Json(new { success = false });

        // Push neo vào DM_BenhNhan từ đợt 1B (ADR 0037).
        var idTaiKhoan = await LayIdHoSoAsync(sdt);
        if (idTaiKhoan is null)
            return Json(new { success = false });

        // Thủ tục tự lo phần "đã có endpoint này chưa" (ADR 0008).
        var ketQua = await _thuTuc.SavePushDangKyAsync(
            idTaiKhoan.Value,
            model.Endpoint,
            model.P256dh ?? "",
            // model.DeviceId không còn được lưu: cột HT_PushDangKy.IDThietBi và bảng
            // HT_ThietBi đã bị xóa ở đợt A. Khóa tự nhiên của một đăng ký là Endpoint.
            model.Auth ?? "");

        if (!ketQua.KetQua.Succeeded)
            return Json(new { success = false, message = ketQua.KetQua.Message });
        _logger.LogInformation("Đăng ký push thành công cho {SDT}", sdt);
        return Json(new { success = true });
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> GuiTinNhan([FromBody] SendSmsRequest model)
    {
        var nguoiGui = User.Identity?.Name;
        if (string.IsNullOrEmpty(nguoiGui))
            return Json(new { success = false, message = "Không tìm thấy thông tin đăng nhập!" });

        var smsMessage = string.IsNullOrWhiteSpace(model.Message) ? "test api gửi tin nhắn" : model.Message.Trim();
        var danhSachNhan = model.DanhSachNguoiNhan ?? new List<string>();

        if (danhSachNhan.Count == 0)
            return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 bệnh nhân!" });

        var now = DateTime.Now;

        var idNguoiGui = await LayIdTaiKhoanAdminAsync(nguoiGui);   // nguoi gui = Admin
        if (idNguoiGui is null)
            return Json(new { success = false, message = "Không tìm thấy tài khoản người gửi!" });

        // Lưu ThongBao — mỗi dòng một lần gọi thủ tục (ADR 0008).
        // 🔴 Người NHẬN này là DM_BenhNhan (FK_HT_ThongBao_NguoiNhan — ADR 0037),
        // không còn HT_TaiKhoan. Tra nhầm bảng là tra điền RỖNG => không dòng thông
        // báo nào được ghi mà endpoint vẫn báo thành công.
        // Một số có thể ứng NHIỀU hồ sơ (nhiều cơ sở) => gửi cho tất cả.
        var hoSoTheoSdt = await _db.BenhNhans.AsNoTracking()
            .Where(b => b.IdCoSo != null && b.SDT != null && danhSachNhan.Contains(b.SDT))
            .Select(b => new { Sdt = b.SDT!, b.Id })
            .ToListAsync();

        var idTheoSdt = hoSoTheoSdt
            .GroupBy(x => x.Sdt)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        foreach (var sdtNhan in danhSachNhan)
        {
            if (idTheoSdt.TryGetValue(sdtNhan, out var dsIdNhan))
                foreach (var idNhan in dsIdNhan)
                    await _thuTuc.SaveThongBaoAsync(idNguoiGui.Value, idNhan, smsMessage);
        }

        var vapidPublicKey = _config["Vapid:PublicKey"] ?? "";
        var vapidPrivateKey = _config["Vapid:PrivateKey"] ?? "";
        var vapidSubject = _config["Vapid:Subject"] ?? "mailto:admin@hissoft.vn";

        var webPushClient = new WebPushClient();
        webPushClient.SetVapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);

        // 🔴 HT_PushDangKy.IDBenhNhan tro DM_BenhNhan tu dot 1B (ADR 0037).
        // Join sang HT_TaiKhoan la khong ra gi, va co the trung nham mot Admin
        // neu hai day ID tinh co gap nhau.
        var danhSachSubscription = await (
            from p in _db.PushDangKys.AsNoTracking()
            join b in _db.BenhNhans.AsNoTracking() on p.IdBenhNhan equals b.Id
            where b.SDT != null && danhSachNhan.Contains(b.SDT)
            select new { Sub = p, SDT = b.SDT! }).ToListAsync();

        int pushOk = 0, pushFail = 0;
        foreach (var item in danhSachSubscription)
        {
            try
            {
                var sub = item.Sub;
                var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title = "ðŸ’¬ Tin nhắn mới từ HisSoft",
                    body = smsMessage,
                    icon = "/static/icon-192.png",
                    badge = "/static/icon-192.png",
                    sender = nguoiGui,
                    url = "/"
                });
                await webPushClient.SendNotificationAsync(subscription, payload);
                pushOk++;
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                           || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription hết hạn — dọn ngay qua thủ tục (ADR 0008).
                await _thuTuc.XoaPushDangKyAsync(item.Sub.Endpoint);
                pushFail++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi gửi push cho {SDT}: {Message}", item.SDT, ex.Message);
                pushFail++;
            }
        }

        _logger.LogInformation("Gửi {Total} thông báo: {Ok} push thành công, {Fail} lỗi",
            danhSachNhan.Count, pushOk, pushFail);

        return Json(new
        {
            success = true,
            message = $"Đã gửi thành công tin nhắn tới {danhSachNhan.Count} bệnh nhân!",
            pushOk,
            pushFail
        });
    }

    [HttpGet]
    public async Task<IActionResult> LayThongBao()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
            return Json(new { success = false, soMoi = 0, danhSach = Array.Empty<object>() });

        // Tên khóa JSON giữ nguyên (NguoiGui là số điện thoại) — JS đang đọc theo đó.
        var danhSach = await (
            from tb in _db.ThongBaos.AsNoTracking()
            join nhan in _db.TaiKhoans.AsNoTracking() on tb.IdNguoiNhan equals nhan.Id
            join gui in _db.TaiKhoans.AsNoTracking() on tb.IdNguoiGui equals gui.Id
            where nhan.SDT == sdt
            orderby tb.ThoiGian descending
            select new
            {
                tb.Id,
                tb.NoiDung,
                NguoiGui = gui.SDT,
                tb.DaDoc,
                ThoiGian = tb.ThoiGian.ToString("HH:mm dd/MM/yyyy")
            }).Take(20).ToListAsync();

        var soMoi = danhSach.Count(t => !t.DaDoc);
        return Json(new { success = true, soMoi, danhSach });
    }

    [HttpPost]
    public async Task<IActionResult> DanhDauDaDoc()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
            return Json(new { success = false });

        var idNhan = await LayIdHoSoAsync(sdt);
        if (idNhan is null) return Json(new { success = false });

        await _thuTuc.DanhDauThongBaoDaDocAsync(idNhan.Value);
        return Json(new { success = true });
    }

    // -------------------------------------------------------------------------
    // Gửi tin nhắn trả lời (Patient -> Admin/DoiTac, hoặc ngược lại)
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> TraLoiTinNhan([FromBody] ReplyRequest model)
    {
        var nguoiGui = User.Identity?.Name;
        if (string.IsNullOrEmpty(nguoiGui))
            return Json(new { success = false, message = "Vui lòng đăng nhập lại." });

        if (string.IsNullOrWhiteSpace(model.Message) || string.IsNullOrWhiteSpace(model.NguoiNhan))
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

        var now = DateTime.Now;
        var noiDungTraLoi = model.Message.Trim();

        var idGui = await LayIdTaiKhoanAdminAsync(nguoiGui);    // Admin tra loi
        var idNhanTraLoi = await LayIdHoSoAsync(model.NguoiNhan); // benh nhan nhan
        if (idGui is null || idNhanTraLoi is null)
            return Json(new { success = false, message = "Không tìm thấy tài khoản." });

        var luu = await _thuTuc.SaveThongBaoAsync(idGui.Value, idNhanTraLoi.Value, noiDungTraLoi);
        if (!luu.KetQua.Succeeded)
            return Json(new { success = false, message = luu.KetQua.Message });

        var vapidPublicKey = _config["Vapid:PublicKey"] ?? "";
        var vapidPrivateKey = _config["Vapid:PrivateKey"] ?? "";
        var vapidSubject = _config["Vapid:Subject"] ?? "mailto:admin@hissoft.vn";

        var webPushClient = new WebPushClient();
        if (!string.IsNullOrEmpty(vapidPublicKey) && !string.IsNullOrEmpty(vapidPrivateKey))
        {
            webPushClient.SetVapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
        }

        var danhSachSubscription = await _db.PushDangKys.AsNoTracking()
            .Where(p => p.IdBenhNhan == idNhanTraLoi.Value)
            .ToListAsync();

        foreach (var sub in danhSachSubscription)
        {
            try
            {
                var subscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title = "ðŸ’¬ Phản hồi từ " + nguoiGui,
                    body = model.Message.Trim(),
                    icon = "/static/icon-192.png",
                    badge = "/static/icon-192.png",
                    sender = nguoiGui,
                    url = "/"
                });
                await webPushClient.SendNotificationAsync(subscription, payload);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi gửi push cho {SDT}: {Message}", model.NguoiNhan, ex.Message);
            }
        }

        return Json(new {
            success = true,
            message = "Đã gửi phản hồi thành công.",
            data = new {
                id = luu.Id,
                noiDung = noiDungTraLoi,
                nguoiGui = nguoiGui,
                nguoiNhan = model.NguoiNhan,
                thoiGian = now.ToString("HH:mm dd/MM/yyyy")
            }
        });
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> GetChatHistory(string sdtBenhNhan)
    {
        var adminId = User.Identity?.Name;
        if (string.IsNullOrEmpty(adminId) || string.IsNullOrEmpty(sdtBenhNhan))
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

        var idAdmin = await LayIdTaiKhoanAdminAsync(adminId);
        var idBenhNhan = await LayIdHoSoAsync(sdtBenhNhan);
        if (idAdmin is null || idBenhNhan is null)
            return Json(new { success = true, data = Array.Empty<object>() });

        var messages = await _db.ThongBaos.AsNoTracking()
            .Where(t => (t.IdNguoiGui == idAdmin.Value && t.IdNguoiNhan == idBenhNhan.Value) ||
                        (t.IdNguoiGui == idBenhNhan.Value && t.IdNguoiNhan == idAdmin.Value))
            .OrderBy(t => t.ThoiGian)
            .Select(t => new
            {
                id = t.Id,
                noiDung = t.NoiDung,
                thoiGian = t.ThoiGian.ToString("HH:mm dd/MM/yyyy"),
                isSender = t.IdNguoiGui == idAdmin.Value,
                daDoc = t.DaDoc
            })
            .ToListAsync();

        return Json(new { success = true, data = messages });
    }

    [HttpGet]
    public async Task<IActionResult> LayLichSuTinNhan([FromQuery] string doiTac)
    {
        var me = User.Identity?.Name;
        if (string.IsNullOrEmpty(me) || string.IsNullOrEmpty(doiTac))
            return Json(new { success = false });

        // Màn *Lịch sử tin nhắn* của bệnh nhân: mình là HỒ SƠ, bên kia là ADMIN.
        var idToi = await LayIdHoSoAsync(me);
        var idDoiTac = await LayIdTaiKhoanAdminAsync(doiTac);
        if (idToi is null || idDoiTac is null)
            return Json(new { success = true, messages = Array.Empty<object>() });

        var messages = await _db.ThongBaos.AsNoTracking()
            .Where(t => (t.IdNguoiGui == idToi.Value && t.IdNguoiNhan == idDoiTac.Value) ||
                        (t.IdNguoiGui == idDoiTac.Value && t.IdNguoiNhan == idToi.Value))
            .OrderBy(t => t.ThoiGian)
            .Select(t => new {
                t.Id,
                t.NoiDung,
                NguoiGui = t.IdNguoiGui == idToi.Value ? me : doiTac,
                NguoiNhan = t.IdNguoiNhan == idToi.Value ? me : doiTac,
                t.DaDoc,
                ThoiGian = t.ThoiGian.ToString("HH:mm dd/MM/yyyy")
            })
            .ToListAsync();

        return Json(new { success = true, messages });
    }
}

// â”€â”€ Request models â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public class SendSmsRequest
{
    public string Message { get; set; } = string.Empty;
    public List<string> DanhSachNguoiNhan { get; set; } = new();
}

public class PushSubscriptionRequest
{
    public string? Endpoint { get; set; }
    public string? P256dh { get; set; }
    public string? Auth { get; set; }
    public string? DeviceId { get; set; }
}

public class LocBNRequest
{
    /// <summary>
    /// Tên công ty — cột phẳng <c>DM_CSKCB.TenCongTy</c> từ đợt 1B (C16/PA-1).
    /// 🔴 Không còn trường Password: phép "xác thực đối tác" cũ là do trang tự
    /// điền rồi tự so lại chính nó. Cửa thật là [Authorize(Roles = "Admin")].
    /// </summary>
    public string TenCongTy { get; set; } = string.Empty;
}

public class ReplyRequest
{
    public string NguoiNhan { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}


