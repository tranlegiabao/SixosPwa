using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Cay quyet dinh cua cong benh nhan: sau khi xac thuc thi di dau, tao ho so noi
/// bo the nao. Tach khoi controller de cac man dung chung MOT cay, khong ai tu
/// che lai.
///
/// 🔴 Dot A da go HET bo man doi tac (ADR 0014): khong con IPartnerGateway,
/// khong con HT_TaiKhoanDoiTac, khong con man Ban giao. Co so co trang rieng thi
/// ChonDichDenAsync tra thang <c>DM_CSKCB.KetNoi_UrlChuyenHuong</c> — xem
/// <see cref="CuaCoSoService"/>.
/// </summary>
public interface ILuongCongBenhNhan
{
    /// <summary>Cho ha canh sau khi OTP dung.</summary>
    Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl,
                                  DanhTinhQuet? quet = null, CancellationToken ct = default);

    /// <summary>Co so co dang hien thi cong khai khong (DM_CSKCB.HienThiCongKhai). ADR 0013.</summary>
    Task<bool> CoSoDangHienThiAsync(string? maCoSo, CancellationToken ct = default);

    /// <summary>Tao ho so noi bo + dat mat khau noi bo.</summary>
    Task<KetQuaBuoc> MoTaiKhoanAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, string matKhau, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>Doi mat khau noi bo. Co so co cua rieng thi tu choi — mat khau la cua ho.</summary>
    Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default);
}

/// <summary>Ket qua mot buoc co dich den ke tiep.</summary>
/// <param name="DoiTacHong">Chuyen tiep tu <see cref="KetQuaThaoTac.DoiTacHong"/> — xem chu thich o do.</param>
public record KetQuaBuoc(bool ThanhCong, string ThongBao, string? DichDen, bool DoiTacHong = false);

public class LuongCongBenhNhan : ILuongCongBenhNhan
{
    /// <summary>Claim giu so CCCD cua benh nhan trong phien.</summary>
    public const string ClaimCccd = "Cccd";

    /// <summary>Claim giu ma co so benh nhan dang dung trong phien.</summary>
    public const string ClaimMaCoSo = "MaCoSo";

    /// <summary>
    /// Claim giu ID cua *ho so dang chon* — con nguoi nao trong so cac ho so cua
    /// tai khoan dang duoc xem (ADR 0019). Mot tai khoan quan nhieu ho so: con
    /// dat kham cho me, me theo doi ket qua cho con.
    ///
    /// <para>
    /// 🔴 Doi ho so = PHAT LAI claim nay (khuon <c>DangKyOnlineUB</c>:
    /// <c>ThemIdXemThongTinBenhNhan</c> goi <c>AddClaimsAsync</c>). Man chi doc,
    /// khong bao gio nhan ID ho so tu tham so URL — nhan tu URL thi go so khac
    /// la xem duoc ho so nguoi ta.
    /// </para>
    /// <para>
    /// Thieu claim nay (phien cu dang song, hoac tai khoan mot ho so) thi cac man
    /// tu chon ho so dau tien — xem <c>HomeController.LayHoSoDangDungAsync</c>.
    /// </para>
    /// </summary>
    public const string ClaimHoSoDangChon = "HoSoDangChon";

    /// <summary>
    /// Dau an: phien nay do CHINH doi tac xac thuc (mat khau that hoac ma SMS cua
    /// ho), khong phai OTP cua SixosPwa. Chi CapPhienBenhNhanAsync dong dau nay.
    ///
    /// 🔴 Day la thu duy nhat phan biet mot phien THAT voi mot phien duc tu
    /// XacNhanOtp — action do goi tran duoc, ma OTP con dang ke tam mot gia tri co
    /// dinh, va ca Cccd lan MaCoSo deu lay thang tu than request. Thieu dau an nay
    /// thi ai biet CCCD cua nguoi khac cung mo duoc man Ban giao va doc duoc mat
    /// khau that cua ho. Xem ADR 0016.
    /// </summary>
    public const string ClaimDoiTacXacThuc = "DoiTacXacThuc";


    private readonly ApplicationDbContext _db;
    private readonly CuaCoSoService _cua;
    private readonly ILogger<LuongCongBenhNhan> _logger;
    private readonly AdminStoredProcedureService _thuTuc;

    public LuongCongBenhNhan(
        ApplicationDbContext db,
        CuaCoSoService cua,
        ILogger<LuongCongBenhNhan> logger,
        AdminStoredProcedureService thuTuc)
    {
        _db = db;
        _cua = cua;
        _logger = logger;
        _thuTuc = thuTuc;
    }

    /// <summary>Doi ma co so (chuoi) sang khoa chinh DM_CSKCB.</summary>
    private Task<long?> LayIdCoSoAsync(string maCoSo, CancellationToken ct) =>
        _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Co so co dang hien thi cong khai khong (DM_CSKCB.HienThiCongKhai — ten cu la
    /// <c>Active</c>). Day la cong DUY NHAT quyet dinh co so co nhan DANG NHAP /
    /// DANG KY MOI hay khong. Khong tim thay ma co so thi tra false (hong theo
    /// huong an toan). Xem ADR 0013.
    ///
    /// CANH BAO: dung nham voi <c>KetNoi_Active</c> (cong tat duong ket noi HIS) —
    /// hai cong khac nhau, cung nam tren mot bang.
    /// </summary>
    public Task<bool> CoSoDangHienThiAsync(string? maCoSo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(maCoSo)) return Task.FromResult(false);

        var ma = maCoSo.Trim();
        return _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == ma)
            .Select(x => x.HienThiCongKhai)
            .FirstOrDefaultAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Cay quyet dinh sau OTP
    // ------------------------------------------------------------------

    public async Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl,
                                               DanhTinhQuet? quet = null, CancellationToken ct = default)
    {
        // 🔴 Dot A: "co so co cua rieng khong" nay la DU LIEU
        // (DM_CSKCB.KetNoi_UrlChuyenHuong), khong con la kieu ban cai
        // (DM_DoiTacApi.KieuApi da bi bo). Co URL thi chuyen huong THANG sang
        // trang cua co so; khong co thi o lai trang benh nhan noi bo.
        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
        var cua = idCoSo is null ? null : await _cua.LayCuaAsync(idCoSo.Value);

        // 🔴 Ho so noi bo phai co TRUOC khi tra bat ky dich den nao — ke ca duong
        // chuyen huong sang trang cua co so. Duong doi tac cu cung lam dung thu tu
        // nay (GhiHoSoRoiChoBanGiaoAsync goi TaoHoSoNoiBoAsync roi moi ban giao).
        // Tra URL truoc roi moi tinh chuyen tao ho so thi benh nhan xong OTP se duoc
        // phat cookie va bay thang sang co so, ma ben nay KHONG co dong DM_BenhNhan /
        // DM_BenhNhanCoSo / HT_TaiKhoan nao: quay lai /benh-nhan la ho so trong tron,
        // con TaiLieuService thi nem ChuaCoNguoiNhanException vi khong nhan ra ho.
        await BaoDamHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, quet, ct);

        if (cua?.UrlChuyenHuong is { Length: > 0 } urlChuyenHuong)
        {
            _logger.LogInformation("Co so {MaCoSo} co cua rieng — chuyen huong sang {Url}", maCoSo, urlChuyenHuong);
            return urlChuyenHuong;
        }

        // Mot tai khoan quan nhieu ho so (ADR 0019) => phai biet dang xem AI
        // truoc khi vao trang benh nhan. Bam khuon DangKyOnlineUB: dang nhap
        // xong la ve man chon ho so (HT_DangNhap_FE.js:35 day thang toi
        // /QuanLy/QL_HoSoBenhNhan).
        //
        // Khac UB o mot cho: chi bat chon khi THAT SU co tren mot ho so. Ben
        // UB ai cung nhieu ho so nen ho luon qua man do; ben nay phan lon tai
        // khoan chi co dung mot ho so, bat ho bam them mot lan la phien vo ich
        // — mot ho so thi khong co gi de chon.
        // Nếu người bệnh quét mã trên phiếu khám HIS, ta đã tự động chọn đúng hồ sơ đó,
        // nên đưa thẳng vào trang chủ /benh-nhan thay vì bắt quay về màn danh sách hồ sơ /benh-nhan/ho-so.
        if (quet != null && (quet.LaNguonHis || !string.IsNullOrWhiteSpace(quet.MaBN)))
        {
            return (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl != "/" && returnUrl != "/Home" && !returnUrl.StartsWith("/DangNhap"))
                ? returnUrl
                : "/benh-nhan";
        }

        var soHoSo = await DemHoSoTaiCoSoAsync(maCoSo, dinhDanh, ct);
        return soHoSo > 1 ? "/benh-nhan/ho-so" : "/benh-nhan";
    }

    // ------------------------------------------------------------------
    //  Man Dang ky
    // ------------------------------------------------------------------

    public async Task<KetQuaBuoc> MoTaiKhoanAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, string matKhau, string? returnUrl = null, CancellationToken ct = default)
    {
        // Co so dang an thi khong mo tai khoan moi. Phien CU van dung binh thuong —
        // cho nay la duong tao MOI co chu dich, khong phai duong dung lai. ADR 0013.
        if (!await CoSoDangHienThiAsync(maCoSo, ct))
        {
            return new KetQuaBuoc(false, "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến.", null);
        }

        _ = await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, hoTen, ct: ct);

        // 🔴 Dot 1B: KHONG con ghi mat khau noi bo cho benh nhan — ho khong co
        // tai khoan nua, dang nhap bang OTP (ADR 0036). Cot HT_TaiKhoan.MatKhauNoiBo
        // chi con phuc vu Admin.

        // Cung luat voi ChonDichDenAsync — hai loi vao (dang nhap / dang ky) phai
        // di cung mot duong, neu khong nguoi dung thay hai hanh vi khac nhau cho
        // cung mot trang thai.
        var soHoSo = await DemHoSoTaiCoSoAsync(maCoSo, dinhDanh, ct);
        return new KetQuaBuoc(true, "Đã tạo tài khoản",
            soHoSo > 1 ? "/benh-nhan/ho-so" : "/benh-nhan");
    }

    // ------------------------------------------------------------------
    //  Man Doi mat khau
    // ------------------------------------------------------------------

    /// <summary>
    /// Bam mat khau noi bo. Dung <c>PasswordHasher&lt;TaiKhoan&gt;</c> cua ASP.NET Core
    /// (co san trong shared framework, khong phai them goi NuGet) — dung ba viec ma
    /// muc "Dieu kien de go dinh chinh" cua ADR 0009 chi dinh.
    /// </summary>
    private static readonly IPasswordHasher<TaiKhoan> BamMatKhau = new PasswordHasher<TaiKhoan>();

    /// <summary>
    /// Ghi mat khau noi bo vao <c>HT_TaiKhoan.MatKhauNoiBo</c> qua thu tuc
    /// <c>HT_TaiKhoan_Save</c> (moi duong ghi di qua stored — ADR 0008).
    ///
    /// 🔴 Thay cho <c>HT_TaiKhoanDoiTac</c> da bi xoa o dot A: mat khau khong con
    /// cat theo TUNG CO SO nua, vi khong con he doi tac nao de ban giao sang.
    ///
    /// 🔴 BAT BUOC BAM TRUOC KHI TRUYEN (ADR 0009): tham so cua thu tuc ten la
    /// <c>@MatKhauNoiBoDaBam</c> va tang T-SQL KHONG BAO GIO tu bam. Truyen chuoi
    /// tho vao day la de mat khau benh nhan tu chon nam nguyen van tren dung cai cot
    /// ma duong dang nhap Admin/DoiTac dem ra so — doi Role mot cai la chuoi do mo
    /// duoc khu quan tri. ADR 0005 (luu khong bam) chi ap cho mat khau DOI TAC, va
    /// bang do (<c>HT_TaiKhoanDoiTac</c>) da bi xoa o dot A.
    /// </summary>
    private Task GhiMatKhauNoiBoAsync(TaiKhoan taiKhoan, string matKhau) =>
        _thuTuc.SaveTaiKhoanAsync(
            taiKhoan.Id,
            taiKhoan.SDT,
            taiKhoan.Email,
            string.IsNullOrWhiteSpace(taiKhoan.Role) ? "BenhNhan" : taiKhoan.Role,
            string.IsNullOrEmpty(matKhau) ? null : BamMatKhau.HashPassword(taiKhoan, matKhau));

    public async Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default)
    {
        var cccd = LayClaim(nguoiDung, ClaimCccd);
        if (string.IsNullOrWhiteSpace(cccd))
        {
            return new KetQuaThaoTac(false, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
        }

        // 🔴 Dot 1B: benh nhan KHONG CON tai khoan, nen cung khong con mat khau
        // noi bo de doi (HT_TaiKhoan chi con Admin — ADR 0036). Ban trung gian cua
        // dot nay tra "Khong tim thay tai khoan cua ban" — DUNG ket qua nhung SAI
        // nguyen nhan, nguoi dung se di tim lai tai khoan khong ton tai.
        // Man nay dang la no cua dot A (§7): hoac bo han, hoac cho no dung that.
        return new KetQuaThaoTac(false,
            "Cổng không còn dùng mật khẩu cho bệnh nhân — bạn đăng nhập bằng mã OTP gửi tới số điện thoại.");
    }


    // ------------------------------------------------------------------
    //  Ho so noi bo
    // ------------------------------------------------------------------

    /// <summary>
    /// CCCD nay thuoc DM_BenhNhan chu khong con nam tren tai khoan — 5/18 tai khoan
    /// la Admin/DoiTac, khong co CCCD la DUNG chu khong phai du lieu thieu.
    /// </summary>
    private async Task<TaiKhoan?> TimTaiKhoanAsync(string cccd, CancellationToken ct)
    {
        // 🔴 Dot 1B: benh nhan KHONG CON tai khoan (HT_TaiKhoan chi con Admin),
        // nen khong con duong nao di tu CCCD sang tai khoan. Xem ADR 0034.
        TaiKhoan? theoChuSoHuu = null;

        // 🔴 Dot A da bo cot HT_TaiKhoan.IDBenhNhan, nen nhanh lui ve chieu CU
        // (join t.IdBenhNhan = p.ID) khong con nua. Script 09 da do het du lieu
        // sang DM_BenhNhan.IDTaiKhoan tu truoc — ADR 0019.
        return theoChuSoHuu;
    }



    /// <summary>
    /// So ho so ma tai khoan nay dang quan TAI MOT CO SO — dung de quyet dinh co
    /// phai qua man chon ho so khong.
    ///
    /// Phai khop dung dieu kien cua <c>HomeController.LayHoSoDangDungAsync</c>
    /// (loc theo tai khoan + <c>DaMoTaiLieu</c>), neu khong se co canh: dem ra 2
    /// nen day sang man chon, ma man kia chi hien 1.
    /// </summary>
    private async Task<int> DemHoSoTaiCoSoAsync(string maCoSo, string dinhDanh, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dinhDanh) || string.IsNullOrWhiteSpace(maCoSo)) return 0;

        var idTaiKhoan = await _db.TaiKhoans.AsNoTracking()
            .Where(t => t.SDT == dinhDanh || t.Email == dinhDanh)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync(ct);

        // Dot 1B: pham vi la cap (SDT x co so) — luat C2.
        return await (
            from h in _db.BenhNhans.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals (long?)cs.Id
            where (h.SDT == dinhDanh || h.Email == dinhDanh)
                  && cs.MaCoSo == maCoSo
                  && h.DaMoTaiLieu
            select h.Id).CountAsync(ct);
    }

    /// <summary>
    /// 🔴 Tu dot 1B KHONG tra <c>TaiKhoan</c> nua. Ban trung gian ket thuc bang
    /// <c>_db.TaiKhoans.FirstAsync(...)</c>, ma <c>HT_TaiKhoan_Save</c> nay no-op
    /// voi vai tro khac Admin nen <c>idTaiKhoan</c> = 0 =>
    /// <c>InvalidOperationException: Sequence contains no elements</c> ngay cuoi
    /// buoc xac nhan OTP. Tra ID HO SO vua dung.
    /// </summary>
    private async Task<long> TaoHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, string hoTen,
                                                   DanhTinhQuet? quet = null, CancellationToken ct = default)
    {
        var laEmail = dinhDanh.Contains('@');
        var sdt = laEmail ? string.Empty : dinhDanh;
        var email = laEmail ? dinhDanh : null;

        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);

        // Con nguoi truoc (khoa CCCD), roi moi den ho so tai co so.
        // Ten/ngay sinh/gioi tinh/dia chi tinh o mot cho duy nhat, de luat "chi dien o
        // trong" khong bi viet lai hai kieu o hai duong.
        var o = await TinhOCanDienAsync(cccd, dinhDanh, hoTen, quet, ct);

        var luuNguoi = await _thuTuc.SaveBenhNhanAsync(
            cccd,
            o.Ten,
            sdt,
            email,
            o.DiaChi,
            // Chu so huu chua biet o buoc nay — tai khoan duoc tao SAU. Nhan chu
            // o cuoi ham bang DM_BenhNhan_NhanChuSoHuu.
            idTaiKhoan: null,
            ngaySinh: o.Ngay,
            // O thu ba cua luat gop (ADR 0018). Chuan hoa MOT BAN DUY NHAT qua
            // ChuanHoaTen — dung tu bo dau kieu khac o cho khac.
            hoTenKhongDau: o.Slug,
            gioiTinh: o.GioiTinh);

        // 🔴 KHONG con bia ma benh nhan (chot 12 dot 1). Ho so vua tao la *tu
        // khai*: co so chua cap ma nao cho nguoi nay, nen MaBN de RONG. Truoc day
        // cong sinh BN-yyyyMMdd-#### cho co cho lap, nhung do khong phai ma co so
        // cap nen no danh lua ca nguoi doc du lieu lan duong nhan tai lieu.
        //
        // Thu tuc tu lo chong trung, va tu bo qua neu nguoi nay DA co ho so noi
        // HIS tai co so — luc do khong can them dong tu khai.
        if (idCoSo is not null && luuNguoi.Id > 0)
        {
            if (!string.IsNullOrWhiteSpace(quet?.MaBN))
            {
                await _thuTuc.SaveBenhNhanCoSoAsync(luuNguoi.Id, idCoSo.Value, quet.MaBN.Trim(), true);
            }
            else
            {
                await _thuTuc.TaoHoSoTuKhaiAsync(luuNguoi.Id, idCoSo.Value);
            }
        }

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct)
                       ?? await _db.TaiKhoans.FirstOrDefaultAsync(x => x.SDT == dinhDanh, ct);

        // MatKhauNoiBo de null: dot nay chua thi hanh phan bam (Dinh chinh ADR 0009).
        var luuTaiKhoan = await _thuTuc.SaveTaiKhoanAsync(
            taiKhoan?.Id ?? 0,
            string.IsNullOrWhiteSpace(sdt) ? (taiKhoan?.SDT ?? dinhDanh) : sdt,
            email ?? taiKhoan?.Email,
            taiKhoan?.Role ?? "BenhNhan",
            null);

        var idTaiKhoan = luuTaiKhoan.Id > 0 ? luuTaiKhoan.Id : (taiKhoan?.Id ?? 0);

        // Nhan chu so huu (ADR 0019). Da co chu KHAC thi thu tuc tra ResultCode 3
        // va khong doi gi — day la "ai khai truoc giu CCCD".
        //
        // 🔴 KHONG chan dang nhap khi trung chu: phien van phai vao duoc, chi la
        // ho so do khong thuoc ve tai khoan nay. Chan o day thi nguoi go nham mot
        // so CCCD se bi khoa hoan toan khoi cong ma khong hieu vi sao. Man *Ho so
        // cua toi* moi la cho hien loi va chi duong ra.
        // 🔴 Cua 3 "nhan chu so huu" da chet tu dot 1B: cot DM_BenhNhan.IDTaiKhoan
        // khong con, va "ho so thuoc ve ai" nay la cap (SDT x co so) cua chinh
        // dong do. Xem ADR 0034 va CONTEXT.md muc *Loi vao*.

        return luuNguoi.Id;
    }

    /// <summary>
    /// Benh nhan vao thang nhanh noi bo thi chua qua man Dang ky nen chua co ten.
    /// Tao ho so toi thieu de trang benh nhan co cai ma chao.
    /// </summary>
    private async Task BaoDamHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh,
                                            DanhTinhQuet? quet, CancellationToken ct)
    {
        // Khong duoc dung o "da co tai khoan": tai khoan la mot, nhung ho so thi
        // MOI CO SO MOT CAI. Benh nhan tung dung co so A sang co so B ma chi kiem
        // tai khoan thi B khong co ho so nao, va trang benh nhan khong biet chao ai.
        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
        if (idCoSo is null) return;

        var daCoHoSo = await _db.BenhNhans
            .AnyAsync(h => (h.SDT == dinhDanh || h.Email == dinhDanh)
                        && h.IdCoSo == idCoSo.Value, ct);

        if (daCoHoSo)
        {
            // Da co ho so roi thi khong phai dung them. NHUNG neu benh nhan vua quet ma
            // thi day la co hoi dien nhung o con trong (chot 11/09) — nhat la nhom ho so
            // dang mang ten la SO DIEN THOAI, do dang ky bang OTP tu de ra.
            if (quet is not null) await DienOTrongTuMaAsync(maCoSo, cccd, dinhDanh, quet, ct);
            return;
        }

        await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, string.Empty, quet, ct);
    }

    /// <summary>
    /// Dien du lieu tu ma QR vao ho so DA CO — dien nhung o dang TRONG, cong voi o ten khi
    /// ten cu la RAC (bang dung dinh danh). Luu MaBN neu co quet duoc tu phieu kham.
    /// </summary>
    private async Task DienOTrongTuMaAsync(string maCoSo, string cccd, string dinhDanh, DanhTinhQuet quet, CancellationToken ct)
    {
        var o = await TinhOCanDienAsync(cccd, dinhDanh, string.Empty, quet, ct);

        // Chi goi SaveBenhNhan khi co it nhat mot truong thay doi
        if (!string.IsNullOrEmpty(o.Ten) || o.Ngay is not null
            || !string.IsNullOrWhiteSpace(o.GioiTinh) || !string.IsNullOrWhiteSpace(o.DiaChi))
        {
            await _thuTuc.SaveBenhNhanAsync(
                cccd, o.Ten, null, null, o.DiaChi,
                idTaiKhoan: null,
                ngaySinh: o.Ngay,
                hoTenKhongDau: o.Slug,
                gioiTinh: o.GioiTinh);
        }

        // Luu/cap nhat MaBN vao DM_BenhNhanCoSo neu quet duoc ma tren phieu kham
        if (!string.IsNullOrWhiteSpace(quet?.MaBN))
        {
            var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
            if (idCoSo is not null)
            {
                var idTaiKhoan = await _db.TaiKhoans.AsNoTracking()
                    .Where(t => t.SDT == dinhDanh || t.Email == dinhDanh)
                    .Select(t => (long?)t.Id)
                    .FirstOrDefaultAsync(ct);

                var idBenhNhan = await _db.BenhNhans.AsNoTracking()
                    .Where(p => p.IdCoSo == idCoSo.Value
                             && (p.SDT == dinhDanh || p.Email == dinhDanh
                                 || (!string.IsNullOrWhiteSpace(cccd) && !LaMaGia(cccd) && p.CCCD == cccd)))
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(ct);

                if (idBenhNhan > 0)
                {
                    var hoSoCoSo = await _db.BenhNhans.AsNoTracking()
                        .FirstOrDefaultAsync(h => h.Id == idBenhNhan && h.IdCoSo == idCoSo.Value, ct);

                    if (hoSoCoSo == null || string.IsNullOrWhiteSpace(hoSoCoSo.MaBN))
                    {
                        await _thuTuc.SaveBenhNhanCoSoAsync(idBenhNhan, idCoSo.Value, quet.MaBN.Trim(), true);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 🔴 Thi hanh chot 11/09: <b>du lieu tu ma QR chi duoc dien vao o TRONG hoac o RAC</b>,
    /// khong dam len ban benh nhan da tu sua tay.
    ///
    /// Cach thi hanh dua han vao <c>DM_BenhNhan_Save</c>: moi cot deu la
    /// <c>ISNULL(NULLIF(@X,''), X)</c> nen <b>gui NULL (hay chuoi rong cho ten) la giu
    /// nguyen ban cu</b> — khoi phai sua thu tuc. Chi can o day quyet dinh gui gi.
    ///
    /// Hai cho phai can than:
    ///  · Ho so CHUA co thi nhanh INSERT lay THANG <c>@TenBN</c>, nen luc do buoc phai gui
    ///    ten that chu khong duoc gui rong.
    ///  · CCCD la <b>ma gia</b> thi tra cuu theo CCCD vo nghia (nhieu ho so chung mot ma,
    ///    tu sau chi muc co loc <c>19_</c>) — bo qua, cu xu nhu ho so moi.
    /// </summary>
    private async Task<(string Ten, DateTime? Ngay, string? GioiTinh, string? DiaChi, string? Slug)>
        TinhOCanDienAsync(string cccd, string dinhDanh, string hoTen, DanhTinhQuet? quet, CancellationToken ct)
    {
        var cu = (!string.IsNullOrWhiteSpace(cccd) && !LaMaGia(cccd))
            ? await _db.BenhNhans.AsNoTracking().FirstOrDefaultAsync(x => x.CCCD == cccd, ct)
            : null;

        var tenMuon = !string.IsNullOrWhiteSpace(hoTen) ? hoTen.Trim()
                    : !string.IsNullOrWhiteSpace(quet?.HoTen) ? quet!.HoTen!.Trim()
                    : dinhDanh;

        // Ten cu la RAC khi no bang dung dinh danh: do la ho so tu de ra luc dang ky bang
        // OTP, chua bao gio di qua man *Them ho so*.
        var tenCuLaRac = cu is null
                         || string.IsNullOrWhiteSpace(cu.TenBN)
                         || string.Equals(cu.TenBN.Trim(), dinhDanh, StringComparison.OrdinalIgnoreCase);

        var ten  = tenCuLaRac ? tenMuon : string.Empty;
        var slug = string.IsNullOrEmpty(ten) ? cu?.HoTenKhongDau : ChuanHoaTen.BoDau(ten);

        return (
            Ten:      ten,
            Ngay:     cu?.NgaySinh is null                    ? quet?.DoiNgay()       : null,
            GioiTinh: string.IsNullOrWhiteSpace(cu?.GioiTinh) ? quet?.DoiGioiTinh()   : null,
            DiaChi:   string.IsNullOrWhiteSpace(cu?.DiaChi)   ? Rong(quet?.DiaChi)    : null,
            Slug:     slug);
    }

    private static string? Rong(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Ma gia "khong co can cuoc" — cung bo voi <c>HoSoBenhNhanService</c>.</summary>
    private static readonly string[] MaGiaKhongCanCuoc = { "11111111111", "111111111111" };

    public static bool LaMaGia(string? cccd) =>
        !string.IsNullOrWhiteSpace(cccd) && MaGiaKhongCanCuoc.Contains(cccd.Trim());


    private static string? LayClaim(ClaimsPrincipal nguoiDung, string ten)
        => nguoiDung.FindFirst(ten)?.Value;

    private static string LayDienThoai(string dinhDanh, TaiKhoan taiKhoan)
        => dinhDanh.Contains('@') ? taiKhoan.SDT : dinhDanh;

    private static string? LayEmail(string dinhDanh, TaiKhoan taiKhoan)
        => dinhDanh.Contains('@') ? dinhDanh : taiKhoan.Email;

}
