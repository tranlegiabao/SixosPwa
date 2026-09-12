using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Cay quyet dinh cua cong benh nhan: sau khi xac thuc thi di dau, mo tai khoan
/// ben doi tac the nao, lien ket ho so cu ra sao. Tach khoi controller de bon man
/// (Dang nhap, Dang ky, Lien ket, Ban giao) dung chung MOT cay, khong ai tu che lai.
/// </summary>
public interface ILuongCongBenhNhan
{
    /// <summary>Cho ha canh sau khi OTP dung.</summary>
    Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl,
                                  DanhTinhQuet? quet = null, CancellationToken ct = default);

    /// <summary>Cua cua mot co so — de man hinh biet co so do co ban giao hay khong.</summary>
    Task<CuaCoSo?> LayCuaAsync(string? maCoSo, CancellationToken ct = default);

    /// <summary>Co so co dang hien thi cong khai khong (DM_CSKCB.Active). ADR 0013.</summary>
    Task<bool> CoSoDangHienThiAsync(string? maCoSo, CancellationToken ct = default);

    /// <summary>Tao ho so noi bo + mo tai khoan ben doi tac (neu co).</summary>
    Task<KetQuaBuoc> MoTaiKhoanAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, string matKhau, string? returnUrl = null, CancellationToken ct = default);

    // --- Bo man cua doi tac (ADR 0014) -------------------------------------
    //  Chi chay khi CuaCoSo.DungManDoiTac = true. Doi tac la NGUON SU THAT cua
    //  mat khau; SixosPwa khong tu phan xu dung/sai bao gio.

    /// <summary>Man Dang nhap kieu doi tac: doi tac kiem mat khau, ta tao ho so + cho dich Ban giao.</summary>
    Task<KetQuaBuoc> DangNhapDoiTacAsync(string maCoSo, string cccd, string matKhau, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>
    /// Phien SixosPwa con song: dang nhap lai ho benh nhan bang mat khau DA CAT,
    /// de ho khong phai go CCCD + mat khau lan nua. Van hoi doi tac that su —
    /// mat khau cat o day co the da cu (benh nhan doi ben trang cua ho). ADR 0016.
    /// </summary>
    Task<KetQuaBuoc> DangNhapLaiBangMatKhauDaCatAsync(string maCoSo, string cccd, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>Buoc 1 man Dang ky kieu doi tac: xin doi tac mo tai khoan va tu gui ma xac thuc (SMS).</summary>
    Task<KetQuaThaoTac> DangKyDoiTacAsync(string maCoSo, string cccd, string dienThoai, string? email, string matKhau, int kenh, CancellationToken ct = default);

    /// <summary>Buoc 2 man Dang ky kieu doi tac: doi ma benh nhan vua go, roi cho dich Ban giao.</summary>
    Task<KetQuaBuoc> XacThucMaDoiTacAsync(string maCoSo, string cccd, string ma, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>Chi nhanh cua doi tac — de man dang nhap hien thong tin cua ho o kho may tinh.</summary>
    Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhDoiTacAsync(string maCoSo, CancellationToken ct = default);

    /// <summary>Man Quen mat khau kieu doi tac: xin doi tac gui duong dan dat lai mat khau.</summary>
    Task<KetQuaThaoTac> QuenMatKhauDoiTacAsync(string maCoSo, string cccd, string emailHoacSdt, CancellationToken ct = default);

    /// <summary>Doi mat khau, ghi sang CA HAI phia (V9).</summary>
    Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default);

    /// <summary>Dung du lieu cho form ban giao. Null neu chua du dieu kien.</summary>
    Task<ThongTinBanGiao?> DungThongTinBanGiaoAsync(string maCoSo, ClaimsPrincipal nguoiDung, string? yDinh = null, CancellationToken ct = default);
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

    /// <summary>
    /// Do dai mat khau sinh cho he doi tac. Ben ho bat TOI THIEU 6 ky tu
    /// (RegisterService: "Mat khau phai co toi thieu 6 ky tu"), user chot TOI DA
    /// 6 — nen chi con dung mot con so.
    /// </summary>
    private const int DoDaiMatKhauDoiTac = 6;

    /// <summary>
    /// Sinh mat khau ngau nhien cho tai khoan ben he doi tac (user chot 22/08).
    /// Benh nhan khong bao gio phai go no: SixosPwa cat lai trong
    /// TaiKhoan_DoiTac.MatKhau va tu dien khi ban giao.
    ///
    /// CO Y KHONG dung CCCD lam mat khau: CCCD in tren giay to, ai doc duoc la
    /// dang nhap thang vao trang cua doi tac.
    ///
    /// Bo ky tu bo qua 0/O va 1/l/I — mat khau nay co the phai doc cho nhan vien
    /// ho tro qua dien thoai, nham mot ky tu la mat cong lam lai tu dau.
    /// Dung RandomNumberGenerator chu khong phai Random: Random doan duoc.
    /// </summary>
    private static string SinhMatKhauChoDoiTac()
    {
        const string boKyTu = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";

        var ky = new char[DoDaiMatKhauDoiTac];
        for (var i = 0; i < ky.Length; i++)
        {
            ky[i] = boKyTu[System.Security.Cryptography.RandomNumberGenerator.GetInt32(boKyTu.Length)];
        }

        return new string(ky);
    }

    private readonly ApplicationDbContext _db;
    private readonly IPartnerGatewayFactory _cuaFactory;
    private readonly ILogger<LuongCongBenhNhan> _logger;
    private readonly AdminStoredProcedureService _thuTuc;

    public LuongCongBenhNhan(
        ApplicationDbContext db,
        IPartnerGatewayFactory cuaFactory,
        ILogger<LuongCongBenhNhan> logger,
        AdminStoredProcedureService thuTuc)
    {
        _db = db;
        _cuaFactory = cuaFactory;
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
    /// Co so co dang hien thi cong khai khong (DM_CSKCB.Active). Day la cong DUY NHAT
    /// quyet dinh co so co nhan DANG NHAP / DANG KY MOI hay khong. Khong tim thay ma
    /// co so thi tra false (hong theo huong an toan). Xem ADR 0013.
    ///
    /// CANH BAO: dung nham voi DM_DoiTacApi.Active ma PartnerGatewayFactory doc — hai
    /// co khac nhau, trung ten.
    /// </summary>
    public Task<bool> CoSoDangHienThiAsync(string? maCoSo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(maCoSo)) return Task.FromResult(false);

        var ma = maCoSo.Trim();
        return _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == ma)
            .Select(x => x.Active)
            .FirstOrDefaultAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Cay quyet dinh sau OTP
    // ------------------------------------------------------------------

    public async Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl,
                                               DanhTinhQuet? quet = null, CancellationToken ct = default)
    {
        var thamSo = $"?coSo={Uri.EscapeDataString(maCoSo)}";
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            thamSo += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);

        // Co so khong co API rieng: o lai trang benh nhan noi bo.
        if (coSo is null || !coSo.CoBanGiao)
        {
            await BaoDamHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, quet, ct);

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

        // Co so dung bo man cua doi tac thi benh nhan KHONG duoc di duong nay:
        // ho phai go mat khau that cua ho o man Dang nhap kieu doi tac. Day cung
        // la chot chan cho XacNhanOtp — action do goi tran duoc, va qua duoc guard
        // la chay tiep toi tan BaoDamHoSoNoiBoAsync (tao that DM_BenhNhan,
        // DM_BenhNhanCoSo, HT_TaiKhoan). ADR 0014.
        if (coSo.DungManDoiTac)
        {
            _logger.LogWarning("Co so {MaCoSo} dung man doi tac — tu choi duong OTP cua SixosPwa", maCoSo);
            return "/DangNhap/Login" + thamSo;
        }

        // Da lien ket va da biet mat khau thi ban giao thang, khong hoi doi tac.
        var taiKhoanDaCo = await TimTaiKhoanAsync(cccd, ct);
        if (taiKhoanDaCo is not null)
        {
            var lienKetDaCo = await TimLienKetAsync(taiKhoanDaCo.Id, maCoSo, ct);
            if (lienKetDaCo is not null && !string.IsNullOrWhiteSpace(lienKetDaCo.MatKhau))
            {
                return "/DangNhap/BanGiao" + thamSo;
            }
        }

        // Doi tac co ban giao nhung KHONG dung man cua ho (chua co ban cai nao nhu
        // vay, de danh cho doi tac tuong lai): chua co lien ket thi di man Dang ky.
        return "/DangNhap/DangKy" + thamSo;
    }

    public Task<CuaCoSo?> LayCuaAsync(string? maCoSo, CancellationToken ct = default)
        => _cuaFactory.LayAsync(maCoSo, ct);

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

        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);

        // Co so dung bo man cua doi tac thi khong di duong nay: benh nhan tu dat
        // mat khau o man Dang ky kieu doi tac, va chinh doi tac gui ma xac thuc.
        // Xem DangKyDoiTacAsync + ADR 0014.
        if (coSo is not null && coSo.DungManDoiTac)
        {
            return new KetQuaBuoc(false, "Cơ sở này đăng ký tài khoản tại trang của cơ sở", null);
        }

        var taiKhoan = await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, hoTen, ct: ct);

        // Co so noi bo: xong o day, khong goi ra ngoai.
        if (coSo is null || !coSo.CoBanGiao)
        {
            await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);

            // Cung luat voi ChonDichDenAsync — hai loi vao (dang nhap / dang ky)
            // phai di cung mot duong, neu khong nguoi dung thay hai hanh vi khac
            // nhau cho cung mot trang thai.
            //
            // Nguoi vua dang ky thuong chi co dung mot ho so nen se di thang;
            // nhung tai khoan cu dang ky them o co so moi thi van co the >1.
            var soHoSo = await DemHoSoTaiCoSoAsync(maCoSo, dinhDanh, ct);
            return new KetQuaBuoc(true, "Đã tạo tài khoản",
                soHoSo > 1 ? "/benh-nhan/ho-so" : "/benh-nhan");
        }

        var dienThoai = LayDienThoai(dinhDanh, taiKhoan);
        var email = LayEmail(dinhDanh, taiKhoan);

        // Doi tac co ban giao nhung KHONG dung man cua ho (de danh cho doi tac
        // tuong lai): mat khau ben do do may chu sinh, khong hoi benh nhan.
        matKhau = SinhMatKhauChoDoiTac();

        // Duong nay danh cho doi tac co ban giao nhung KHONG dung man cua ho, nen
        // benh nhan khong duoc chon kenh — de ban cai tu quyet bang mac dinh.
        var ketQua = await coSo.Cua.MoTaiKhoanAsync(
            coSo.CauHinh, new YeuCauMoTaiKhoan(hoTen, cccd, dienThoai, email, matKhau, 0), ct);

        if (!ketQua.ThanhCong)
        {
            return new KetQuaBuoc(false, ketQua.ThongBao, null);
        }

        await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);

        return new KetQuaBuoc(true, ketQua.ThongBao, ThemDichCuoi($"/DangNhap/BanGiao?coSo={Uri.EscapeDataString(maCoSo)}", returnUrl));
    }

    // ------------------------------------------------------------------
    //  Bo man cua doi tac — Dang nhap / Dang ky / Quen mat khau (ADR 0014)
    // ------------------------------------------------------------------

    /// <summary>
    /// Co so nay co dung bo man cua doi tac khong. Dung de chan tu xa: cac duong
    /// duoi day khong duoc chay cho co so noi bo, va nguoc lai luong OTP cua
    /// SixosPwa khong duoc chay cho co so doi tac.
    /// </summary>
    private async Task<CuaCoSo?> LayCuaDoiTacAsync(string maCoSo, CancellationToken ct)
    {
        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);
        return coSo is not null && coSo.DungManDoiTac ? coSo : null;
    }

    public async Task<KetQuaBuoc> DangNhapDoiTacAsync(string maCoSo, string cccd, string matKhau, string? returnUrl = null, CancellationToken ct = default)
    {
        var coSo = await LayCuaDoiTacAsync(maCoSo, ct);
        if (coSo is null)
        {
            return new KetQuaBuoc(false, "Cơ sở này không dùng tài khoản của đối tác", null);
        }

        // Co so dang an thi khong cho vao — giong het cua ngo cua luong OTP. ADR 0013.
        if (!await CoSoDangHienThiAsync(maCoSo, ct))
        {
            return new KetQuaBuoc(false, "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến.", null);
        }

        // Doi tac phan xu mat khau, khong phai ta. Ho tu bao loi tieng Viet gi thi
        // hien lai nguyen van cho benh nhan.
        var ketQua = await coSo.Cua.DangNhapAsync(coSo.CauHinh, cccd, matKhau, ct);
        if (!ketQua.ThanhCong)
        {
            return new KetQuaBuoc(false, ketQua.ThongBao, null, ketQua.DoiTacHong);
        }

        return new KetQuaBuoc(true, ketQua.ThongBao,
            await GhiHoSoRoiChoBanGiaoAsync(maCoSo, cccd, matKhau, returnUrl, ct));
    }

    /// <summary>
    /// Duong "khoi go lai": phien con song va DA CO dau an cua doi tac, nen ta lay
    /// mat khau da cat ra dang nhap ho.
    ///
    /// KHONG tu quyet dung/sai o day — van goi DangNhapDoiTacAsync nhu duong go tay,
    /// vi mat khau cat trong HT_TaiKhoanDoiTac co the da cu: benh nhan doi mat khau
    /// TREN TRANG CUA DOI TAC (ke ca qua Quen mat khau — HT_QuenMatKhauServices dung
    /// {request.Host} nen duong dan dat lai luon tro ve ben ho) ma SixosPwa khong he
    /// hay biet. Doi tac tu phan xu, ta chi dua cau tra loi cua ho ra man hinh.
    ///
    /// Goi LoginAsync ben ho la an toan: do la truy van doc thuan, khong dem lan sai
    /// va khong khoa tai khoan.
    /// </summary>
    public async Task<KetQuaBuoc> DangNhapLaiBangMatKhauDaCatAsync(string maCoSo, string cccd, string? returnUrl = null, CancellationToken ct = default)
    {
        // Hai guard nay chay TRUOC va tra cau cua chinh chung, khong duoc de roi
        // xuong khoi viet de o duoi: "co so dang tam ngung" ma bao thanh "mat khau
        // da thay doi" thi benh nhan ngoi doi mat khau ca buoi vo ich.
        var coSo = await LayCuaDoiTacAsync(maCoSo, ct);
        if (coSo is null)
        {
            return new KetQuaBuoc(false, "Cơ sở này không dùng tài khoản của đối tác", null);
        }

        if (!await CoSoDangHienThiAsync(maCoSo, ct))
        {
            return new KetQuaBuoc(false, "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến.", null);
        }

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct);
        var lienKet = taiKhoan is null ? null : await TimLienKetAsync(taiKhoan.Id, maCoSo, ct);

        // Chua tung ban giao tu may nay thi khong co gi de dung lai — de benh nhan
        // go nhu binh thuong, khong bia mat khau.
        if (lienKet is null || string.IsNullOrWhiteSpace(lienKet.MatKhau))
        {
            return new KetQuaBuoc(false, "Vui lòng đăng nhập để tiếp tục.", null);
        }

        var ketQua = await DangNhapDoiTacAsync(maCoSo, cccd, lienKet.MatKhau, returnUrl, ct);

        // Toi day thi chi con hai kha nang: doi tac tu choi MAT KHAU CU, hoac doi
        // tac hong. Benh nhan khong go gi ca, nen cau "Thong tin dang nhap khong
        // chinh xac" cua ho doc len la vo nghia — phai noi ro vi sao tu nhien lai
        // hien man dang nhap. Doi tac hong thi giu NGUYEN VAN cau cua ho (ADR 0015).
        if (!ketQua.ThanhCong && !ketQua.DoiTacHong)
        {
            return ketQua with
            {
                ThongBao = "Mật khẩu của bạn tại cơ sở đã thay đổi, vui lòng đăng nhập lại."
            };
        }

        return ketQua;
    }

    public async Task<KetQuaThaoTac> DangKyDoiTacAsync(string maCoSo, string cccd, string dienThoai, string? email, string matKhau, int kenh, CancellationToken ct = default)
    {
        var coSo = await LayCuaDoiTacAsync(maCoSo, ct);
        if (coSo is null)
        {
            return new KetQuaThaoTac(false, "Cơ sở này không dùng tài khoản của đối tác");
        }

        if (!await CoSoDangHienThiAsync(maCoSo, ct))
        {
            return new KetQuaThaoTac(false, "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến.");
        }

        var ketQua = await coSo.Cua.MoTaiKhoanAsync(
            coSo.CauHinh, new YeuCauMoTaiKhoan(string.Empty, cccd, dienThoai, email, matKhau, kenh), ct);

        if (!ketQua.ThanhCong)
        {
            return ketQua;
        }

        // Cat mat khau NGAY o buoc nay, truoc khi benh nhan go ma. Nho vay buoc 2
        // khong phai bat client gui lai mat khau qua mang lan nua. Ho so noi bo
        // luc nay giong ben doi tac: da tao nhung chua xac thuc.
        var taiKhoan = await TaoHoSoNoiBoAsync(maCoSo, cccd, string.IsNullOrWhiteSpace(email) ? dienThoai : email, string.Empty, ct: ct);
        await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);

        return ketQua;
    }

    public async Task<KetQuaBuoc> XacThucMaDoiTacAsync(string maCoSo, string cccd, string ma, string? returnUrl = null, CancellationToken ct = default)
    {
        var coSo = await LayCuaDoiTacAsync(maCoSo, ct);
        if (coSo is null)
        {
            return new KetQuaBuoc(false, "Cơ sở này không dùng tài khoản của đối tác", null);
        }

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct);
        var lienKet = taiKhoan is null ? null : await TimLienKetAsync(taiKhoan.Id, maCoSo, ct);

        // Mat khau da cat o buoc 1. Khong con no thi khong ban giao duoc, va cung
        // khong nen doan bua — bat benh nhan lam lai tu dau con hon.
        if (lienKet is null || string.IsNullOrWhiteSpace(lienKet.MatKhau))
        {
            return new KetQuaBuoc(false, "Phiên đăng ký đã hết hạn, vui lòng đăng ký lại", null);
        }

        var dienThoai = taiKhoan!.SDT;
        var ketQua = await coSo.Cua.XacThucMaAsync(coSo.CauHinh, cccd, taiKhoan.Email, dienThoai, ma, ct);
        if (!ketQua.ThanhCong)
        {
            return new KetQuaBuoc(false, ketQua.ThongBao, null);
        }

        return new KetQuaBuoc(true, ketQua.ThongBao,
            await GhiHoSoRoiChoBanGiaoAsync(maCoSo, cccd, lienKet.MatKhau, returnUrl, ct));
    }

    public async Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhDoiTacAsync(string maCoSo, CancellationToken ct = default)
    {
        var coSo = await LayCuaDoiTacAsync(maCoSo, ct);
        return coSo is null
            ? Array.Empty<ChiNhanhDoiTac>()
            : await coSo.Cua.LayChiNhanhAsync(coSo.CauHinh, ct);
    }

    public async Task<KetQuaThaoTac> QuenMatKhauDoiTacAsync(string maCoSo, string cccd, string emailHoacSdt, CancellationToken ct = default)
    {
        var coSo = await LayCuaDoiTacAsync(maCoSo, ct);
        if (coSo is null)
        {
            return new KetQuaThaoTac(false, "Cơ sở này không dùng tài khoản của đối tác");
        }

        // KHONG tao ho so noi bo o day: duong dan dat lai mat khau do doi tac gui
        // va tro ve TRANG CUA HO (HT_QuenMatKhauServices dung {request.Host}), nen
        // ta khong biet benh nhan co hoan tat hay khong. Ho so sinh ra o lan dang
        // nhap sau. Xem ADR 0014.
        return await coSo.Cua.QuenMatKhauAsync(coSo.CauHinh, cccd, emailHoacSdt, ct);
    }

    /// <summary>
    /// Dam bao co ho so + lien ket noi bo (co mat khau de ban giao), roi tra ve
    /// duong dan man Ban giao. Dung chung cho ca duong Dang nhap lan Dang ky.
    /// </summary>
    private async Task<string> GhiHoSoRoiChoBanGiaoAsync(string maCoSo, string cccd, string matKhau, string? returnUrl, CancellationToken ct)
    {
        var taiKhoan = await TimTaiKhoanAsync(cccd, ct);

        // Lan dau vao tu co so nay: chua co ho so noi bo nao. Dinh danh de trong
        // vi ta chi biet CCCD — doi tac giu ten/dien thoai that cua benh nhan.
        taiKhoan ??= await TaoHoSoNoiBoAsync(maCoSo, cccd, string.Empty, string.Empty, ct: ct);

        // maXacNhan = null: ban giao di duong dang nhap thuan bang mat khau that
        // cua benh nhan, khong con dung ma xac nhan cua doi tac nua (ADR 0014).
        await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);

        return ThemDichCuoi($"/DangNhap/BanGiao?coSo={Uri.EscapeDataString(maCoSo)}", returnUrl);
    }


    // ------------------------------------------------------------------
    //  Man Doi mat khau — ghi CA HAI phia
    // ------------------------------------------------------------------

    public async Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default)
    {
        var cccd = LayClaim(nguoiDung, ClaimCccd);
        if (string.IsNullOrWhiteSpace(cccd))
        {
            return new KetQuaThaoTac(false, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
        }

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct);
        if (taiKhoan is null)
        {
            return new KetQuaThaoTac(false, "Không tìm thấy tài khoản của bạn");
        }

        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);

        // Co so dung man cua doi tac: mat khau la CUA HO, ta khong duoc ghi de.
        // Benh nhan doi mat khau tren trang cua doi tac. Man nay cung da duoc an
        // khoi menu cho nhom co so do — day chi la chot chan cuoi. ADR 0014.
        if (coSo is not null && coSo.DungManDoiTac)
        {
            return new KetQuaThaoTac(false,
                "Mật khẩu của bạn do cơ sở quản lý. Vui lòng đổi mật khẩu trên trang của cơ sở.");
        }

        await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhauMoi, null, ct);
        return new KetQuaThaoTac(true, "Đã đổi mật khẩu");
    }

    // ------------------------------------------------------------------
    //  Man Ban giao
    // ------------------------------------------------------------------

    public async Task<ThongTinBanGiao?> DungThongTinBanGiaoAsync(string maCoSo, ClaimsPrincipal nguoiDung, string? yDinh = null, CancellationToken ct = default)
    {
        var cccd = LayClaim(nguoiDung, ClaimCccd);
        if (string.IsNullOrWhiteSpace(cccd)) return null;

        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);
        if (coSo is null || !coSo.CoBanGiao) return null;

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct);
        if (taiKhoan is null) return null;

        var lienKet = await TimLienKetAsync(taiKhoan.Id, maCoSo, ct);
        if (lienKet is null) return null;

        var dienThoai = LayClaim(nguoiDung, ClaimTypes.MobilePhone) ?? taiKhoan.SDT;

        var thongTin = coSo.Cua.DungThongTinBanGiao(coSo.CauHinh,
            new YeuCauBanGiao(cccd, dienThoai, taiKhoan.Email, lienKet.MaXacNhanTam, lienKet.MatKhau, yDinh));

        // Ma xac nhan chi dung duoc mot lan. Xoa ngay de lan ban giao sau di
        // duong dang nhap thuan, khong con OTP.
        if (!string.IsNullOrWhiteSpace(lienKet.MaXacNhanTam))
        {
            await _thuTuc.XoaMaXacNhanAsync(lienKet.IdTaiKhoan, lienKet.IdCoSo);
        }

        return thongTin;
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
        // Chieu MOI truoc (ADR 0019): con nguoi tro ve tai khoan quan minh.
        var theoChuSoHuu = await (
            from p in _db.BenhNhans
            join t in _db.TaiKhoans on p.IdTaiKhoan equals t.Id
            where p.CCCD == cccd
            select t).FirstOrDefaultAsync(ct);

        if (theoChuSoHuu is not null) return theoChuSoHuu;

        // Lui ve chieu CU cho du lieu chua kip do sang. Script 09 do het mot lan,
        // nhung nhanh nay giu lai de phien dang song khong gay giua chung.
        return await (
            from t in _db.TaiKhoans
            join p in _db.BenhNhans on t.IdBenhNhan equals p.Id
            where p.CCCD == cccd
            select t).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Lien ket cua benh nhan tai mot co so. Neu co so nay chua co, tim sang cac
    /// co so KHAC CUNG MOT HE DOI TAC (cung TrangChu): ben ho chi co MOT tai
    /// khoan dung chung cho moi chi nhanh, nen bat lien ket lai tung chi nhanh la
    /// bat lam mot viec vo ich.
    /// </summary>
    private async Task<TaiKhoanDoiTac?> TimLienKetAsync(long idTaiKhoan, string maCoSo, CancellationToken ct)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
        if (idCoSo is null) return null;

        var lienKet = await _db.TaiKhoanDoiTacs
            .FirstOrDefaultAsync(x => x.IdTaiKhoan == idTaiKhoan && x.IdCoSo == idCoSo.Value, ct);

        if (lienKet is not null) return lienKet;

        var trangChu = await _db.DoiTacApis
            .AsNoTracking()
            .Where(x => x.IdCoSo == idCoSo.Value)
            .Select(x => x.TrangChu)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(trangChu)) return null;

        var idCoSoAnhEm = await _db.DoiTacApis
            .AsNoTracking()
            .Where(x => x.TrangChu == trangChu && x.IdCoSo != idCoSo.Value)
            .Select(x => x.IdCoSo)
            .ToListAsync(ct);

        if (idCoSoAnhEm.Count == 0) return null;

        return await _db.TaiKhoanDoiTacs
            .FirstOrDefaultAsync(x => x.IdTaiKhoan == idTaiKhoan
                                   && idCoSoAnhEm.Contains(x.IdCoSo)
                                   && x.MatKhau != null, ct);
    }

    private async Task LuuLienKetAsync(long idTaiKhoan, string maCoSo, string matKhau, string? maXacNhan, CancellationToken ct)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
        if (idCoSo is null)
        {
            _logger.LogWarning("Khong tim thay co so {MaCoSo} de luu lien ket", maCoSo);
            return;
        }

        // Thu tuc tu lo them-hay-cap-nhat; UK_HT_TaiKhoanDoiTac chan trung (ADR 0008).
        await _thuTuc.SaveTaiKhoanDoiTacAsync(idTaiKhoan, idCoSo.Value, matKhau, maXacNhan, true);
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

        return await (
            from p in _db.BenhNhans.AsNoTracking()
            join h in _db.BenhNhanCoSos.AsNoTracking() on p.Id equals h.IdBenhNhan
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals cs.Id
            where ((idTaiKhoan != null && p.IdTaiKhoan == idTaiKhoan)
                   || (p.IdTaiKhoan == null && (p.SDT == dinhDanh || p.Email == dinhDanh)))
                  && cs.MaCoSo == maCoSo
                  && h.DaMoTaiLieu
            select h.Id).CountAsync(ct);
    }

    private async Task<TaiKhoan> TaoHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, string hoTen,
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
            null,
            luuNguoi.Id > 0 ? luuNguoi.Id : taiKhoan?.IdBenhNhan);

        var idTaiKhoan = luuTaiKhoan.Id > 0 ? luuTaiKhoan.Id : (taiKhoan?.Id ?? 0);

        // Nhan chu so huu (ADR 0019). Da co chu KHAC thi thu tuc tra ResultCode 3
        // va khong doi gi — day la "ai khai truoc giu CCCD".
        //
        // 🔴 KHONG chan dang nhap khi trung chu: phien van phai vao duoc, chi la
        // ho so do khong thuoc ve tai khoan nay. Chan o day thi nguoi go nham mot
        // so CCCD se bi khoa hoan toan khoi cong ma khong hieu vi sao. Man *Ho so
        // cua toi* moi la cho hien loi va chi duong ra.
        if (luuNguoi.Id > 0 && idTaiKhoan > 0)
        {
            var nhanChu = await _thuTuc.NhanChuSoHuuAsync(luuNguoi.Id, idTaiKhoan);
            if (!nhanChu.Succeeded)
            {
                _logger.LogInformation(
                    "Ho so {IdBenhNhan} da thuoc tai khoan khac, tai khoan {IdTaiKhoan} khong nhan duoc: {ThongBao}",
                    luuNguoi.Id, idTaiKhoan, nhanChu.Message);
            }
        }

        return await _db.TaiKhoans.FirstAsync(x => x.Id == idTaiKhoan, ct);
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

        var daCoHoSo = await (
            from p in _db.BenhNhans
            join h in _db.BenhNhanCoSos on p.Id equals h.IdBenhNhan
            where (p.SDT == dinhDanh || p.Email == dinhDanh) && h.IdCoSo == idCoSo.Value
            select p.Id).AnyAsync(ct);

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
                    .Where(p => (idTaiKhoan != null && p.IdTaiKhoan == idTaiKhoan)
                                || p.SDT == dinhDanh || p.Email == dinhDanh
                                || (!string.IsNullOrWhiteSpace(cccd) && !LaMaGia(cccd) && p.CCCD == cccd))
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(ct);

                if (idBenhNhan > 0)
                {
                    var hoSoCoSo = await _db.BenhNhanCoSos.AsNoTracking()
                        .FirstOrDefaultAsync(h => h.IdBenhNhan == idBenhNhan && h.IdCoSo == idCoSo.Value, ct);

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

    /// <summary>Gan y dinh (returnUrl) vao duong dan Ban giao neu co.</summary>
    private static string ThemDichCuoi(string duongDan, string? returnUrl)
        => string.IsNullOrWhiteSpace(returnUrl)
            ? duongDan
            : $"{duongDan}&returnUrl={Uri.EscapeDataString(returnUrl)}";

    private static string? LayClaim(ClaimsPrincipal nguoiDung, string ten)
        => nguoiDung.FindFirst(ten)?.Value;

    private static string LayDienThoai(string dinhDanh, TaiKhoan taiKhoan)
        => dinhDanh.Contains('@') ? taiKhoan.SDT : dinhDanh;

    private static string? LayEmail(string dinhDanh, TaiKhoan taiKhoan)
        => dinhDanh.Contains('@') ? dinhDanh : taiKhoan.Email;

}
