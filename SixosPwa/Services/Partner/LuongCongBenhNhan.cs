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

    /// <summary>Buoc 1 cua man Lien ket: xin doi tac gui ma xac thuc.</summary>
    Task<KetQuaThaoTac> GuiMaLienKetAsync(string maCoSo, string cccd, string dienThoai, CancellationToken ct = default);

    /// <summary>Buoc 2 cua man Lien ket: dat mat khau moi ca hai phia.</summary>
    Task<KetQuaBuoc> XacNhanLienKetAsync(string maCoSo, string cccd, string dienThoai, string ma, string matKhau, CancellationToken ct = default);

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

        // Da lien ket va da biet mat khau thi KHONG hoi doi tac nua: ta biet thua
        // la benh nhan co tai khoan ben do. Vua do mot lan goi mang moi lan dang
        // nhap, vua khong chet theo khi API doi tac sap.
        var taiKhoanDaCo = await TimTaiKhoanAsync(cccd, ct);
        if (taiKhoanDaCo is not null)
        {
            var lienKetDaCo = await TimLienKetAsync(taiKhoanDaCo.Id, maCoSo, ct);
            if (lienKetDaCo is not null && !string.IsNullOrWhiteSpace(lienKetDaCo.MatKhau))
            {
                return "/DangNhap/BanGiao" + thamSo;
            }
        }

        var tinhTrang = await coSo.Cua.TinhTrangTaiKhoanAsync(coSo.CauHinh, cccd, ct);

        // Khong hoi duoc doi tac thi khong doan bua: dua ve trang noi bo, man do
        // se noi ro la chua ket noi duoc voi co so.
        if (tinhTrang == TinhTrangTaiKhoan.KhongXacDinh)
        {
            _logger.LogWarning("Khong hoi duoc tinh trang tai khoan tai {MaCoSo}", maCoSo);
            await BaoDamHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, ct);
            return "/benh-nhan?loi=khong-ket-noi-duoc";
        }

        if (tinhTrang == TinhTrangTaiKhoan.ChuaCo)
        {
            return "/DangNhap/DangKy" + thamSo;
        }

        // Doi tac bao da co tai khoan, ma o tren ta chua tim thay lien ket nao
        // => benh nhan phai qua man Lien ket dung MOT lan (V9).
        return "/DangNhap/LienKet" + thamSo;
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

        var taiKhoan = await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, hoTen, ct);
        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);

        // Co so noi bo: xong o day, khong goi ra ngoai.
        if (coSo is null || !coSo.CoBanGiao)
        {
            await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);
            return new KetQuaBuoc(true, "Đã tạo tài khoản", "/benh-nhan");
        }

        var dienThoai = LayDienThoai(dinhDanh, taiKhoan);
        var email = LayEmail(dinhDanh, taiKhoan);

        // Nhanh co API: mat khau ben doi tac do may chu sinh ngau nhien, khong
        // hoi benh nhan (man Dang ky cung khong con o nhap nua).
        matKhau = SinhMatKhauChoDoiTac();

        var ketQua = await coSo.Cua.MoTaiKhoanAsync(
            coSo.CauHinh, new YeuCauMoTaiKhoan(hoTen, cccd, dienThoai, email, matKhau), ct);

        if (!ketQua.ThanhCong)
        {
            return new KetQuaBuoc(false, ketQua.ThongBao, null);
        }

        // Ma xac nhan do doi tac tra ve trong than phan hoi — benh nhan khong
        // nhin thay no. Cat lai de man Ban giao POST kem (diem tua #2, ADR 0003).
        await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, ketQua.MaXacNhan, ct);

        return new KetQuaBuoc(true, ketQua.ThongBao, ThemDichCuoi($"/DangNhap/BanGiao?coSo={Uri.EscapeDataString(maCoSo)}", returnUrl));
    }

    // ------------------------------------------------------------------
    //  Man Lien ket (benh nhan da co san tai khoan ben doi tac)
    // ------------------------------------------------------------------

    public async Task<KetQuaThaoTac> GuiMaLienKetAsync(string maCoSo, string cccd, string dienThoai, CancellationToken ct = default)
    {
        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);
        if (coSo is null || !coSo.CoBanGiao)
        {
            return new KetQuaThaoTac(false, "Cơ sở này không cần liên kết tài khoản");
        }

        return await coSo.Cua.GuiMaLienKetAsync(coSo.CauHinh, cccd, dienThoai, ct);
    }

    public async Task<KetQuaBuoc> XacNhanLienKetAsync(string maCoSo, string cccd, string dienThoai, string ma, string matKhau, CancellationToken ct = default)
    {
        var coSo = await _cuaFactory.LayAsync(maCoSo, ct);
        if (coSo is null || !coSo.CoBanGiao)
        {
            return new KetQuaBuoc(false, "Cơ sở này không cần liên kết tài khoản", null);
        }

        // Dat lai mat khau ben doi tac bang mot chuoi ngau nhien moi, dong bo
        // voi luong Dang ky. Benh nhan khong can biet no la gi.
        matKhau = SinhMatKhauChoDoiTac();

        var ketQua = await coSo.Cua.DatLaiMatKhauAsync(coSo.CauHinh, cccd, dienThoai, ma, matKhau, ct);
        if (!ketQua.ThanhCong)
        {
            return new KetQuaBuoc(false, ketQua.ThongBao, null);
        }

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct)
                       ?? await TaoHoSoNoiBoAsync(maCoSo, cccd, dienThoai, string.Empty, ct);

        await LuuLienKetAsync(taiKhoan.Id, maCoSo, matKhau, null, ct);

        return new KetQuaBuoc(true, ketQua.ThongBao, $"/DangNhap/BanGiao?coSo={Uri.EscapeDataString(maCoSo)}");
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

        // Co so co API: doi ben ho TRUOC. Ho khong nhan thi khong ghi ben minh,
        // de hai phia khong lech nhau.
        if (coSo is not null && coSo.CoBanGiao)
        {
            var dienThoai = LayClaim(nguoiDung, ClaimTypes.MobilePhone) ?? taiKhoan.SDT;
            var guiMa = await coSo.Cua.GuiMaLienKetAsync(coSo.CauHinh, cccd, dienThoai, ct);

            if (!guiMa.ThanhCong)
            {
                return new KetQuaThaoTac(false,
                    "Không đổi được mật khẩu tại cơ sở. Vui lòng dùng màn Liên kết hồ sơ.");
            }

            return new KetQuaThaoTac(false,
                "Cơ sở vừa gửi mã xác thực cho bạn. Vui lòng nhập mã ở màn Liên kết hồ sơ để hoàn tất.");
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
