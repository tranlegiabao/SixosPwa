using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services.His;

/// <summary>
/// Ba trạng thái TÁCH BẠCH của một lần hỏi HIS. 🔴 Không được gộp
/// <see cref="ChuaNoi"/> với <see cref="KhongHoiDuoc"/> vào "không có dữ liệu":
/// Đợt 3 đã cán đúng lỗi này một lần (<c>HoiMaDaCoNguoiNhan</c> coi <c>null</c> là
/// rỗng => 100% dòng rơi nhầm trạng thái), và ở *Lịch khám của tôi* thì im lặng
/// bị dịch thành "bạn không có hẹn" — bệnh nhân có hẹn tái khám thật sẽ tin là
/// mình không có.
/// </summary>
public enum TrangThaiHoiHis
{
    /// <summary>Cơ sở không chạy HisSoft, hoặc đường API đang tắt => ẨN hẳn ở.</summary>
    ChuaNoi,

    /// <summary>Gọi được, HIS trả lời (kể cả trả về tập rỗng — đó là câu trả lời thật).</summary>
    Xong,

    /// <summary>Đã nối nhưng HIS không trả lời lúc này => nhấn *chưa hỏi được cơ sở*.</summary>
    KhongHoiDuoc
}

public sealed record KetQuaHoiHis<T>(TrangThaiHoiHis TrangThai, T? DuLieu, string? ThongDiep = null)
{
    public bool Xong => TrangThai == TrangThaiHoiHis.Xong;
}

/// <summary>
/// Một hồ sơ bên HIS. 🔴 <see cref="SoCCCD"/> và <see cref="DienThoai"/> đã bị HIS
/// CHE BỚT (chỉ còn 4 số cuối) — dùng để HIỆN cho người dùng nhận ra, KHÔNG dùng
/// để so khớp. Phép so khớp CCCD đã chạy xong bên HIS và kết quả nằm ở
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

/// <summary>Một hẹn tái khám sắp tới. KHÔNG có giờ — nguồn bên HIS kiểu <c>date</c>.</summary>
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
    /// Tra cứu hồ sơ theo *Luật gộp hồ sơ* (bốn ô). <paramref name="cccd"/> có thể
    /// rỗng — HIS vẫn trả danh sách để cổng đưa ra cho bệnh nhân tự nhận (*Tầng 2*).
    /// </summary>
    Task<KetQuaHoiHis<List<HoSoHis>>> TraCuuHoSoAsync(
        long idCoSo, string? cccd, string hoTen, DateTime ngaySinh, string gioiTinh,
        CancellationToken ct = default);

    /// <summary>Hẹn tái khám sắp tới của TẤT CẢ mã của một hồ sơ, một cuộc gọi.</summary>
    Task<KetQuaHoiHis<List<LichHenHis>>> LayLichHenAsync(
        long idCoSo, IReadOnlyCollection<string> maBN, CancellationToken ct = default);

    /// <summary>Cơ sở này có đang nối HIS không — dùng cho CÀI VẶN, không gọi ra ngoài.</summary>
    Task<bool> CoNoiHisAsync(long idCoSo, CancellationToken ct = default);
}

/// <summary>
/// Đường ĐỌC của cổng sang HIS của cơ sở (Giai đoạn 2, Đợt 4).
///
/// <para>
/// Bám khuôn cuộc gọi đã chạy thật ở <c>Areas/Admin/KiemTraHisController</c>:
/// <c>BaseAddress</c> lấy từ <c>DM_DoiTacApi.BaseUrl</c>, header <c>X-API-Key</c> +
/// <c>X-Ma-CSKCB</c>, timeout 15 giây. Gom vào MỘT chỗ để khỏi mỗi màn một bản sao.
/// </para>
/// <para>
/// 🔴 Chỉ hỏi HIS KHI NGƯỜI DÙNG BẤM (lưu hồ sơ / mở ở lịch), không hỏi mỗi lần mở
/// màn: một tài khoản N hồ sơ thì mở màn một lần sẽ thành N cuộc gọi sang máy khách.
/// </para>
/// <para>
/// 🔴 Không bao giờ ném ngoại lệ ra ngoài. Mọi trục trặc (HIS chết, timeout, JSON
/// lạ) đều về <see cref="TrangThaiHoiHis.KhongHoiDuoc"/> — màn phải NÓI được rằng
/// nó không hỏi được, chứ không được sập.
/// </para>
/// </summary>
public sealed class HisDocService : IHisDocService
{
    private static readonly TimeSpan ThoiGianCho = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions _json = new()
    {
        // HisSoft CAMEL-HÓA model có kiểu khi trả JSON (data.maBN, data.tenBN).
        // Bật không phân biệt hoa/thường để bản HIS cũ (nếu có) không làm cả lưới
        // trong ruột — đúng lỗi đã cán ở Đợt 3, không một dấu hiệu nào báo hỏng.
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

        // CCCD chỉ gửi khi CÓ và KHÔNG PHẢI trường hợp không có CCCD (11 hoặc 12 số 1).
        // HIS dùng nó để đặt cờ cccdKhop trên từng dòng, và cờ đó là ranh giới *Tầng 1* / *Tầng 2*.
        var cccdTrim = cccd?.Trim();
        var laCccdKhongCo = cccdTrim is "11111111111" or "111111111111";

        if (!string.IsNullOrWhiteSpace(cccdTrim) && !laCccdKhongCo)
            duong += $"&cccd={Uri.EscapeDataString(cccdTrim)}";

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

        // Không có mã nào = hồ sơ chưa nối. Đó là câu trả lời CHẮC CHẮN, không phải
        // "chưa hỏi được" — khỏi tốn một cuộc gọi để nhận về tập rỗng.
        if (ma.Count == 0)
            return Task.FromResult(new KetQuaHoiHis<List<LichHenHis>>(
                TrangThaiHoiHis.Xong, new List<LichHenHis>()));

        var duong = "SPWA_LichHen?maBN=" + Uri.EscapeDataString(string.Join(',', ma));

        return GoiAsync<LichHenHis>(idCoSo, duong, "SPWA_LichHen", ct);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private sealed record CuaHis(Uri Goc, string Khoa, string MaCoSo);

    /// <summary>
    /// Cấu hình để gọi sang HIS của một cơ sở, hoặc <c>null</c> khi cơ sở này
    /// không đi đường này. Các điều kiện đều là "chưa nối", không phải lỗi:
    /// <c>KetNoi_Active = 0</c> · không cấu hình <c>KetNoi_BaseUrlHIS</c>.
    /// Thiếu <c>KetNoi_BaseUrlHIS</c> hay <c>KetNoi_KhoaGoiHIS</c> khi đang mở cổng thì
    /// ghi cảnh báo vì gần như chắc chắn là seed thiếu.
    ///
    /// <para>
    /// 🔴 <b>Đọc bằng CÂU SQL RIÊNG (ADO thuần), cố ý không qua EF.</b> Bảng
    /// <c>DM_DoiTacApi</c> đã bị xóa, cấu hình dồn vào <c>DM_CSKCB</c>; trong đó
    /// <c>KetNoi_KhoaGoiHIS</c> là <b>cột bí mật</b> — KHÔNG được khai trong thực thể EF
    /// <see cref="Models.DMCSKCB"/> vì có 59 chỗ đọc <c>DM_CSKCB</c> qua EF và trang công
    /// khai nạp TRỌN thực thể mọi cơ sở. Đọc SQL riêng là sửa 3 chỗ thay vì 59.
    /// Chỉ SELECT đúng cột cần, không <c>SELECT *</c>.
    /// </para>
    /// </summary>
    private async Task<CuaHis?> LayCuaAsync(long idCoSo, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        string maCoSo;
        string? baseUrl;
        string? khoaGoiHis;
        bool active;

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT MaCoSo, KetNoi_BaseUrlHIS, KetNoi_KhoaGoiHIS, KetNoi_Active
FROM dbo.DM_CSKCB
WHERE ID = @idCoSo;";

            var p = cmd.CreateParameter();
            p.ParameterName = "@idCoSo";
            p.DbType = System.Data.DbType.Int64;
            p.Value = idCoSo;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            maCoSo     = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            baseUrl    = reader.IsDBNull(1) ? null : reader.GetString(1);
            khoaGoiHis = reader.IsDBNull(2) ? null : reader.GetString(2);
            active     = !reader.IsDBNull(3) && reader.GetBoolean(3);
        }

        if (!active) return null;
        if (string.IsNullOrWhiteSpace(baseUrl) && string.IsNullOrWhiteSpace(khoaGoiHis)) return null;

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(khoaGoiHis))
        {
            _logger.LogWarning(
                "Co so {IdCoSo} mo cong ket noi HIS nhung thieu {Thieu} — duong doc sang HIS dang tat.",
                idCoSo,
                string.IsNullOrWhiteSpace(baseUrl) ? "KetNoi_BaseUrlHIS" : "KetNoi_KhoaGoiHIS");
            return null;
        }

        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var goc))
        {
            _logger.LogWarning("Co so {IdCoSo} co KetNoi_BaseUrlHIS khong hop le: {BaseUrl}", idCoSo, baseUrl);
            return null;
        }

        return new CuaHis(goc, khoaGoiHis!, maCoSo);
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

            // data = null mà success = true thì coi là TẬP RỖNG, không phải lỗi:
            // "không tìm thấy" là kết quả bình thường của cả hai cửa này.
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.Xong, boc.Data ?? new List<T>());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Người dùng bỏ trang — không phải HIS hỏng. Vẫn về KhongHoiDuoc vì
            // ta thật sự không có dữ liệu, nhưng không ghi cảnh báo.
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.KhongHoiDuoc, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Ten} co so {IdCoSo}: khong goi duoc sang HIS", ten, idCoSo);
            return new KetQuaHoiHis<List<T>>(TrangThaiHoiHis.KhongHoiDuoc, null, ex.Message);
        }
    }
}
