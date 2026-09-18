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

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext db,
        IConfiguration config,
        AdminStoredProcedureService thuTuc,
        ILuongCongBenhNhan luong,
        IHoSoBenhNhanService hoSo,
        Services.His.IHisDocService his)
    {
        _logger = logger;
        _db = db;
        _config = config;
        _thuTuc = thuTuc;
        _luong = luong;
        _hoSo = hoSo;
        _his = his;
    }

    /// <summary>
    /// Thong bao / thiet bi / push nay khoa theo IDTaiKhoan chu khong con theo chuoi
    /// so dien thoai. Cac API JSON van GIU nguyen ten khoa cu (nguoiGui/nguoiNhan la
    /// so dien thoai) vi JS phia trinh duyet dang doc theo do.
    /// </summary>
    private Task<long?> LayIdTaiKhoanAsync(string sdt) =>
        _db.TaiKhoans.AsNoTracking()
            .Where(t => t.SDT == sdt)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();


    /// <summary>
    /// Ho so cua nguoi dang dang nhap TAI CO SO cua phien. Mot cho duy nhat —
    /// truoc day cau nay chep o hai man va de lech nhau.
    ///
    /// <para>
    /// 🔴 <c>orderby h.Id</c> la bat buoc (V6a). Script 05 da bo rang buoc
    /// <c>UK_DM_BenhNhanCoSo_HoSo</c> vi no trai du lieu that (17,3% benh nhan
    /// Thien Nam co >=2 MaBN tai CUNG mot co so). Truoc day CSDL bao dam moi
    /// (nguoi, co so) mot ho so; nay chi con mot phep kiem o tang ung dung
    /// (<see cref="Services.Partner.LuongCongBenhNhan"/>). Ngay nao co dong thu
    /// hai ma khong sap thu tu thi SQL Server tra dong nao la tuy ke hoach truy
    /// van — man se doi NGUOI giua hai lan tai ma khong bao gi.
    /// </para>
    /// <para>
    /// Con phai chon dung MOT ho so cho toi khi co *ho so dang chon* (chot 2 dot
    /// 1, ADR 0019) — do la V6b, di cung man Noi ho so. Tam thoi lay ho so cu
    /// nhat va GHI CANH BAO khi co nhieu hon mot, de ngay do minh biet chu khong
    /// phai doan.
    /// </para>
    /// <para>
    /// Loc <c>DaMoTaiLieu</c> ngay tai day: *Cua tai lieu* (chot 9 dot 1,
    /// ADR 0020) la dieu kien de mo ket qua can lam sang / don thuoc.
    /// </para>
    /// </summary>
    private async Task<HoSoDangDung?> LayHoSoDangDungAsync(string dinhDanh, string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(dinhDanh) || string.IsNullOrWhiteSpace(maCoSo))
        {
            ViewBag.SoHoSo = 0;
            return null;
        }

        // Mot tai khoan quan NHIEU con nguoi (ADR 0019), nen phai bat dau tu tai
        // khoan chu khong tu so dien thoai cua tung con nguoi: ho so cua me do
        // con tao mang so cua me, loc theo so dien thoai la no bien mat.
        var idTaiKhoan = await _hoSo.LayIdTaiKhoanAsync(dinhDanh);

        // 🔴 KHONG khop bang CCCD (V5). CCCD go luc dang nhap khong duoc xac
        // thuc — OTP chi xac thuc so dien thoai (A6 dot 1). Khop bang CCCD
        // nghia la go CCCD nguoi khac la xem duoc tai lieu cua ho.
        //
        // Nhanh thu hai (p.SDT == dinhDanh) la duong LUI cho du lieu chua kip do
        // sang cot IdTaiKhoan. Script 09 do het mot lan, nhung phien dang song
        // van phai chay dung.
        var danhSach = await (
            from p in _db.BenhNhans.AsNoTracking()
            join h in _db.BenhNhanCoSos.AsNoTracking() on p.Id equals h.IdBenhNhan
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals cs.Id
            where ((idTaiKhoan != null && p.IdTaiKhoan == idTaiKhoan)
                   || (p.IdTaiKhoan == null && (p.SDT == dinhDanh || p.Email == dinhDanh)))
                  && cs.MaCoSo == maCoSo
                  && h.DaMoTaiLieu
            orderby h.Id
            select new HoSoDangDung(p, h)).ToListAsync();

        ViewBag.SoHoSo = danhSach.Count;

        if (danhSach.Count == 0) return null;

        // *Ho so dang chon* (ADR 0019): claim quyet dinh dang xem ho so nao.
        // Claim CHI duoc phat sau khi da kiem ho so thuoc tai khoan
        // (HoSoController.Chon), nen o day tin duoc — nhung van loc lai trong
        // danh sach da tra ve chu khong tra cuu thang theo claim.
        var idDangChon = User.FindFirst(LuongCongBenhNhan.ClaimHoSoDangChon)?.Value;

        if (long.TryParse(idDangChon, out var idChon))
        {
            var khop = danhSach.FirstOrDefault(x => x.BenhNhan.Id == idChon);
            if (khop is not null) return khop;

            // Ho so dang chon khong co mat o co so nay — chuyen co so hoac vua bi
            // xoa. Roi ve ho so dau tien, dung de man trang.
            _logger.LogInformation(
                "Ho so dang chon {IdChon} khong co o co so {MaCoSo}, dung ho so {IdThayThe}",
                idChon, maCoSo, danhSach[0].BenhNhan.Id);
        }
        else if (danhSach.Count > 1)
        {
            // Phien cu chua mang claim. Van tat dinh nho orderby, nhung nguoi
            // dung chua chon duoc — ho vao *Ho so cua toi* mot lan la xong.
            _logger.LogInformation(
                "Tai khoan co {SoHoSo} ho so tai co so {MaCoSo} nhung phien chua co claim ho so dang chon.",
                danhSach.Count, maCoSo);
        }

        return danhSach[0];
    }

    // Action Index (trang benh nhan cu) da duoc go bo ngay 2026-08-22 theo yeu cau
    // cua user: luong do khong dung nua, thay bang /benh-nhan. Lay lai neu can:
    //   git show 224341a -- SixosPwa/Views/Home/Index.cshtml

    // Tham so van ten phongKhamId de khong pha URL dang chay; thuc chat no la
    // ID CO SO — bang PhongKham da bi xoa o dot tai kien truc (W-05).
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
        // Danh mục tương ứng
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
            .Where(x => x.IdNhomCS == idNhom && x.Active)
            .OrderByDescending(x => x.QuangCao)
            .ToListAsync();

        return View(dsCoso);
    }

    /// <summary>
    /// URL co dinh cua tung co so. Slug do quan tri vien dat tay (DMCSKCB.Slug),
    /// KHONG sinh tu ten, nen doi ten co so khong lam gay URL da phat cho doi tac.
    /// </summary>
    [HttpGet("/DangKyOnline/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> ChiTietCoSo(string slug, string? canhBao = null)
    {
        var coSo = await _db.DMCSKCBs.FirstOrDefaultAsync(x => x.Slug == slug);

        if (coSo == null) return NotFound();
        if (!coSo.Active) return await DayDiKhiCoSoAnAsync(coSo);

        await DoDuLieuCoSoAsync(coSo);
        await DoDoiChieuPhienCoSoAsync(coSo.MaCoSo, canhBao == "1");
        return View();
    }

    /// <summary>
    /// URL cu khop co so bang cach bo dau ten. Giu lai va chuyen huong 301 sang
    /// /DangKyOnline/{slug} de moi duong link da phat di khong chet.
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
                // Chan o day chu khong doi nhanh 301 phia duoi lo ho: co so CHUA co
                // slug se render thang o :142-144, khong di qua /DangKyOnline/{slug}.
                if (!matchedCS.Active) return await DayDiKhiCoSoAnAsync(matchedCS);

                // Nam sua 2026-08-24: tra lai 301 sang /DangKyOnline/{slug}. Render thang o day
                // thi ViewData thieu Slug/MaCoSo, keo theo hai nut ben trang co so mat
                // tham so ?coSo= va luong ban giao sang doi tac chet. Xem ADR 0003.
                if (!string.IsNullOrWhiteSpace(matchedCS.Slug))
                {
                    return RedirectPermanent($"/DangKyOnline/{matchedCS.Slug}");
                }

                // Co so chua duoc dat slug: van hien duoc trang, chi la khong co
                // URL co dinh. Quan tri vien dat slug trong man Admin/CoSoYTe.
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
    /// Trang chu cua benh nhan tai co so KHONG co API rieng. Dot 2026-08 moi chi
    /// dung giao dien: ba the dich vu deu dan toi man "Dang cap nhat".
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

        // Phai loc theo CA dinh danh LAN co so: mot so dien thoai co the co ho so
        // o nhieu co so khac nhau (du lieu that dang co truong hop do), khong loc
        // thi trang chao ten lay tu ho so cua co so KHAC.
        var hoSoInfo = await LayHoSoDangDungAsync(dinhDanh, maCoSo);

        var benhNhan = hoSoInfo?.BenhNhan;
        var hoSoCoSo = hoSoInfo?.HoSo;

        int soLuongDonThuoc = 0;
        int soLuongKetQuaKham = 0;

        if (coSo != null && hoSoCoSo != null)
        {
            // Chi dem ban MOI NHAT: ket qua bi sua/ky lai giu lai ban cu lam doi
            // chung nhung khong duoc dem hai lan. Bo nhanh so theo MaBN — tu dot 2
            // IdBenhNhanCoSo da NOT NULL nen no chi la duong vong.
            var queryTl = _db.TaiLieuBenhNhans.AsNoTracking()
                .Where(t => t.IdCoSo == coSo.Id && t.IdBenhNhanCoSo == hoSoCoSo.Id && t.LaBanMoiNhat);

            // V9 — dem theo THANH VIEN TAP, khong phai "khac DON_THUOC". Ma la
            // (du lieu cu, hoac HIS go sai truoc khi cua API siet) khong duoc
            // lang le nhay vao nhom Ket qua kham nua.
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

        // 🔴 Khoi *Ma ho so* cua giao dien moi (nhanh bk) doc ViewBag.MaBN
        ViewBag.MaBN = hoSoCoSo?.MaBN;

        // Logo + duong ra khoi trang benh nhan. Truoc day man nay khong co loi nao
        // quay lai phan cong khai, ma tu 2026-08-27 "/" lai day nguoc ve day, nen
        // thieu no la benh nhan bi nhot. Tro toi DANH SACH co so chu khong tro "/":
        // tro "/" la thanh nut chet vi "/" se day ve lai day.
        //
        // UnescapeDataString bam theo ChiTietCoSo.cshtml:7 — URL trong cot Logo co
        // the da bi ma hoa mot lan truoc khi luu.
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
        // 🔴 HAI o nay thuoc ve *Ho so dang chon*, khong phai chu tai khoan — dung
        // mot khuon voi ViewBag.TenBenhNhan va ViewBag.MaBN ngay tren. Truoc day
        // chung doc claim, nen tai khoan mo ho so nguoi than thi the hien TEN nguoi
        // than nhung CCCD/dien thoai cua NGUOI CAM DIEN THOAI. Claim chi con la
        // duong lui cho ho so chua khai du (xem CONTEXT.md muc *Ho so dang chon*).
        ViewBag.DienThoai = benhNhan?.SDT ?? User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
        ViewBag.CccdCheBot = CheBotCccd(benhNhan?.CCCD ?? cccd);
        ViewBag.CoLoiKetNoi = loi == "khong-ket-noi-duoc";

        // Co so dung bo man cua doi tac: mat khau la CUA HO, benh nhan doi tren
        // trang cua co so. An muc "Doi mat khau" di cho khoi dan toi ngo cut. ADR 0014.
        var cuaCoSo = await _luong.LayCuaAsync(maCoSo);
        ViewBag.DungManDoiTac = cuaCoSo?.DungManDoiTac == true;

        // ── CAI VAN cua o *Lich kham cua toi* (ADR 0025) ───────────────────
        // 🔴 Co so CHUA NOI thi o BIEN MAT khoi trang, khong hien roi bao loi:
        // o co so chua noi thi 100% so lan bam la bam vao thu khong dung duoc.
        // Da co tien le ngay trong glossary — o *Dang ky kham theo goi* AN toi
        // giai doan 3. Phep kiem nay chi doc CSDL cong, KHONG goi sang HIS:
        // hoi HIS la viec cua luc nguoi dung BAM mo o.
        ViewBag.HienOLichKham = coSo != null && await _his.CoNoiHisAsync(coSo.Id);

        // ── Dai nhac hoan thien ho so (ADR 0024, chot 12) ──────────────────
        // 🔴 NHAC MEM, KHONG CHAN. Chan cung theo "thieu truong" se khoa luon
        // nhung ho so DA NOI MA ma con khuyet du lieu — dung trang thai cua tai
        // khoan nghiem thu 0363982926 (co MaBN 100992, thieu ngay sinh): mot man
        // dang chay va dang phuc vu tai lieu that bong thanh man chan.
        // Cho moc neu sau nay doi y: Services/Partner/LuongCongBenhNhan.cs — cong
        // ha canh duy nhat sau xac thuc.
        ViewBag.HoSoThieuTruong = benhNhan != null
            && (LaTenTam(benhNhan.TenBN, dinhDanh)
                || benhNhan.NgaySinh is null
                || string.IsNullOrWhiteSpace(benhNhan.GioiTinh));

        ViewBag.IdHoSoDangXem = benhNhan?.Id;

        return View();
    }

    /// <summary>
    /// Ten ho so mang SO DIEN THOAI chu khong phai ten nguoi — 14/19 tai khoan do
    /// tren cong ngay 2026-09-09. Chung tu de ra luc dang ky bang OTP, khong bao
    /// gio di qua man *Them ho so*.
    /// </summary>
    private static bool LaTenTam(string? ten, string dinhDanh)
    {
        if (string.IsNullOrWhiteSpace(ten)) return true;

        var t = ten.Trim();

        if (string.Equals(t, dinhDanh?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

        // Toan chu so (co the co dau + o dau) => la so dien thoai, khong phai ten.
        return t.TrimStart('+').All(char.IsDigit);
    }

    /// <summary>
    /// Trang danh sách tài liệu y tế của bệnh nhân (Đơn thuốc, Kết quả xét nghiệm, CĐHA, ...).
    /// </summary>
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

            var q = _db.TaiLieuBenhNhans.AsNoTracking()
                .Where(t => t.IdCoSo == coSo.Id && t.IdBenhNhanCoSo == idBnCoSo && t.LaBanMoiNhat);

            // Lọc chính xác theo nhóm tài liệu
            if (nhomChuan == LoaiTaiLieu.NhomDonThuoc)
            {
                q = q.Where(t => t.LoaiTaiLieu == LoaiTaiLieu.DonThuoc);
            }
            else if (nhomChuan == LoaiTaiLieu.NhomKetQuaKham)
            {
                q = q.Where(t => LoaiTaiLieu.MaCuaNhomKetQuaKham.Contains(t.LoaiTaiLieu));
            }

            danhSach = await q
                .OrderByDescending(t => t.NgayKham ?? t.NgayTao)
                .ThenByDescending(t => t.Id)
                .ToListAsync();
        }

        return View(danhSach);
    }

    /// <summary>
    /// Gio lam viec nay nam o bang con DM_CSKCB_GioLamViec (mot dong moi thu), khong
    /// con la cap cot TGLamViec/NgayLamViec voi noi dung nguoc ten cot nhu truoc.
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
    /// DM_CSKCB_GioLamViec luu Thu theo quy uoc 0 = Chu nhat, 1..6 = Thu 2..Thu 7.
    /// Chuyen danh sach so trong DB thanh nhan de giao dien, khong hien "1,2,3...".
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

    /// <summary>Màn trống cho các thẻ chưa nối dữ liệu.</summary>
    [HttpGet("/benh-nhan/sap-co")]
    public async Task<IActionResult> SapCo(string? muc = null)
    {
        // Ke hoach dieu tri chi danh cho co so nha khoa. Neu co so khac co tinh vao truc tiep thi chuyen ve /benh-nhan.
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

    /// <summary>Che bot so CCCD khi hien tren man: 0772•••••069.</summary>
    private static string CheBotCccd(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return "—";
        if (cccd.Length <= 7) return cccd;

        return $"{cccd[..4]}{new string('\u2022', cccd.Length - 7)}{cccd[^3..]}";
    }

    private const string AnhCoSoMacDinh = "https://images.unsplash.com/photo-1519494026892-80bbd2d6fd0d?w=800&q=80";
    private const string LogoCoSoMacDinh = "https://tse1.mm.bing.net/th/id/OIP.JgUNpJPll-8BkzE3XN6LggHaHa?r=0&pid=Api&P=0&h=180";

    /// <summary>
    /// Do du lieu mot co so ra ViewData cho trang chi tiet. MaCoSo va Slug la hai
    /// thu hai nut "Dang ky kham" / "Dang nhap" phai mang theo — thieu chung thi
    /// man dang nhap khong biet benh nhan dang o co so nao.
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
        ViewData["Img"] = coSo.Img ?? AnhCoSoMacDinh;
        ViewData["Logo"] = coSo.Logo ?? LogoCoSoMacDinh;
        ViewData["TGLamViec"] = await GetOperatingHoursValueAsync(coSo);
        ViewData["NoiDungCskcb"] = await LoadNoiDungAsync(coSo);
    }

    /// <summary>
    /// So MaCoSo cua phien voi co so dang xem, de trang tu quyet dinh co bat modal
    /// chan dang nhap cheo co so hay khong. Chi so — KHONG tu doi claim, viec doi
    /// (neu benh nhan dong y) di qua DangXuat roi Login binh thuong.
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
    /// Co so dang AN (DM_CSKCB.Active = 0) thi day khach ve danh sach cong khai cua
    /// nhom no — 302 tran, khong bang thong bao. Khong suy duoc nhom (IdNhomCS rong,
    /// vi du co so ID=11) thi ve trang chu. Xem ADR 0013.
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
        // "/" la start_url cua PWA, nen day chinh la cho benh nhan dap xuong moi
        // lan MO NGUOI app tu bieu tuong. Da dang nhap thi khong con ly do xem
        // trang quang ba cong khai — di thang trang benh nhan.
        //
        // 🔴 CHI day khi MO NGUOI (khoi dong lai app / vao bang bookmark / go thang
        // URL) — luc do khong co Referer cung host. Bam trong app toi "/" (vi du nut
        // logo o header trang danh sach co so tro ve day) thi CO Referer cung host,
        // phai render "/" binh thuong chu KHONG bat nguoc ve /benh-nhan. Day dung
        // phep thu ma "PWA Last Page Restore" trong _Layout.cshtml dung.
        //
        // 🔴 Ve "co claim Cccd" la BAT BUOC. Khu Admin ky CA HAI cookie (xem
        // _Layout.cshtml), nen admin cung tinh la IsAuthenticated — nhung ho khong
        // co claim Cccd/MaCoSo, day ho sang /benh-nhan la ra trang rong khong biet
        // chao ai. Xem ADR 0016.
        var referer = Request.Headers["Referer"].ToString();
        var tuTrongApp = !string.IsNullOrEmpty(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var refUri)
            && string.Equals(refUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase);

        if (!tuTrongApp
            && User.Identity?.IsAuthenticated == true
            && !string.IsNullOrWhiteSpace(User.FindFirst(LuongCongBenhNhan.ClaimCccd)?.Value))
        {
            // 🔴 Voi co so dung BO MAN CUA DOI TAC (Ung Buou), /benh-nhan la SAI dich.
            // Benh nhan do song o TrangChu ben doi tac; trang benh nhan noi bo chi la
            // mot tram dung khong ai muon. Phien da mang dau an thi mo nguoi phai di
            // THANG sang ho, dung nhu luc bam nut trong app.
            //
            // Uy thac cho /DangNhap/DiTiep chu KHONG chep lai cay quyet dinh: no da
            // giu du ba ve (co CCCD - co dau an - dung co so cua phien) va goi
            // DangNhapLaiBangMatKhauDaCatAsync de HOI DOI TAC truoc khi ban giao
            // (ADR 0016 muc 2). Moi nhanh thoat cua no deu la trang cuoi — /benh-nhan,
            // /DangNhap/Login?coSo=, /DangNhap/BanGiao?coSo= — nen khong the vong lai "/".
            //
            // Doi tac chet thi DiTiep roi xuong ChonDichDenAsync => man dang nhap cua
            // co so kem cau bao su co, KHONG phai /benh-nhan. Do la danh doi da biet
            // cua ADR 0016 muc 2, khong phai lo thung moi.
            //
            // SUA ADR 0016 muc 5 ngay 2026-08-27: truoc do MOI phien mo nguoi deu ve
            // /benh-nhan, ke ca phien cua co so doi tac.
            var maCoSoPhien = User.FindFirst(LuongCongBenhNhan.ClaimMaCoSo)?.Value;
            if (!string.IsNullOrWhiteSpace(maCoSoPhien)
                && User.FindFirst(LuongCongBenhNhan.ClaimDoiTacXacThuc) is not null)
            {
                var cuaPhien = await _luong.LayCuaAsync(maCoSoPhien);
                if (cuaPhien?.DungManDoiTac == true)
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
                // Thu tuc cu Top_CSKCB_QC da bi V002 xoa. Ban moi sua loi nhan dong
                // cua no (W-03) va nhan @SoLuong thay vi khoa cung TOP 5.
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

        // Ho so con nguoi. KHONG tu tao o day: day la mot GET, ma khoi tu tao cu
        // chinh la nguon cua nhung dong rac MaDT = 'DT001' (phat hien C-02).
        var benhNhan = await _db.BenhNhans.FirstOrDefaultAsync(b => b.SDT == sdt);

        if (benhNhan is null)
        {
            // Tai khoan vua dang ky OTP nhung chua co ho so — danh sach rong la
            // KET QUA DUNG, dung thay bang du lieu mau.
            ViewData["MaBN"] = "";
            ViewData["TenBN"] = "";
            ViewData["DiaChi"] = "";
            ViewData["Email"] = "";
            return View(new List<DotKham>());
        }

        ViewData["MaBN"] = await _db.BenhNhanCoSos
            .Where(h => h.IdBenhNhan == benhNhan.Id)
            .OrderBy(h => h.Id)
            .Select(h => h.MaBN)
            .FirstOrDefaultAsync() ?? "";
        ViewData["TenBN"] = benhNhan.TenBN;
        ViewData["DiaChi"] = benhNhan.DiaChi ?? "";
        ViewData["Email"] = benhNhan.Email ?? "";

        // Lich su kham nay treo vao HO SO TAI MOT CO SO — bang PhongKham da bi
        // xoa o dot tai kien truc (W-05).
        //
        // Gom theo CON NGUOI (IdBenhNhan) chu khong theo mot ho so: mot nguoi tai
        // MOT co so van co the co nhieu ho so — 17,3% benh nhan Thien Nam co >=2
        // MaBN. Loc theo mot ho so se giau mat lich su cua chinh ho.
        var dotKham = await (
            from dk in _db.DotKhams
            join h in _db.BenhNhanCoSos on dk.IdBenhNhanCoSo equals h.Id
            where h.IdBenhNhan == benhNhan.Id
            orderby dk.NgayGioVao descending
            select dk).ToListAsync();

        return View(dotKham);
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public IActionResult GuiTinNhan()
    {
        var doiTacs = _db.DoiTacs.ToList();
        return View(doiTacs);
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public IActionResult LocDanhSachBN([FromBody] LocBNRequest model)
    {
        if (string.IsNullOrWhiteSpace(model.TenDT) || string.IsNullOrWhiteSpace(model.Password))
            return Json(new { success = false, message = "Vui lòng nhập đủ thông tin đối tác và mật khẩu." });

        try
        {
            // Thu tuc cu LocDanhSachBN vua xac thuc doi tac vua loc benh nhan theo
            // chuoi MaDT. Cot MaDT do lan HAI he ma (ma co so + ma doi tac) cong rac
            // nen da bi bo (C-01). Nay tach doi: xac thuc o day, loc o DM_BenhNhan_Loc.
            var doiTac = _db.DoiTacs.AsNoTracking()
                .FirstOrDefault(x => x.TenDT == model.TenDT);

            if (doiTac is null || !string.Equals(doiTac.MatKhauDoiTac, model.Password, StringComparison.Ordinal))
                return Json(new { success = false, message = "Tên đối tác hoặc mật khẩu không đúng." });

            // Loc theo DOI TAC qua cot DM_CSKCB.IDDoiTac (V010 + ADR 0011).
            // Truoc V010 hai bang khong co duong noi nao nen cho nay buoc phai
            // tra MOI co so — bo loc chet am tham. Nay di dung duong:
            // doi tac -> cac co so cua doi tac -> ho so benh nhan.
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandText = "DM_BenhNhan_Loc";

            foreach (var (ten, giaTri) in new (string, object?)[]
                     { ("@TuKhoa", null), ("@IDCoSo", null), ("@IDDoiTac", doiTac.Id),
                       ("@Trang", 1), ("@CoTrang", 1000) })
            {
                var p = cmd.CreateParameter();
                p.ParameterName = ten;
                p.Value = giaTri ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            using var reader = cmd.ExecuteReader();

            // Khoa JSON doi ten maDT -> maCoSo. Day la NGOAI LE co chu y cua luat
            // "giu nguyen ten khoa": gia tri o day la MA CO SO (ho so benh nhan
            // treo vao co so), nen khoa cu dang NOI DOI ve nghia. gui-tin-nhan.js
            // da sua theo.
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

    // -------------------------------------------------------------------------
    // Trả về VAPID Public Key để client đăng ký push subscription
    // -------------------------------------------------------------------------
    [HttpGet]
    [AllowAnonymous]
    public IActionResult VapidPublicKey()
    {
        var publicKey = _config["Vapid:PublicKey"] ?? "";
        return Json(new { publicKey });
    }

    // -------------------------------------------------------------------------
    // Lấy danh sách tài khoản bệnh nhân thật từ DB (cho GuiTinNhan dùng)
    // -------------------------------------------------------------------------
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

    // -------------------------------------------------------------------------
    // Nhận và lưu push subscription của thiết bị vào DB
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> DangKyPush([FromBody] PushSubscriptionRequest model)
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt) || string.IsNullOrEmpty(model.Endpoint))
            return Json(new { success = false });

        var idTaiKhoan = await LayIdTaiKhoanAsync(sdt);
        if (idTaiKhoan is null)
            return Json(new { success = false });

        // Thu tuc tu lo phan "da co endpoint nay chua" (ADR 0008).
        var ketQua = await _thuTuc.SavePushDangKyAsync(
            idTaiKhoan.Value,
            model.Endpoint,
            model.P256dh ?? "",
            model.Auth ?? "",
            model.DeviceId);

        if (!ketQua.KetQua.Succeeded)
            return Json(new { success = false, message = ketQua.KetQua.Message });
        _logger.LogInformation("Đăng ký push thành công cho {SDT}", sdt);
        return Json(new { success = true });
    }

    // -------------------------------------------------------------------------
    // Gửi tin nhắn hàng loạt – lưu DB + gửi Web Push tới từng thiết bị
    // -------------------------------------------------------------------------
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

        var idNguoiGui = await LayIdTaiKhoanAsync(nguoiGui);
        if (idNguoiGui is null)
            return Json(new { success = false, message = "Không tìm thấy tài khoản người gửi!" });

        // 1) Luu ThongBao — moi dong mot lan goi thu tuc (ADR 0008).
        var idTheoSdt = await _db.TaiKhoans.AsNoTracking()
            .Where(t => danhSachNhan.Contains(t.SDT))
            .ToDictionaryAsync(t => t.SDT, t => t.Id);

        foreach (var sdtNhan in danhSachNhan)
        {
            if (idTheoSdt.TryGetValue(sdtNhan, out var idNhan))
                await _thuTuc.SaveThongBaoAsync(idNguoiGui.Value, idNhan, smsMessage);
        }

        // 2) Gửi Web Push tới tất cả thiết bị đã đăng ký của từng bệnh nhân
        var vapidPublicKey = _config["Vapid:PublicKey"] ?? "";
        var vapidPrivateKey = _config["Vapid:PrivateKey"] ?? "";
        var vapidSubject = _config["Vapid:Subject"] ?? "mailto:admin@hissoft.vn";

        var webPushClient = new WebPushClient();
        webPushClient.SetVapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);

        var danhSachSubscription = await (
            from p in _db.PushDangKys.AsNoTracking()
            join t in _db.TaiKhoans.AsNoTracking() on p.IdTaiKhoan equals t.Id
            where danhSachNhan.Contains(t.SDT)
            select new { Sub = p, t.SDT }).ToListAsync();

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
                // Subscription het han — don ngay qua thu tuc (ADR 0008).
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

    // -------------------------------------------------------------------------
    // Lấy danh sách thông báo chưa đọc của tài khoản hiện tại
    // -------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> LayThongBao()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
            return Json(new { success = false, soMoi = 0, danhSach = Array.Empty<object>() });

        // Ten khoa JSON giu nguyen (NguoiGui la so dien thoai) — JS dang doc theo do.
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

    // -------------------------------------------------------------------------
    // Đánh dấu tất cả thông báo của user là đã đọc
    // -------------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> DanhDauDaDoc()
    {
        var sdt = User.Identity?.Name;
        if (string.IsNullOrEmpty(sdt))
            return Json(new { success = false });

        var idNhan = await LayIdTaiKhoanAsync(sdt);
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

        var idGui = await LayIdTaiKhoanAsync(nguoiGui);
        var idNhanTraLoi = await LayIdTaiKhoanAsync(model.NguoiNhan);
        if (idGui is null || idNhanTraLoi is null)
            return Json(new { success = false, message = "Không tìm thấy tài khoản." });

        var luu = await _thuTuc.SaveThongBaoAsync(idGui.Value, idNhanTraLoi.Value, noiDungTraLoi);
        if (!luu.KetQua.Succeeded)
            return Json(new { success = false, message = luu.KetQua.Message });

        // Gửi Push (Tái sử dụng logic gửi)
        var vapidPublicKey = _config["Vapid:PublicKey"] ?? "";
        var vapidPrivateKey = _config["Vapid:PrivateKey"] ?? "";
        var vapidSubject = _config["Vapid:Subject"] ?? "mailto:admin@hissoft.vn";

        var webPushClient = new WebPushClient();
        if (!string.IsNullOrEmpty(vapidPublicKey) && !string.IsNullOrEmpty(vapidPrivateKey))
        {
            webPushClient.SetVapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
        }

        var danhSachSubscription = await _db.PushDangKys.AsNoTracking()
            .Where(p => p.IdTaiKhoan == idNhanTraLoi.Value)
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

    // -------------------------------------------------------------------------
    // Lấy lịch sử trò chuyện (Admin <-> Bệnh nhân)
    // -------------------------------------------------------------------------
    [HttpGet]
    [Authorize(AuthenticationSchemes = AdminAuthentication.Scheme, Roles = "Admin")]
    public async Task<IActionResult> GetChatHistory(string sdtBenhNhan)
    {
        var adminId = User.Identity?.Name;
        if (string.IsNullOrEmpty(adminId) || string.IsNullOrEmpty(sdtBenhNhan))
            return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

        var idAdmin = await LayIdTaiKhoanAsync(adminId);
        var idBenhNhan = await LayIdTaiKhoanAsync(sdtBenhNhan);
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

    // -------------------------------------------------------------------------
    // Lấy toàn bộ lịch sử tin nhắn giữa người dùng hiện tại và một đối tác/bệnh nhân
    // -------------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> LayLichSuTinNhan([FromQuery] string doiTac)
    {
        var me = User.Identity?.Name;
        if (string.IsNullOrEmpty(me) || string.IsNullOrEmpty(doiTac))
            return Json(new { success = false });

        var idToi = await LayIdTaiKhoanAsync(me);
        var idDoiTac = await LayIdTaiKhoanAsync(doiTac);
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
    public string TenDT { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ReplyRequest
{
    public string NguoiNhan { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}


