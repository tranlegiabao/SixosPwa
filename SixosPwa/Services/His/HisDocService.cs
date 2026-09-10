using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services.His;

// ─────────────────────────────────────────────────────────────────────────────
// Hop dong tra ve
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Ba trang thai TACH BAC cua mot lan hoi HIS. 🔴 Khong duoc gop
/// <see cref="ChuaNoi"/> voi <see cref="KhongHoiDuoc"/> vao "khong co du lieu":
/// Dot 3 da can dung loi nay mot lan (<c>HoiMaDaCoNguoiNhan</c> coi <c>null</c> la
/// rong => 100% dong roi nham trang thai), va o *Lich kham cua toi* thi im lang
/// bi dich thanh "ban khong co hen" — benh nhan co hen tai kham that se tin la
/// minh khong co.
/// </summary>
public enum TrangThaiHoiHis
{
    /// <summary>Co so khong chay HisSoft, hoac duong API dang tat => AN han o.</summary>
    ChuaNoi,

    /// <summary>Goi duoc, HIS tra loi (ke ca tra ve tap rong — do la cau tra loi that).</summary>
    Xong,

    /// <summary>Da noi nhung HIS khong tra loi lucnay => nhan *chua hoi duoc co so*.</summary>
    KhongHoiDuoc
}

public sealed record KetQuaHoiHis<T>(TrangThaiHoiHis TrangThai, T? DuLieu, string? ThongDiep = null)
{
    public bool Xong => TrangThai == TrangThaiHoiHis.Xong;
}

/// <summary>
/// Mot ho so ben HIS. 🔴 <see cref="SoCCCD"/> va <see cref="DienThoai"/> da bi HIS
/// CHE BOT (chi con 4 so cuoi) — dung de HIEN cho nguoi dung nhan ra, KHONG dung
/// de so khop. Phep so khop CCCD da chay xong ben HIS va ket qua nam o
/// <see cref="CccdKhop"/>.
/// </summary>
public sealed class HoSoHis
{
    [JsonPropertyName("id")]        public long Id { get; set; }
    [JsonPropertyName("maBN")]      public string? MaBN { get; set; }
    [JsonPropertyName("tenBN")]     public string? TenBN { get; set; }
    [JsonPropertyName("ngaySinh")]  public DateTime? NgaySinh { get; set; }
    [JsonPropertyName("namSinh")]   public int? NamSinh { get; set; }
    [JsonPropertyName("gioiTinh")]  public string? GioiTinh { get; set; }
    [JsonPropertyName("tenGioiTinh")] public string? TenGioiTinh { get; set; }
    [JsonPropertyName("soCCCD")]    public string? SoCCCD { get; set; }
    [JsonPropertyName("cccdKhop")]  public bool CccdKhop { get; set; }
    [JsonPropertyName("dienThoai")] public string? DienThoai { get; set; }
    [JsonPropertyName("lanKhamCuoi")] public DateTime? LanKhamCuoi { get; set; }
    [JsonPropertyName("idBNCu")]    public long? IdBNCu { get; set; }
}

/// <summary>Mot hen tai kham sap toi. KHONG co gio — nguon ben HIS kieu <c>date</c>.</summary>
public sealed class LichHenHis
{
    [JsonPropertyName("maBN")]     public string? MaBN { get; set; }
    [JsonPropertyName("ngayHen")]  public DateTime NgayHen { get; set; }
    [JsonPropertyName("ngayTao")]  public DateTime? NgayTao { get; set; }
    [JsonPropertyName("tenKhoa")]  public string? TenKhoa { get; set; }
    [JsonPropertyName("tenBacSi")] public string? TenBacSi { get; set; }
    [JsonPropertyName("nguon")]    public string? Nguon { get; set; }
}

public interface IHisDocService
{
    /// <summary>
    /// Tra cuu ho so theo *Luat gop ho so* (bon o). <paramref name="cccd"/> co the
    /// rong — HIS van tra danh sach de cong dua ra cho benh nhan tu nhan (*Tang 2*).
    /// </summary>
    Task<KetQuaHoiHis<List<HoSoHis>>> TraCuuHoSoAsync(
        long idCoSo, string? cccd, string hoTen, DateTime ngaySinh, string gioiTinh,
        CancellationToken ct = default);

    /// <summary>Hen tai kham sap toi cua TAT CA ma cua mot ho so, mot cuoc goi.</summary>
    Task<KetQuaHoiHis<List<LichHenHis>>> LayLichHenAsync(
        long idCoSo, IReadOnlyCollection<string> maBN, CancellationToken ct = default);

    /// <summary>Co so nay co dang noi HIS khong — dung cho CAI VAN, khong goi ra ngoai.</summary>
    Task<bool> CoNoiHisAsync(long idCoSo, CancellationToken ct = default);
}

/// <summary>
/// Duong DOC cua cong sang HIS cua co so (Giai doan 2, Dot 4).
///
/// <para>
/// Bam khuon cuoc goi da chay that o <c>Areas/Admin/KiemTraHisController</c>:
/// <c>BaseAddress</c> lay tu <c>DM_DoiTacApi.BaseUrl</c>, header <c>X-API-Key</c> +
/// <c>X-Ma-CSKCB</c>, timeout 15 giay. Gom vao MOT cho de khoi moi man mot ban sao.
/// </para>
/// <para>
/// 🔴 Chi hoi HIS KHI NGUOI DUNG BAM (luu ho so / mo o lich), khong hoi moi lan mo
/// man: mot tai khoan N ho so thi mo man mot lan se thanh N cuoc goi sang may khach.
/// </para>
/// <para>
/// 🔴 Khong bao gio nem ngoai le ra ngoai. Moi truc trac (HIS chet, timeout, JSON
/// la) deu ve <see cref="TrangThaiHoiHis.KhongHoiDuoc"/> — man phai NOI duoc rang
/// no khong hoi duoc, chu khong duoc sap.
/// </para>
/// </summary>
public sealed class HisDocService : IHisDocService
{
    private static readonly TimeSpan ThoiGianCho = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions _json = new()
    {
        // HisSoft CAMEL-HOA model co kieu khi tra JSON (data.maBN, data.tenBN).
        // Bat khong phan biet hoa/thuong de ban HIS cu (neu co) khong lam ca luoi
        // trong ruot — dung loi da can o Dot 3, khong mot dau hieu nao bao hong.
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HisDocService> _logger;

    public HisDocService(ApplicationDbContext db,
                         IHttpClientFactory httpFactory,
                         ILogger<HisDocService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<bool> CoNoiHisAsync(long idCoSo, CancellationToken ct = default)
        => await LayCuaAsync(idCoSo, ct) is not null;

    public Task<KetQuaHoiHis<List<HoSoHis>>> TraCuuHoSoAsync(
        long idCoSo, string? cccd, string hoTen, DateTime ngaySinh, string gioiTinh,
        CancellationToken ct = default)
    {
        var duong = "SPWA_TraCuuHoSo"
                  + $"?hoTen={Uri.EscapeDataString(hoTen)}"
                  + $"&ngaySinh={ngaySinh:yyyy-MM-dd}"
                  + $"&gioiTinh={Uri.EscapeDataString(gioiTinh)}";

        // CCCD chi gui khi CO. HIS dung no de dat co cccdKhop tren tung dong, va
        // co do la ranh gioi *Tang 1* / *Tang 2*.
        if (!string.IsNullOrWhiteSpace(cccd))
            duong += $"&cccd={Uri.EscapeDataString(cccd.Trim())}";

        return GoiAsync<HoSoHis>(idCoSo, duong, "SPWA_TraCuuHoSo", ct);
    }

    public Task<KetQuaHoiHis<List<LichHenHis>>> LayLichHenAsync(
        long idCoSo, IReadOnlyCollection<string> maBN, CancellationToken ct = default)
    {
        var ma = (maBN ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Khong co ma nao = ho so chua noi. Do la cau tra loi CHAC CHAN, khong phai
        // "chua hoi duoc" — khoi ton mot cuoc goi de nhan ve tap rong.
        if (ma.Count == 0)
            return Task.FromResult(new KetQuaHoiHis<List<LichHenHis>>(
                TrangThaiHoiHis.Xong, new List<LichHenHis>()));

        var duong = "SPWA_LichHen?maBN=" + Uri.EscapeDataString(string.Join(',', ma));

        return GoiAsync<LichHenHis>(idCoSo, duong, "SPWA_LichHen", ct);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private sealed record CuaHis(Uri Goc, string Khoa, string MaCoSo);

    /// <summary>
    /// Cau hinh de goi sang HIS cua mot co so, hoac <c>null</c> khi co so nay
    /// khong di duong nay. Cac dieu kien deu la "chua noi", khong phai loi:
    /// khong co dong <c>DM_DoiTacApi</c> · <c>Active = 0</c> · khong cau hinh <c>BaseUrl</c>.
    /// Thieu <c>BaseUrl</c> hay <c>KhoaGoiHIS</c> khi dang mo cong thi ghi canh bao vi
    /// gan nhu chac chan la seed thieu.
    /// </summary>
    private async Task<CuaHis?> LayCuaAsync(long idCoSo, CancellationToken ct)
    {
        var cauHinh = await _db.DoiTacApis.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdCoSo == idCoSo, ct);

        if (cauHinh is null || !cauHinh.Active) return null;
        if (string.IsNullOrWhiteSpace(cauHinh.BaseUrl) && string.IsNullOrWhiteSpace(cauHinh.KhoaGoiHIS)) return null;

        if (string.IsNullOrWhiteSpace(cauHinh.BaseUrl) || string.IsNullOrWhiteSpace(cauHinh.KhoaGoiHIS))
        {
            _logger.LogWarning(
                "Co so {IdCoSo} mo cong ket noi HIS nhung thieu {Thieu} — duong doc sang HIS dang tat.",
                idCoSo,
                string.IsNullOrWhiteSpace(cauHinh.BaseUrl) ? "BaseUrl" : "KhoaGoiHIS");
            return null;
        }

        if (!Uri.TryCreate(cauHinh.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var goc))
        {
            _logger.LogWarning("Co so {IdCoSo} co BaseUrl khong hop le: {BaseUrl}", idCoSo, cauHinh.BaseUrl);
            return null;
        }

        var maCoSo = await _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.Id == idCoSo)
            .Select(x => x.MaCoSo)
            .FirstOrDefaultAsync(ct);

        return new CuaHis(goc, cauHinh.KhoaGoiHIS!, maCoSo ?? string.Empty);
    }

    /// <summary>Phong bi cua HIS: <c>{ statusCode, success, data: [...] }</c>.</summary>
    private sealed class PhongBiHis<T>
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("data")]    public List<T>? Data { get; set; }
    }

    private async Task<KetQuaHoiHis<List<T>>> GoiAsync<T>(
        long idCoSo, string duong, string ten, CancellationToken ct)
    {
        var cua = await LayCuaAsync(idCoSo, ct);
        if (cua is null)
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.ChuaNoi, null);

        try
        {
            var client = _httpFactory.CreateClient();
            client.BaseAddress = cua.Goc;
            client.Timeout = ThoiGianCho;
            client.DefaultRequestHeaders.Add("X-API-Key", cua.Khoa);
            client.DefaultRequestHeaders.Add("X-Ma-CSKCB", cua.MaCoSo);

            using var phanHoi = await client.GetAsync(duong, ct);

            if (!phanHoi.IsSuccessStatusCode)
            {
                _logger.LogWarning("{Ten} co so {IdCoSo}: HIS tra ma {Ma}",
                    ten, idCoSo, (int)phanHoi.StatusCode);

                return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.KhongHoiDuoc, null,
                    $"HIS trả mã {(int)phanHoi.StatusCode}");
            }

            var than = await phanHoi.Content.ReadAsStringAsync(ct);
            var boc = JsonSerializer.Deserialize<PhongBiHis<T>>(than, _json);

            if (boc is null || !boc.Success)
                return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.KhongHoiDuoc, null,
                    "HIS trả nội dung không đọc được");

            // data = null ma success = true thi coi la TAP RONG, khong phai loi:
            // "khong tim thay" la ket qua binh thuong cua ca hai cua nay.
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.Xong, boc.Data ?? new List<T>());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nguoi dung bo trang — khong phai HIS hong. Van ve KhongHoiDuoc vi
            // ta that su khong co du lieu, nhung khong ghi canh bao.
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.KhongHoiDuoc, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Ten} co so {IdCoSo}: khong goi duoc sang HIS", ten, idCoSo);
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.KhongHoiDuoc, null, ex.Message);
        }
    }
}
