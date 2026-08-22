using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

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

    private readonly ApplicationDbContext _db;
    private readonly IPartnerGatewayFactory _cuaFactory;
    private readonly ILogger<LuongCongBenhNhan> _logger;

    public LuongCongBenhNhan(
        ApplicationDbContext db,
        IPartnerGatewayFactory cuaFactory,
        ILogger<LuongCongBenhNhan> logger)
    {
        _db = db;
        _cuaFactory = cuaFactory;
        _logger = logger;
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

    // ------------------------------------------------------------------
    //  Man Dang ky
    // ------------------------------------------------------------------

    public async Task<KetQuaBuoc> MoTaiKhoanAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, string matKhau, string? returnUrl = null, CancellationToken ct = default)
    {
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

        // Chon giup ho so CHINH CHU (SoCccd trung CCCD dang nhap) de benh nhan
        // khong phai qua them mot man cua doi tac. Khong tim thay thi de trong,
        // ho so nguoi than van do ho tu chon.
        var idHoSo = await coSo.Cua.TimHoSoChinhChuAsync(coSo.CauHinh, cccd, lienKet.MatKhau ?? string.Empty, ct);

        var thongTin = coSo.Cua.DungThongTinBanGiao(coSo.CauHinh,
            new YeuCauBanGiao(cccd, dienThoai, taiKhoan.Email, lienKet.MaXacNhanTam, lienKet.MatKhau, yDinh, idHoSo));

        // Ma xac nhan chi dung duoc mot lan. Xoa ngay de lan ban giao sau di
        // duong dang nhap thuan, khong con OTP.
        if (!string.IsNullOrWhiteSpace(lienKet.MaXacNhanTam))
        {
            lienKet.MaXacNhanTam = null;
            await _db.SaveChangesAsync(ct);
        }

        return thongTin;
    }

    // ------------------------------------------------------------------
    //  Ho so noi bo
    // ------------------------------------------------------------------

    private Task<TaiKhoan?> TimTaiKhoanAsync(string cccd, CancellationToken ct)
        => _db.TaiKhoans.FirstOrDefaultAsync(x => x.CCCD == cccd, ct);

    /// <summary>
    /// Lien ket cua benh nhan tai mot co so. Neu co so nay chua co, tim sang cac
    /// co so KHAC CUNG MOT HE DOI TAC (cung TrangChu): ben ho chi co MOT tai
    /// khoan dung chung cho moi chi nhanh, nen bat lien ket lai tung chi nhanh la
    /// bat lam mot viec vo ich.
    /// </summary>
    private async Task<TaiKhoanDoiTac?> TimLienKetAsync(long idTaiKhoan, string maCoSo, CancellationToken ct)
    {
        var lienKet = await _db.TaiKhoanDoiTacs
            .FirstOrDefaultAsync(x => x.IdTaiKhoan == idTaiKhoan && x.MaCoSo == maCoSo, ct);

        if (lienKet is not null) return lienKet;

        var trangChu = await _db.DoiTacApis
            .AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo)
            .Select(x => x.TrangChu)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(trangChu)) return null;

        var maCoSoAnhEm = await _db.DoiTacApis
            .AsNoTracking()
            .Where(x => x.TrangChu == trangChu && x.MaCoSo != maCoSo)
            .Select(x => x.MaCoSo)
            .ToListAsync(ct);

        if (maCoSoAnhEm.Count == 0) return null;

        return await _db.TaiKhoanDoiTacs
            .FirstOrDefaultAsync(x => x.IdTaiKhoan == idTaiKhoan
                                   && maCoSoAnhEm.Contains(x.MaCoSo)
                                   && x.MatKhau != null, ct);
    }

    private async Task LuuLienKetAsync(long idTaiKhoan, string maCoSo, string matKhau, string? maXacNhan, CancellationToken ct)
    {
        var lienKet = await TimLienKetAsync(idTaiKhoan, maCoSo, ct);

        if (lienKet is null)
        {
            lienKet = new TaiKhoanDoiTac { IdTaiKhoan = idTaiKhoan, MaCoSo = maCoSo };
            await _db.TaiKhoanDoiTacs.AddAsync(lienKet, ct);
        }

        lienKet.MatKhau = matKhau;
        lienKet.MaXacNhanTam = maXacNhan;
        lienKet.DaLienKet = true;
        lienKet.NgayLienKet = DateTime.Now;

        await _db.SaveChangesAsync(ct);
    }

    private async Task<TaiKhoan> TaoHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, CancellationToken ct)
    {
        var laEmail = dinhDanh.Contains('@');
        var sdt = laEmail ? string.Empty : dinhDanh;
        var email = laEmail ? dinhDanh : null;

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct)
                       ?? await _db.TaiKhoans.FirstOrDefaultAsync(x => x.SDT == dinhDanh, ct);

        if (taiKhoan is null)
        {
            taiKhoan = new TaiKhoan { SDT = sdt, Email = email, CCCD = cccd, Role = "BenhNhan" };
            await _db.TaiKhoans.AddAsync(taiKhoan, ct);
        }
        else
        {
            taiKhoan.CCCD ??= cccd;
            if (!laEmail && string.IsNullOrWhiteSpace(taiKhoan.SDT)) taiKhoan.SDT = sdt;
            if (laEmail && string.IsNullOrWhiteSpace(taiKhoan.Email)) taiKhoan.Email = email;
        }

        await _db.SaveChangesAsync(ct);

        // MaDT ghi bang MaCoSo (V7): DMCSKCB va DMDoiTac hien khong co cot nao noi
        // voi nhau, va MaCoSo moi la thu ca luong nay mang theo.
        var daCoHoSo = await _db.BenhNhans.AnyAsync(x => x.SDT == dinhDanh && x.MaDT == maCoSo, ct);

        if (!daCoHoSo)
        {
            await _db.BenhNhans.AddAsync(new BenhNhan
            {
                MaBN = SinhMaBenhNhan(),
                MaDT = maCoSo,
                SDT = sdt,
                Email = email,
                TenBN = string.IsNullOrWhiteSpace(hoTen) ? dinhDanh : hoTen
            }, ct);

            await _db.SaveChangesAsync(ct);
        }

        return taiKhoan;
    }

    /// <summary>
    /// Benh nhan vao thang nhanh noi bo thi chua qua man Dang ky nen chua co ten.
    /// Tao ho so toi thieu de trang benh nhan co cai ma chao.
    /// </summary>
    private async Task BaoDamHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, CancellationToken ct)
    {
        var taiKhoan = await TimTaiKhoanAsync(cccd, ct);
        if (taiKhoan is not null) return;

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
