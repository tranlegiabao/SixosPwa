using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Controllers;

/// <summary>
/// Kiem tra suc khoe duong noi sang HIS cua mot co so co cau hinh HIS.
///
/// VI SAO CO MAN NAY: quyet dinh 2 Dot 0 doi "kiem tra suc khoe ngay luc luu
/// cau hinh" — cho ma ADR 0014 tung sap vi <c>BaseUrl</c> tro vao IP NOI BO cua
/// benh vien, SixosPwa chay tren internet khong bao gio goi toi duoc, va khong
/// ai biet cho den luc benh nhan bam nut.
///
/// Nhung <c>DM_DoiTacApi</c> HIEN KHONG CO MAN LUU NAO — no duoc cau hinh bang
/// SQL truc tiep (da ra soat: khong mot cho nao trong app ghi vao bang do). Nen
/// khong co su kien "luu" de moc vao. Thay vi dung ca mot man CRUD nam ngoai
/// pham vi dot nay, dat dung mot cua kiem tra goi duoc NGAY SAU khi chay seed —
/// bat dung ba thu hay sai nhat, theo thu tu tu re den dat:
///
///   1. Cau hinh: co dong <c>DM_DoiTacApi</c> chua, <c>Active = 1</c> chua,
///      <c>BaseUrl</c> co hop le khong.
///   2. Noi duoc toi HIS khong (day la cho ADR 0014 sap).
///   3. Khoa + ma co so co dung khong — goi <c>SPWA_TraCuuHoSo</c> KHONG kem
///      tham so: HIS tra 400 (qua duoc cua xac thuc, thieu tham so) = khoa DUNG;
///      401 = khoa sai hoac lech ma co so. Goi mot cua that nen no kiem duoc
///      dung cai ma cuoc goi that se gap, ma van khong doc du lieu benh nhan nao.
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
    /// Khoa tho truyen theo tham so, KHONG luu o dau — cong chi giu BAM cua khoa
    /// HIS cap cho no, con khoa de goi NGUOC vao HIS thi nam ben HIS.
    /// </summary>
    [HttpGet("Admin/KiemTraHis/CoSo/{idCoSo:long}")]
    public async Task<IActionResult> CoSo(long idCoSo, [FromQuery] string? khoa, CancellationToken ct)
    {
        var ketQua = new List<KetQuaKiemTra>();

        // ── 1. Cau hinh ──────────────────────────────────────────────────────
        var coSo = await _db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == idCoSo, ct);
        if (coSo is null)
            return Json(new { dat = false, ketQua = new[] { new KetQuaKiemTra(false, "Cau hinh", $"Khong co co so Id={idCoSo}") } });

        var cauHinh = await _db.DoiTacApis.AsNoTracking().FirstOrDefaultAsync(x => x.IdCoSo == idCoSo, ct);

        if (cauHinh is null)
        {
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", "Chua co dong nao trong DM_DoiTacApi cho co so nay"));
            return Json(new { dat = false, coSo = coSo.TenCoSo, ketQua });
        }

        if (!cauHinh.Active)
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", "DM_DoiTacApi.Active = 0 — cua dang tat"));

        if (string.IsNullOrWhiteSpace(cauHinh.BaseUrl))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", "BaseUrl rong — khong biet goi di dau"));
            return Json(new { dat = false, coSo = coSo.TenCoSo, ketQua });
        }

        if (!Uri.TryCreate(cauHinh.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var goc))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Cau hinh", $"BaseUrl khong hop le: {cauHinh.BaseUrl}"));
            return Json(new { dat = false, coSo = coSo.TenCoSo, ketQua });
        }

        ketQua.Add(new KetQuaKiemTra(true, "Cau hinh", $"BaseUrl={goc}"));

        // Canh bao SOM cho dung cai bay cua ADR 0014: dia chi noi bo thi may chu
        // cong (chay tren internet) khong bao gio goi toi duoc.
        if (goc.IsLoopback || LaDiaChiNoiBo(goc.Host))
        {
            ketQua.Add(new KetQuaKiemTra(false, "Dia chi",
                $"'{goc.Host}' la dia chi NOI BO — may chu cong khong goi toi duoc tu internet (bay cua ADR 0014)"));
        }

        // ── 2. Noi duoc toi HIS khong + 3. Khoa co dung khong ────────────────
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

            // Goi KHONG kem tham so: qua duoc cua xac thuc thi HIS tra 400
            // (thieu tham so). Khong doc du lieu benh nhan nao.
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

    /// <summary>Dai dia chi rieng (RFC 1918) + .local — nhung dia chi chi goi duoc trong mang noi bo.</summary>
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
