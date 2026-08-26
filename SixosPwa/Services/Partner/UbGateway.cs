using System.Net.Http;
using System.Text;
using System.Text.Json;
using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Ban cai cho Benh vien Ung Buou (DangKyOnlineUB).
///
/// SixosPwa dung lai BO MAN cua ho (Dang nhap / Dang ky / Quen mat khau) va goi
/// dung cac cua DA CO SAN tren TrangChu cong khai. Doi tac la NGUON SU THAT cua
/// mat khau — ta khong tu phan xu dung/sai bao gio.
///
/// KHONG con cot BaseUrl: dia chi do la IP noi bo benh vien (10.85.9.34), goi tu
/// internet la "connection refused". Doc ADR 0014 (THAY ADR 0003) truoc khi sua.
/// </summary>
public class UbGateway : IPartnerGateway
{
    // Trang MVC cua doi tac (dat cookie DKOnline_auth). Tat ca deu la
    // [AllowAnonymous] va deu nam duoi TrangChu cong khai.
    private const string DuongDanDangNhap = "/HeThong/HT_DangNhap/login";
    private const string DuongDanDangKy = "/HeThong/HT_DangNhap/register";
    private const string DuongDanQuenMatKhau = "/HeThong/HT_QuenMatKhau/QuenMatKhau";
    private const string DuongDanChiNhanh = "/HeThong/HT_DangNhap/branches";

    /// <summary>
    /// Ban XacThucMaXacNhan o Controllers/ — chi tra JSON, KHONG dat cookie.
    ///
    /// ⚠️ Doi tac co HAI action trung ten. Ban o Area/API/Controllers/ vua dat
    /// cookie vua Redirect; goi ban do o tang may chu la vo nghia (cookie roi vao
    /// HttpClient cua ta chu khong phai trinh duyet benh nhan) va con TIEU THU
    /// mat ma. Ta can biet ma dung/sai de bao ngay trong app, nen dung ban nay.
    /// </summary>
    private const string DuongDanXacThuc = "/HeThong/HT_DangNhap/XacThucMaXacNhan";

    /// <summary>
    /// Nut benh nhan bam -> man tuong ung ben Ung Buou. Doi tac khac se co bang
    /// cua rieng ho trong ban cai cua ho; man hinh khong phai biet gi ve day.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ManTheoYDinh =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["dat-goi-kham"] = "/QuanLy/QL_DangKyTheoGoi",
            ["lich-su-hen"] = "/QuanLy/QL_DangKyLichOnline_LichSuKhamBenh",
            ["ho-so-kham"] = "/QuanLy/QL_LichSuKhamBenh",
            // Bam "Ho so benh nhan" o menu 3 gach khi da dang nhap — man chon ho so.
            ["ho-so"] = "/QuanLy/QL_HoSoBenhNhan"
        };

    /// <summary>
    /// Cac kenh gui ma cua doi tac, khop switch trong RegisterAsync cua ho:
    /// 1 = Zalo, 2 = Email, 3 = SMS, 4 = chi sinh ma khong gui tin.
    ///
    /// Chi mo 1 va 3 cho benh nhan chon. KHONG mo kenh 2 (Email): cuoi SendCode
    /// ben ho chi tra thongTinBenhNhan khi SoDienThoai khac rong roi gan MaXacNhan
    /// vo dieu kien, nen nhanh Email nem NullReferenceException va tra "Gui ma xac
    /// thuc that bai". KHONG mo kenh 4: no khong gui tin nen man nhap ma 4 o cua ho
    /// mat ly do ton tai.
    ///
    /// Cai gia da biet cua ca hai kenh mo: moi lan dang ky la mot tin nhan that.
    /// </summary>
    public const int KenhZalo = 1;
    public const int KenhSms = 3;

    /// <summary>Kenh benh nhan chon; khong nhan ra thi ve SMS cho chac.</summary>
    private static int ChonKenh(int kenh) => kenh == KenhZalo ? KenhZalo : KenhSms;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UbGateway> _logger;

    public UbGateway(IHttpClientFactory httpClientFactory, ILogger<UbGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string KieuApi => KieuApiDoiTac.UngBuou;

    public bool CoBanGiao => true;

    public bool DungManDoiTac => true;

    public async Task<KetQuaThaoTac> DangNhapAsync(DoiTacApi cauHinh, string cccd, string matKhau, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            return new KetQuaThaoTac(false, "Chưa cấu hình địa chỉ trang của cơ sở này");
        }

        // Doi tac nhan form-urlencoded (tham so roi cua action, khong phai [FromBody]).
        return await GoiFormAsync(cauHinh, DuongDanDangNhap,
            new Dictionary<string, string> { ["username"] = cccd, ["password"] = matKhau },
            "Đăng nhập thành công", "Thông tin đăng nhập không chính xác", ct);
    }

    public async Task<KetQuaThaoTac> MoTaiKhoanAsync(DoiTacApi cauHinh, YeuCauMoTaiKhoan yeuCau, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            return new KetQuaThaoTac(false, "Chưa cấu hình địa chỉ trang của cơ sở này");
        }

        // register nhan [FromBody] JSON — khac ba cua kia.
        var than = new
        {
            SoCccd = yeuCau.Cccd,
            Email = yeuCau.Email,
            DienThoai = yeuCau.DienThoai,
            MatKhau = yeuCau.MatKhau,
            xacthuc = ChonKenh(yeuCau.Kenh)
        };

        return await GoiJsonAsync(cauHinh, DuongDanDangKy, than,
            "Đã gửi mã xác thực", "Không tạo được tài khoản tại cơ sở", ct);
    }

    public async Task<KetQuaThaoTac> XacThucMaAsync(DoiTacApi cauHinh, string cccd, string? email, string dienThoai, string ma, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            return new KetQuaThaoTac(false, "Chưa cấu hình địa chỉ trang của cơ sở này");
        }

        return await GoiFormAsync(cauHinh, DuongDanXacThuc,
            new Dictionary<string, string>
            {
                ["cccd"] = cccd,
                ["Email"] = email ?? string.Empty,
                ["sdt"] = dienThoai,
                ["code"] = ma
            },
            "Xác thực tài khoản thành công", "Mã xác thực không đúng hoặc đã hết hạn", ct);
    }

    public async Task<KetQuaThaoTac> QuenMatKhauAsync(DoiTacApi cauHinh, string cccd, string emailHoacSdt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            return new KetQuaThaoTac(false, "Chưa cấu hình địa chỉ trang của cơ sở này");
        }

        // Doi tac tach san Email va SDT thanh hai truong, tu quyet gui qua kenh nao.
        var laEmail = emailHoacSdt.Contains('@');
        var than = new
        {
            CCCD = cccd,
            Email = laEmail ? emailHoacSdt : null,
            SDT = laEmail ? null : emailHoacSdt
        };

        return await GoiJsonAsync(cauHinh, DuongDanQuenMatKhau, than,
            "Đã gửi đường dẫn đặt lại mật khẩu", "Không gửi được đường dẫn đặt lại mật khẩu", ct);
    }

    public async Task<IReadOnlyList<ChiNhanhDoiTac>> LayChiNhanhAsync(DoiTacApi cauHinh, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            return Array.Empty<ChiNhanhDoiTac>();
        }

        try
        {
            var client = TaoClient();
            var url = $"{CatDauGach(cauHinh.TrangChu)}{DuongDanChiNhanh}";
            using var phanHoi = await client.GetAsync(url, ct);
            if (!phanHoi.IsSuccessStatusCode)
            {
                _logger.LogWarning("Lay chi nhanh doi tac tra ve {StatusCode}", phanHoi.StatusCode);
                return Array.Empty<ChiNhanhDoiTac>();
            }

            using var tep = JsonDocument.Parse(await phanHoi.Content.ReadAsStringAsync(ct));
            if (!tep.RootElement.TryGetProperty("data", out var mang) || mang.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ChiNhanhDoiTac>();
            }

            var ds = new List<ChiNhanhDoiTac>();
            foreach (var o in mang.EnumerateArray())
            {
                var ten = Chuoi(o, "tenDoiTac");
                if (string.IsNullOrWhiteSpace(ten)) continue;

                ds.Add(new ChiNhanhDoiTac(ten, Chuoi(o, "diaChi"), Chuoi(o, "hotline"), Chuoi(o, "thoiGianLamViec")));
            }

            return ds;
        }
        catch (Exception ex)
        {
            // Khoi nay chi de TRANG TRI man dang nhap — hong thi bo qua, tuyet doi
            // khong duoc chan benh nhan dang nhap.
            _logger.LogWarning(ex, "Khong lay duoc danh sach chi nhanh doi tac");
            return Array.Empty<ChiNhanhDoiTac>();
        }
    }

    private static string? Chuoi(JsonElement o, string ten)
        => o.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public ThongTinBanGiao? DungThongTinBanGiao(DoiTacApi cauHinh, YeuCauBanGiao yeuCau)
    {
        if (string.IsNullOrWhiteSpace(cauHinh.TrangChu)) return null;

        // Ban giao luon di duong dang nhap thuan: den day thi mat khau da duoc
        // CHINH doi tac xac nhan la dung (DangNhapAsync), hoac benh nhan vua tu
        // dat no o man Dang ky. LoginAsync ben ho so chuoi thuan nen phai gui
        // nguyen van mat khau (ADR 0005).
        if (string.IsNullOrWhiteSpace(yeuCau.MatKhau)) return null;

        var goc = CatDauGach(cauHinh.TrangChu);

        // Dich cuoi theo dung nut benh nhan da bam. Khong nhan ra y dinh thi ve
        // trang chu — an toan hon la doan bua.
        var dichCuoi = goc;
        if (!string.IsNullOrWhiteSpace(yeuCau.YDinh)
            && ManTheoYDinh.TryGetValue(yeuCau.YDinh, out var man))
        {
            dichCuoi = goc + man;
        }

        // CHI MOT BUOC. Cookie DKOnline_auth cua ho la SameSite=Lax: Lax cho phep
        // NHAN Set-Cookie, nhung chi GUI cookie kem dieu huong top-level bang GET.
        // POST lien site khong mang cookie, nen chuoi nhieu buoc (chon chi nhanh,
        // chon ho so) KHONG THE chay — da do bang Playwright, xem ADR 0003.
        var cacBuoc = new List<BuocBanGiao>
        {
            new($"{goc}{DuongDanDangNhap}", new Dictionary<string, string>
            {
                ["username"] = yeuCau.Cccd,
                ["password"] = yeuCau.MatKhau
            })
        };

        return new ThongTinBanGiao(cacBuoc, dichCuoi);
    }

    // ------------------------------------------------------------------
    //  Goi doi tac
    // ------------------------------------------------------------------

    private async Task<KetQuaThaoTac> GoiJsonAsync(DoiTacApi cauHinh, string duongDan, object than,
        string thongBaoDat, string thongBaoHong, CancellationToken ct)
    {
        using var noiDung = new StringContent(JsonSerializer.Serialize(than), Encoding.UTF8, "application/json");
        return await GuiAsync(cauHinh, duongDan, noiDung, thongBaoDat, thongBaoHong, ct);
    }

    private async Task<KetQuaThaoTac> GoiFormAsync(DoiTacApi cauHinh, string duongDan, IDictionary<string, string> truong,
        string thongBaoDat, string thongBaoHong, CancellationToken ct)
    {
        using var noiDung = new FormUrlEncodedContent(truong);
        return await GuiAsync(cauHinh, duongDan, noiDung, thongBaoDat, thongBaoHong, ct);
    }

    private async Task<KetQuaThaoTac> GuiAsync(DoiTacApi cauHinh, string duongDan, HttpContent noiDung,
        string thongBaoDat, string thongBaoHong, CancellationToken ct)
    {
        try
        {
            var client = TaoClient();
            var url = $"{CatDauGach(cauHinh.TrangChu)}{duongDan}";
            using var phanHoi = await client.PostAsync(url, noiDung, ct);
            var chuoi = await phanHoi.Content.ReadAsStringAsync(ct);

            if (!phanHoi.IsSuccessStatusCode)
            {
                _logger.LogWarning("Goi {DuongDan} tra ve {StatusCode}: {Than}", duongDan, phanHoi.StatusCode, chuoi);
                return new KetQuaThaoTac(false, DocThongBao(chuoi) ?? thongBaoHong);
            }

            // Doi tac tra { statusCode, message } ngay ca khi HTTP 200 — phai doc them.
            if (DocMaTrangThai(chuoi) is int ma && ma != 200)
            {
                return new KetQuaThaoTac(false, DocThongBao(chuoi) ?? thongBaoHong);
            }

            return new KetQuaThaoTac(true, DocThongBao(chuoi) ?? thongBaoDat);
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

        // 🔴 BAT BUOC. Doi tac da them nhanh non-AJAX (commit f653f96): thieu header
        // nay thi login tra 302 Redirect thay vi JSON, va ta se doc nham thanh
        // "sai mat khau". Header do jQuery $.ajax tu gan cho MOI request AJAX, nen
        // gan no o day = tu xung "toi la lenh goi AJAX, tra JSON cho toi".
        //
        // ⚠️ Buoc BAN GIAO thi NGUOC LAI — form POST top-level KHONG duoc co header
        // nay, chinh nho vay doi tac moi Redirect thay vi hien JSON tran cho benh nhan.
        client.DefaultRequestHeaders.Remove("X-Requested-With");
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        return client;
    }

    private static string CatDauGach(string? duongDan)
        => (duongDan ?? string.Empty).TrimEnd('/');

    /// <summary>Lay statusCode trong than phan hoi; null neu doi tac khong tra truong do.</summary>
    private static int? DocMaTrangThai(string chuoiJson)
    {
        try
        {
            using var tep = JsonDocument.Parse(chuoiJson);
            if (tep.RootElement.ValueKind != JsonValueKind.Object) return null;

            foreach (var ten in new[] { "statusCode", "StatusCode" })
            {
                if (tep.RootElement.TryGetProperty(ten, out var ma) && ma.TryGetInt32(out var so))
                {
                    return so;
                }
            }
        }
        catch (JsonException)
        {
            // Doi tac tra HTML trang loi thay vi JSON — coi nhu khong ro, de
            // GuiAsync xu theo ma HTTP.
        }

        return null;
    }

    /// <summary>Lay message tieng Viet cua doi tac de hien lai cho benh nhan.</summary>
    private static string? DocThongBao(string chuoiJson)
    {
        try
        {
            using var tep = JsonDocument.Parse(chuoiJson);
            if (tep.RootElement.ValueKind != JsonValueKind.Object) return null;

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
