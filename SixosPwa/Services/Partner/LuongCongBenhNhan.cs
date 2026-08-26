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
    Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl, CancellationToken ct = default);

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

    /// <summary>Buoc 1 man Dang ky kieu doi tac: xin doi tac mo tai khoan va tu gui ma xac thuc (SMS).</summary>
    Task<KetQuaThaoTac> DangKyDoiTacAsync(string maCoSo, string cccd, string dienThoai, string? email, string matKhau, CancellationToken ct = default);

    /// <summary>Buoc 2 man Dang ky kieu doi tac: doi ma benh nhan vua go, roi cho dich Ban giao.</summary>
    Task<KetQuaBuoc> XacThucMaDoiTacAsync(string maCoSo, string cccd, string ma, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>Man Quen mat khau kieu doi tac: xin doi tac gui duong dan dat lai mat khau.</summary>
    Task<KetQuaThaoTac> QuenMatKhauDoiTacAsync(string maCoSo, string cccd, string emailHoacSdt, CancellationToken ct = default);

    /// <summary>Doi mat khau, ghi sang CA HAI phia (V9).</summary>
    Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default);

    /// <summary>Dung du lieu cho form ban giao. Null neu chua du dieu kien.</summary>
    Task<ThongTinBanGiao?> DungThongTinBanGiaoAsync(string maCoSo, ClaimsPrincipal nguoiDung, string? yDinh = null, CancellationToken ct = default);
}

/// <summary>Ket qua mot buoc co dich den ke tiep.</summary>
public record KetQuaBuoc(bool ThanhCong, string ThongBao, string? DichDen);

public class LuongCongBenhNhan : ILuongCongBenhNhan
{
    /// <summary>Claim giu so CCCD cua benh nhan trong phien.</summary>
    public const string ClaimCccd = "Cccd";

    /// <summary>Claim giu ma co so benh nhan dang dung trong phien.</summary>
    public const string ClaimMaCoSo = "MaCoSo";

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

    public async Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl, CancellationToken ct = default)
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
            await BaoDamHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, ct);
            return "/benh-nhan";
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

        var taiKhoan = await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, hoTen, ct);

        // Co so noi bo: xong o day, khong goi ra ngoai.
        if (coSo is null || !coSo.CoBanGiao)
        {
            await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);
            return new KetQuaBuoc(true, "Đã tạo tài khoản", "/benh-nhan");
        }

        var dienThoai = LayDienThoai(dinhDanh, taiKhoan);
        var email = LayEmail(dinhDanh, taiKhoan);

        // Doi tac co ban giao nhung KHONG dung man cua ho (de danh cho doi tac
        // tuong lai): mat khau ben do do may chu sinh, khong hoi benh nhan.
        matKhau = SinhMatKhauChoDoiTac();

        var ketQua = await coSo.Cua.MoTaiKhoanAsync(
            coSo.CauHinh, new YeuCauMoTaiKhoan(hoTen, cccd, dienThoai, email, matKhau), ct);

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
            return new KetQuaBuoc(false, ketQua.ThongBao, null);
        }

        return new KetQuaBuoc(true, ketQua.ThongBao,
            await GhiHoSoRoiChoBanGiaoAsync(maCoSo, cccd, matKhau, returnUrl, ct));
    }

    public async Task<KetQuaThaoTac> DangKyDoiTacAsync(string maCoSo, string cccd, string dienThoai, string? email, string matKhau, CancellationToken ct = default)
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
            coSo.CauHinh, new YeuCauMoTaiKhoan(string.Empty, cccd, dienThoai, email, matKhau), ct);

        if (!ketQua.ThanhCong)
        {
            return ketQua;
        }

        // Cat mat khau NGAY o buoc nay, truoc khi benh nhan go ma. Nho vay buoc 2
        // khong phai bat client gui lai mat khau qua mang lan nua. Ho so noi bo
        // luc nay giong ben doi tac: da tao nhung chua xac thuc.
        var taiKhoan = await TaoHoSoNoiBoAsync(maCoSo, cccd, string.IsNullOrWhiteSpace(email) ? dienThoai : email, string.Empty, ct);
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
        taiKhoan ??= await TaoHoSoNoiBoAsync(maCoSo, cccd, string.Empty, string.Empty, ct);

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
    private Task<TaiKhoan?> TimTaiKhoanAsync(string cccd, CancellationToken ct)
        => (from t in _db.TaiKhoans
            join p in _db.BenhNhans on t.IdBenhNhan equals p.Id
            where p.CCCD == cccd
            select t).FirstOrDefaultAsync(ct);

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

    private async Task<TaiKhoan> TaoHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, CancellationToken ct)
    {
        var laEmail = dinhDanh.Contains('@');
        var sdt = laEmail ? string.Empty : dinhDanh;
        var email = laEmail ? dinhDanh : null;

        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);

        // Con nguoi truoc (khoa CCCD), roi moi den ho so tai co so.
        var luuNguoi = await _thuTuc.SaveBenhNhanAsync(
            cccd,
            string.IsNullOrWhiteSpace(hoTen) ? dinhDanh : hoTen,
            sdt,
            email,
            null);

        if (idCoSo is not null && luuNguoi.Id > 0)
        {
            var daCoHoSo = await _db.BenhNhanCoSos
                .AnyAsync(x => x.IdBenhNhan == luuNguoi.Id && x.IdCoSo == idCoSo.Value, ct);

            if (!daCoHoSo)
                await _thuTuc.SaveBenhNhanCoSoAsync(luuNguoi.Id, idCoSo.Value, SinhMaBenhNhan());
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

        return await _db.TaiKhoans.FirstAsync(x => x.Id == idTaiKhoan, ct);
    }

    /// <summary>
    /// Benh nhan vao thang nhanh noi bo thi chua qua man Dang ky nen chua co ten.
    /// Tao ho so toi thieu de trang benh nhan co cai ma chao.
    /// </summary>
    private async Task BaoDamHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, CancellationToken ct)
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

        if (daCoHoSo) return;

        await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, string.Empty, ct);
    }

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

    /// <summary>Giu dung khuon ma benh nhan dang dung trong DB: BN-yyyyMMdd-####.</summary>
    private static string SinhMaBenhNhan()
        => $"BN-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
}
