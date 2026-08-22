using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Ban cai cho Benh vien Ung Buou (DangKyOnlineUB + SixOSDatKhamAPI).
///
/// KHONG sua mot dong nao trong repo cua doi tac — chi dung cac cua DA CO SAN
/// va da la [AllowAnonymous]. Toan bo thiet ke treo len 4 diem tua trong code
/// cua ho; doc ADR 0003 (SixosPwa/docs/adr/) truoc khi sua file nay.
/// </summary>
public class UbGateway : IPartnerGateway
{
    // Trang MVC cua doi tac (dat cookie DKOnline_auth). Khac BaseUrl cua Web API.
    private const string DuongDanDangKy = "/HeThong/HT_DangNhap/register";
    private const string DuongDanXacThuc = "/api/HT_DangNhap/XacThucMaXacNhan";
    private const string DuongDanDangNhap = "/HeThong/HT_DangNhap/login";
    private const string DuongDanChonChiNhanh = "/HeThong/HT_DangNhap/select-branch";
    private const string DuongDanDanhSachHoSo = "/QuanLy/QL_HoSoBenhNhan/LayDanhSachHoSoBenhNhan";
    private const string DuongDanChonHoSo = "/QuanLy/QL_HoSoBenhNhan/ThemIdXemThongTinBenhNhan";

    /// <summary>
    /// Nut benh nhan bam -> man tuong ung ben Ung Buou. Doi tac khac se co bang
    /// cua rieng ho trong ban cai cua ho; man hinh khong phai biet gi ve day.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ManTheoYDinh =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dat-goi-kham"] = "/QuanLy/QL_DangKyTheoGoi",
            ["lich-su-hen"] = "/QuanLy/QL_DangKyLichOnline_LichSuKhamBenh",
            ["ho-so-kham"] = "/QuanLy/QL_LichSuKhamBenh"
        };

    // 1 = Zalo, 2 = Email, 3 = SMS — khop switch trong RegisterAsync cua doi tac.
    private const int KenhEmail = 2;
    private const int KenhSms = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UbGateway> _logger;

    public UbGateway(IHttpClientFactory httpClientFactory, ILogger<UbGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string KieuApi => KieuApiDoiTac.UngBuou;

    public bool CoBanGiao => true;

    public async Task<TinhTrangTaiKhoan> TinhTrangTaiKhoanAsync(DoiTacApi cauHinh, string cccd, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.BaseUrl) || string.IsNullOrWhiteSpace(cccd))
        {
            return TinhTrangTaiKhoan.KhongXacDinh;
        }

        try
        {
            var client = TaoClient();
            var url = $"{CatDauGach(cauHinh.BaseUrl)}/api/TaiKhoan/cccd/{Uri.EscapeDataString(cccd)}";
            using var phanHoi = await client.GetAsync(url, ct);

            // Doi tac tra 404 kem message khi khong tim thay — day la ca binh thuong,
            // khong phai loi he thong.
            if (phanHoi.StatusCode == HttpStatusCode.NotFound) return TinhTrangTaiKhoan.ChuaCo;
            if (phanHoi.IsSuccessStatusCode) return TinhTrangTaiKhoan.DaCo;

            _logger.LogWarning("Tra tai khoan doi tac tra ve {StatusCode}", phanHoi.StatusCode);
            return TinhTrangTaiKhoan.KhongXacDinh;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Khong hoi duoc tinh trang tai khoan ben doi tac");
            return TinhTrangTaiKhoan.KhongXacDinh;
        }
    }

    public async Task<KetQuaMoTaiKhoan> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            return new KetQuaMoTaiKhoan(false, "Chưa cấu hình địa chỉ trang của cơ sở này", null);
        }

        // Doi tac tu chon kenh gui ma theo tham so xacthuc. Uu tien Email vi khong
        // ton SMS; khong co email thi rot ve SMS.
        var kenh = string.IsNullOrWhiteSpace(yeuCau.Email) ? KenhSms : KenhEmail;

        var than = new
        {
            SoCccd = yeuCau.Cccd,
            Email = yeuCau.Email,
            DienThoai = yeuCau.DienThoai,
            MatKhau = yeuCau.MatKhau,
            xacthuc = kenh
        };

        try
        {
            var client = TaoClient();
            var url = $"{CatDauGach(cauHinh.TrangChu)}{DuongDanDangKy}";
            using var noiDung = new StringContent(JsonSerializer.Serialize(than), Encoding.UTF8, "application/json");
            using var phanHoi = await client.PostAsync(url, noiDung, ct);
            var chuoi = await phanHoi.Content.ReadAsStringAsync(ct);

            if (!phanHoi.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mo tai khoan doi tac that bai {StatusCode}: {Than}", phanHoi.StatusCode, chuoi);
                return new KetQuaMoTaiKhoan(false, DocThongBao(chuoi) ?? "Không tạo được tài khoản tại cơ sở", null);
            }

            using var tep = JsonDocument.Parse(chuoi);
            var goc = tep.RootElement;

            // Doi tac tra { statusCode, message } ngay ca khi HTTP 200 — phai doc them.
            if (goc.TryGetProperty("statusCode", out var ma) && ma.TryGetInt32(out var so) && so != 200)
            {
                return new KetQuaMoTaiKhoan(false, DocThongBao(chuoi) ?? "Không tạo được tài khoản tại cơ sở", null);
            }

            // DIEM TUA #2 (ADR 0003): SendCode tra thang truong "code" trong than
            // phan hoi, va RegisterAsync tra ket qua do ra ngoai. Nho vay benh nhan
            // KHONG bao gio phai go ma cua doi tac.
            var maXacNhan = goc.TryGetProperty("code", out var c) ? c.GetString() : null;

            if (string.IsNullOrWhiteSpace(maXacNhan))
            {
                _logger.LogError("Doi tac khong tra truong code — diem tua so 2 cua ADR 0003 da doi");
                return new KetQuaMoTaiKhoan(false, "Cơ sở không trả về mã xác nhận, vui lòng liên hệ hỗ trợ", null);
            }

            return new KetQuaMoTaiKhoan(true, "Đã tạo tài khoản tại cơ sở", maXacNhan);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi khi mo tai khoan ben doi tac");
            return new KetQuaMoTaiKhoan(false, "Không kết nối được tới cơ sở, vui lòng thử lại", null);
        }
    }

    public async Task<KetQuaThaoTac> GuiMaLienKetAsync(DoiTacApi cauHinh, string cccd, string dienThoai, CancellationToken ct = default)
        => await GoiApiAsync(cauHinh, "/api/Auth/forgot-password/send-otp",
            new { Cccd = cccd, DienThoai = dienThoai },
            "Đã gửi mã xác thực", "Không gửi được mã xác thực", ct);

    public async Task<KetQuaThaoTac> DatLaiMatKhauAsync(DoiTacApi cauHinh, string cccd, string dienThoai, string ma, string matKhauMoi, CancellationToken ct = default)
        => await GoiApiAsync(cauHinh, "/api/Auth/forgot-password/reset",
            new { Cccd = cccd, DienThoai = dienThoai, Otp = ma, MatKhauMoi = matKhauMoi, XacNhanMatKhau = matKhauMoi },
            "Đã liên kết tài khoản", "Mã xác thực không đúng hoặc đã hết hạn", ct);

    /// <summary>
    /// Tu dang nhap o TANG MAY CHU (co CCCD + mat khau) de hoi danh sach ho so,
    /// roi lay ho so co SoCccd trung — tuc CHINH CHU. Khong tim thay thi tra
    /// null va de doi tac hien man chon ho so nhu binh thuong.
    ///
    /// Phai dung CookieContainer rieng: cookie nay la cua MAY CHU ta, khong lien
    /// quan gi toi cookie tren trinh duyet benh nhan.
    /// </summary>
    public async Task<long?> TimHoSoChinhChuAsync(DoiTacApi cauHinh, string cccd, string matKhau, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu) || string.IsNullOrWhiteSpace(matKhau))
        {
            return null;
        }

        var goc = CatDauGach(cauHinh.TrangChu);

        try
        {
            var tuiCookie = new CookieContainer();
            using var handler = new HttpClientHandler { CookieContainer = tuiCookie, UseCookies = true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };

            using var thanDangNhap = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = cccd,
                ["password"] = matKhau
            });

            using var phanHoiDangNhap = await client.PostAsync($"{goc}{DuongDanDangNhap}", thanDangNhap, ct);
            if (!phanHoiDangNhap.IsSuccessStatusCode)
            {
                _logger.LogInformation("Dang nhap tang may chu that bai, de doi tac tu hien man chon ho so");
                return null;
            }

            using var thanDanhSach = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["keyTimKiem"] = string.Empty,
                ["timTatCa"] = "false"
            });

            using var phanHoi = await client.PostAsync($"{goc}{DuongDanDanhSachHoSo}", thanDanhSach, ct);
            if (!phanHoi.IsSuccessStatusCode) return null;

            var chuoi = await phanHoi.Content.ReadAsStringAsync(ct);
            using var tep = JsonDocument.Parse(chuoi);

            if (!tep.RootElement.TryGetProperty("data", out var danhSach)
                || danhSach.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var hoSo in danhSach.EnumerateArray())
            {
                var soCccd = hoSo.TryGetProperty("soCccd", out var c1) ? c1.GetString()
                           : hoSo.TryGetProperty("SoCccd", out var c2) ? c2.GetString()
                           : null;

                if (!string.Equals(soCccd?.Trim(), cccd.Trim(), StringComparison.Ordinal)) continue;

                if (hoSo.TryGetProperty("idBenhNhan", out var i1) && i1.TryGetInt64(out var id1)) return id1;
                if (hoSo.TryGetProperty("IdBenhNhan", out var i2) && i2.TryGetInt64(out var id2)) return id2;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong tim duoc ho so chinh chu, de doi tac tu hien man chon");
            return null;
        }
    }

    public ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu)) return null;

        var goc = CatDauGach(cauHinh.TrangChu);

        // Dich cuoi theo dung nut benh nhan da bam. Khong nhan ra y dinh thi ve
        // trang chu — an toan hon la doan bua.
        var dichCuoi = goc;
        if (!string.IsNullOrWhiteSpace(yeuCau.YDinh)
            && ManTheoYDinh.TryGetValue(yeuCau.YDinh, out var man))
        {
            dichCuoi = goc + man;
        }

        var cacBuoc = new List<BuocBanGiao>();

        // Buoc 1 — dat cookie. Tai khoan vua mo thi dung ma xac nhan (doi tac vua
        // bat DaXacThuc vua dat cookie trong cung mot lan POST — diem tua so 3 va
        // so 4, ADR 0003). Nhung lan sau thi dang nhap thuan, khong OTP; LoginAsync
        // ben ho so chuoi thuan nen phai gui nguyen van mat khau (ADR 0005).
        if (!string.IsNullOrWhiteSpace(yeuCau.MaXacNhan))
        {
            cacBuoc.Add(new BuocBanGiao($"{goc}{DuongDanXacThuc}", new Dictionary<string, string>
            {
                ["cccd"] = yeuCau.Cccd,
                ["Email"] = yeuCau.Email ?? "",
                ["sdt"] = yeuCau.DienThoai,
                ["code"] = yeuCau.MaXacNhan
            }));
        }
        else if (!string.IsNullOrWhiteSpace(yeuCau.MatKhau))
        {
            cacBuoc.Add(new BuocBanGiao($"{goc}{DuongDanDangNhap}", new Dictionary<string, string>
            {
                ["username"] = yeuCau.Cccd,
                ["password"] = yeuCau.MatKhau
            }));
        }
        else
        {
            return null;
        }

        // Buoc 2 — chon chi nhanh. Middleware cua doi tac doi claim IdDT truoc
        // tien; thieu la da benh nhan ve man chon chi nhanh cua ho. Moi co so ben
        // ta ung voi dung mot chi nhanh ben ho nen chon giup duoc.
        if (cauHinh.MaChiNhanh is > 0)
        {
            cacBuoc.Add(new BuocBanGiao($"{goc}{DuongDanChonChiNhanh}", new Dictionary<string, string>
            {
                ["idDoiTac"] = cauHinh.MaChiNhanh.Value.ToString()
            }));
        }

        // Buoc 3 — chon ho so. CHI chon khi tim duoc ho so CHINH CHU (SoCccd trung
        // CCCD vua dang nhap). Ho so nguoi than thi khong dam dong vao: doan ho la
        // dang ky kham nham nguoi.
        if (yeuCau.IdHoSo is > 0)
        {
            cacBuoc.Add(new BuocBanGiao($"{goc}{DuongDanChonHoSo}", new Dictionary<string, string>
            {
                ["idBenhNhan"] = yeuCau.IdHoSo.Value.ToString()
            }));
        }

        return new ThongTinBanGiao(cacBuoc, dichCuoi);
    }

    private async Task<KetQuaThaoTac> GoiApiAsync(DoiTacApi cauHinh, string duongDan, object than,
        string thongBaoDat, string thongBaoHong, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.BaseUrl))
        {
            return new KetQuaThaoTac(false, "Chưa cấu hình địa chỉ API của cơ sở này");
        }

        try
        {
            var client = TaoClient();
            var url = $"{CatDauGach(cauHinh.BaseUrl)}{duongDan}";
            using var noiDung = new StringContent(JsonSerializer.Serialize(than), Encoding.UTF8, "application/json");
            using var phanHoi = await client.PostAsync(url, noiDung, ct);
            var chuoi = await phanHoi.Content.ReadAsStringAsync(ct);

            if (!phanHoi.IsSuccessStatusCode)
            {
                _logger.LogWarning("Goi {DuongDan} tra ve {StatusCode}: {Than}", duongDan, phanHoi.StatusCode, chuoi);
                return new KetQuaThaoTac(false, DocThongBao(chuoi) ?? thongBaoHong);
            }

            // API doi tac dung { success, message } cho nhom forgot-password.
            using var tep = JsonDocument.Parse(chuoi);
            if (tep.RootElement.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False)
            {
                return new KetQuaThaoTac(false, DocThongBao(chuoi) ?? thongBaoHong);
            }

            return new KetQuaThaoTac(true, thongBaoDat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi khi goi {DuongDan} ben doi tac", duongDan);
            return new KetQuaThaoTac(false, "Không kết nối được tới cơ sở, vui lòng thử lại");
        }
    }

    private HttpClient TaoClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(UbGateway));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string CatDauGach(string? duongDan)
        => (duongDan ?? string.Empty).TrimEnd('/');

    /// <summary>Lay message tieng Viet cua doi tac de hien lai cho benh nhan.</summary>
    private static string? DocThongBao(string chuoiJson)
    {
        try
        {
            using var tep = JsonDocument.Parse(chuoiJson);
            foreach (var ten in new[] { "message", "Message" })
            {
                if (tep.RootElement.TryGetProperty(ten, out var m) && m.ValueKind == JsonValueKind.String)
                {
                    return m.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Doi tac tra HTML trang loi thay vi JSON — bo qua, dung thong bao mac dinh.
        }

        return null;
    }
}
