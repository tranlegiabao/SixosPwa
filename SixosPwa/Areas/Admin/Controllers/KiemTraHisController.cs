using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Controllers;

/// <summary>
/// Kiểm tra sức khỏe đường nối sang HIS của một cơ sở có cấu hình HIS.
///
/// VÌ SAO CÓ MÀN NÀY: quyết định 2 Đợt 0 đòi "kiểm tra sức khỏe ngay lúc lưu
/// cấu hình" — cho cái bẫy ADR 0014 từng sập vì <c>BaseUrl</c> trỏ vào IP NỘI BỘ của
/// bệnh viện, SixosPwa chạy trên internet không bao giờ gọi tới được, và không
/// ai biết cho đến lúc bệnh nhân bấm nút.
///
/// Trước đợt A, <c>DM_DoiTacApi</c> KHÔNG CÓ MÀN LƯU NÀO — nó được cấu hình bằng
/// SQL trực tiếp. Từ đợt A các ô đó đã nằm trong màn Sửa cơ sở (cột KetNoi_*),
/// nhưng cửa kiểm tra này vẫn giữ vì nó bắt đúng ba thứ hay sai nhất NGAY SAU khi
/// lưu — theo thứ tự từ rẻ đến đắt:
///
///   1. Cấu hình: <c>KetNoi_Active = 1</c> chưa, <c>KetNoi_BaseUrlHIS</c> có hợp lệ
///      không. (Đợt A: bảng <c>DM_DoiTacApi</c> đã gộp thành cột của <c>DM_CSKCB</c>.)
///   2. Nối được tới HIS không (đây là chỗ ADR 0014 sập).
///   3. Khóa + mã cơ sở có đúng không — gọi <c>SPWA_TraCuuHoSo</c> KHÔNG kèm
///      tham số: HIS trả 400 (qua được cửa xác thực, thiếu tham số) = khóa ĐÚNG;
///      401 = khóa sai hoặc lệch mã cơ sở. Gọi một cửa thật nên nó kiểm được
///      đúng cái mà cuộc gọi thật sẽ gặp, mà vẫn không đọc dữ liệu bệnh nhân nào.
/// </summary>
public sealed class KiemTraHisController : AdminControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<KiemTraHisController> _logger;

    public KiemTraHisController(ApplicationDbContext db,
                                IHttpClientFactory httpFactory,
                                ILogger<KiemTraHisController> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public sealed record KetQuaKiemTra(bool Dat, string Buoc, string ThongDiep, long? Ms = null);

    /// <summary>
    /// GET /Admin/KiemTraHis/CoSo/{idCoSo}?khoa=...
    /// Khóa thô truyền theo tham số, KHÔNG lưu ở đâu — cổng chỉ giữ BĂM của khóa
    /// HIS cấp cho nó, còn khóa để gọi NGƯỢC vào HIS thì nằm bên HIS.
    /// </summary>
    [HttpGet("Admin/KiemTraHis/CoSo/{idCoSo:long}")]
    public async Task<IActionResult> CoSo(long idCoSo, [FromQuery] string? khoa, CancellationToken ct)
    {
        var ketQua = new List<KetQuaKiemTra>();

        // ── 1. Cấu hình ──────────────────────────────────────────────────────
        var coSo = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == idCoSo, ct);
        if (coSo is null)
            return Json(new { dat = false, ketQua = new[] { new KetQuaKiemTra(false, "Cau hinh", $"Khong co co so Id={idCoSo}") } });

        // Đợt A: bảng 1:1 DM_DoiTacApi đã gộp thẳng vào DM_CSKCB (cột KetNoi_*),
        // nên không còn "chưa có dòng nào" — chỉ còn "chưa điền cấu hình".
        if (!coSo.KetNoi_Active)
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", "DM_CSKCB.KetNoi_Active = 0 — cua dang tat"));

        if (string.IsNullOrWhiteSpace(coSo.KetNoi_BaseUrlHIS))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", "KetNoi_BaseUrlHIS rong — khong biet goi di dau"));
            return Json(new { dat = false, coSo = coSo.TenCoSo, ketQua });
        }

        if (!Uri.TryCreate(coSo.KetNoi_BaseUrlHIS.TrimEnd('/') + "/", UriKind.Absolute, out var goc))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", $"KetNoi_BaseUrlHIS khong hop le: {coSo.KetNoi_BaseUrlHIS}"));
            return Json(new { dat = false, coSo = coSo.TenCoSo, ketQua });
        }

        ketQua.Add(new KetQuaKiemTra(true, "Cau hinh", $"BaseUrl={goc}"));

        // Cảnh báo SỚM cho đúng cái bẫy của ADR 0014: địa chỉ nội bộ thì máy chủ
        // cổng (chạy trên internet) không bao giờ gọi tới được.
        if (goc.IsLoopback || LaDiaChiNoiBo(goc.Host))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Dia chi",
                $"'{goc.Host}' la dia chi NOI BO — may chu cong khong goi toi duoc tu internet (bay cua ADR 0014)"));
        }

        // ── 2. Nối được tới HIS không + 3. Khóa có đúng không ────────────────
        if (string.IsNullOrWhiteSpace(khoa))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Khoa", "Chua truyen ?khoa= nen bo qua buoc goi thu"));
            return Json(new { dat = ketQua.All(x => x.Dat), coSo = coSo.TenCoSo, ketQua });
        }

        var dongHo = Stopwatch.StartNew();
        try
        {
            var client = _httpFactory.CreateClient();
            client.BaseAddress = goc;
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("X-API-Key", khoa);
            client.DefaultRequestHeaders.Add("X-Ma-CSKCB", coSo.MaCoSo ?? string.Empty);

            // Gọi KHÔNG kèm tham số: qua được cửa xác thực thì HIS trả 400
            // (thiếu tham số). Không đọc dữ liệu bệnh nhân nào.
            using var phanHoi = await client.GetAsync("SPWA_TraCuuHoSo", ct);
            dongHo.Stop();

            int ma = (int)phanHoi.StatusCode;

            ketQua.Add(new KetQuaKiemTra(true, "Ket noi", $"Goi duoc toi HIS ({ma})", dongHo.ElapsedMilliseconds));

            ketQua.Add(ma switch
            {
                400 => new KetQuaKiemTra(true, "Khoa", "Khoa va ma co so DUNG (HIS tra 400 vi co y khong gui tham so)"),
                401 => new KetQuaKiemTra(false, "Khoa",
                        $"HIS tu choi (401). Khoa sai, hoac X-Ma-CSKCB '{coSo.MaCoSo}' khong khop ThongTinDoanhNghiep.MaCSKCB ben HIS, "
                      + "hoac SpwaKhoaNhanBam ben HIS con rong (cua vao dang dong)"),
                404 => new KetQuaKiemTra(false, "Khoa", "HIS tra 404 — ban HIS o dia chi nay chua co cua SPWA_TraCuuHoSo (chua trien khai Dot 3?)"),
                _   => new KetQuaKiemTra(false, "Khoa", $"HIS tra ma la: {ma}")
            });
        }
        catch (TaskCanceledException)
        {
            dongHo.Stop();
            ketQua.Add(new KetQuaKiemTra(false, "Ket noi",
                $"Het thoi gian cho ({dongHo.ElapsedMilliseconds} ms) — HIS khong tra loi. Kiem dia chi co goi duoc tu may chu cong khong",
                dongHo.ElapsedMilliseconds));
        }
        catch (HttpRequestException ex)
        {
            dongHo.Stop();
            _logger.LogWarning(ex, "KiemTraHis: khong noi duoc toi {BaseUrl}", goc);
            ketQua.Add(new KetQuaKiemTra(false, "Ket noi", $"Khong noi duoc toi HIS: {ex.Message}", dongHo.ElapsedMilliseconds));
        }

        return Json(new { dat = ketQua.All(x => x.Dat), coSo = coSo.TenCoSo, maCoSo = coSo.MaCoSo, ketQua });
    }

    /// <summary>Dải địa chỉ riêng (RFC 1918) + .local — những địa chỉ chỉ gọi được trong mạng nội bộ.</summary>
    private static bool LaDiaChiNoiBo(string host)
    {
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;

        if (!System.Net.IPAddress.TryParse(host, out var ip)) return false;

        var b = ip.GetAddressBytes();
        if (b.Length != 4) return false;

        return b[0] == 10                                  // 10.0.0.0/8
            || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12
            || (b[0] == 169 && b[1] == 254);               // link-local
    }
}
