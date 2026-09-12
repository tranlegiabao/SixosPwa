using System.Net;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>Giá trị hợp lệ của <c>QL_TaiLieuBenhNhan.NguonKho</c>.</summary>
public static class NguonKhoTaiLieu
{
    /// <summary>Kho FTP của chính cổng (<c>sixospwa/...</c>) — cổng ghi.</summary>
    public const string Cong = "CONG";

    /// <summary>"Kho phiếu cơ sở" — FTP của phòng khám, cổng CHỈ ĐỌC.</summary>
    public const string CoSo = "COSO";
}

/// <summary>Đường dẫn có thật nhưng kho trả 550 — tệp không tồn tại ⇒ 404.</summary>
public sealed class KhoCoSoKhongCoTepException : Exception
{
    public KhoCoSoKhongCoTepException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>
/// Không với tới được kho: sai mật khẩu, máy chủ chết, quá 10 giây ⇒ 502.
/// 🔴 Phải TÁCH khỏi <see cref="KhoCoSoKhongCoTepException"/>: gộp hai ca lại đúng là
/// lỗi im lặng mà thuật ngữ <i>Chưa hỏi được cơ sở</i> sinh ra để dẹp (chốt 38).
/// </summary>
public sealed class KhoCoSoKhongNoiDuocException : Exception
{
    public KhoCoSoKhongNoiDuocException(string message, Exception? inner = null)
        : base(message, inner) { }
}

/// <summary>
/// Đọc "Kho phiếu cơ sở" — FTP <b>của phòng khám</b>, phục vụ chế độ <b>Trỏ đường</b>
/// (ADR 0030). Cổng không giữ byte, chỉ giữ đường rồi kéo về khi bệnh nhân mở.
///
/// 🔴 CHỈ ĐỌC, cố ý không có <c>Upload</c>/<c>Delete</c>/<c>Move</c>: nhờ vậy việc cổng
/// lỡ ghi vào kho của khách là <b>bất khả thi về kiểu</b>, không phải trông vào kỷ luật
/// người viết (chốt 43). Đó cũng là lý do lớp này tách hẳn khỏi <see cref="IFtpService"/>
/// — 635 dòng đang phục vụ 4 khu ảnh, và có đủ cả bốn lệnh ghi.
/// </summary>
public interface IKhoCoSoService
{
    /// <summary>
    /// Kéo một tệp về dưới dạng luồng. <paramref name="duongDan"/> là nguyên văn
    /// <c>QL_TaiLieuBenhNhan.DuongDanFtp</c> (chính là <c>URLKySo</c> bên HIS).
    /// </summary>
    /// <exception cref="KhoCoSoKhongCoTepException">Kho trả 550 ⇒ 404.</exception>
    /// <exception cref="KhoCoSoKhongNoiDuocException">Không với tới kho ⇒ 502.</exception>
    Task<Stream> TaiVeAsync(long idCoSo, string duongDan, CancellationToken ct = default);

    /// <summary>
    /// Nút <i>Thử kết nối kho</i> ở màn Sửa cơ sở gọi vào đây.
    ///
    /// 🔴 Nhận THÔNG SỐ RỜI, cố ý KHÔNG đọc dòng đang lưu trong DB. Hai lý do, cả hai
    /// đều đã thành lỗi thật khi bản đầu làm ngược (12/09):
    /// <list type="number">
    ///   <item>Người ta bấm Thử <b>trước khi Lưu</b> — phải thử đúng cái vừa gõ. Đọc DB
    ///   thì gõ mật khẩu sai vào ô vẫn báo "đạt", vì nó đang thử bản cũ.</item>
    ///   <item>Kho MỚI có <c>Active = 0</c>. Nếu đường thử đòi kho bật thì không đời nào
    ///   thử đạt ⇒ không bao giờ bật được — đúng vòng luẩn quẩn chốt 41 định tránh.</item>
    /// </list>
    /// </summary>
    Task<bool> ThuKetNoiAsync(ThongSoKho thongSo, CancellationToken ct = default);
}

/// <summary>
/// Thông số một kho, tách khỏi <see cref="Models.KhoFtpCoSo"/> để thử được cấu hình
/// <b>chưa lưu</b>. Không mang <c>Active</c>: đang thử thì cờ bật chưa có nghĩa gì.
/// </summary>
public sealed record ThongSoKho(string Host, string TaiKhoan, string MatKhau, string? ThuMucGoc);

public sealed class KhoCoSoService : IKhoCoSoService
{
    /// <summary>
    /// 🔴 10 giây, KHÔNG để mặc định. <see cref="FtpService"/> hiện không đặt timeout nào
    /// ⇒ <see cref="FtpWebRequest"/> lấy mặc định <b>100 giây</b>: bệnh nhân nhìn spinner
    /// 100 giây, mà tunnel cũng chết quanh đúng mốc đó.
    /// </summary>
    private const int TimeoutMs = 10_000;

    /// <summary>Thư mục mà HIS ký số ghi phiếu vào. Xem hàng rào ở <see cref="QuyDuong"/>.</summary>
    private const string ThuMucPhieu = "CongVan";

    private readonly ApplicationDbContext _db;
    private readonly ILogger<KhoCoSoService> _logger;

    public KhoCoSoService(ApplicationDbContext db, ILogger<KhoCoSoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Stream> TaiVeAsync(long idCoSo, string duongDan, CancellationToken ct = default)
    {
        var (kho, maCoSo) = await LayKhoAsync(idCoSo, ct);

        var thongSo = new ThongSoKho(kho.Host, kho.TaiKhoan, kho.MatKhau, kho.ThuMucGoc);
        var duongDayDu = QuyDuong(kho.ThuMucGoc, maCoSo, duongDan);
        if (duongDayDu == null)
        {
            // Không mở phiên FTP nào. Coi như không có tệp — nói "có nhưng chặn" là
            // đã lộ ra rằng nó tồn tại.
            _logger.LogWarning(
                "Chan duong nam ngoai kho co so {IdCoSo}: {DuongDan}", idCoSo, duongDan);
            throw new KhoCoSoKhongCoTepException("Đường dẫn tài liệu nằm ngoài kho của cơ sở.");
        }

        var request = TaoRequest(thongSo, duongDayDu, WebRequestMethods.Ftp.DownloadFile);

        try
        {
            using var response = await GoiCoHanAsync(request, ct);
            await using var luongMang = response.GetResponseStream();

            // Đệm trong bộ nhớ đúng một lượt rồi trả — y khuôn FtpService.DownloadAsync.
            // KHÔNG đệm ra đĩa / cache: đo thật PDF 232KB mất 110–241 ms (chốt 37).
            var bo = new MemoryStream();
            await luongMang.CopyToAsync(bo, ct);
            bo.Position = 0;
            return bo;
        }
        catch (WebException ex) when (LaKhongCoTep(ex))
        {
            _logger.LogWarning("Kho co so {IdCoSo} bao khong co tep: {Duong}", idCoSo, duongDayDu);
            throw new KhoCoSoKhongCoTepException("Không tìm thấy tệp trên kho của cơ sở.", ex);
        }
        catch (Exception ex) when (ex is not KhoCoSoKhongCoTepException)
        {
            _logger.LogWarning(ex, "Khong noi duoc kho co so {IdCoSo}: {Duong}", idCoSo, duongDayDu);
            throw new KhoCoSoKhongNoiDuocException("Chưa lấy được tài liệu từ phòng khám.", ex);
        }
    }

    public async Task<bool> ThuKetNoiAsync(ThongSoKho thongSo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(thongSo.Host)
            || string.IsNullOrWhiteSpace(thongSo.TaiKhoan)
            || string.IsNullOrWhiteSpace(thongSo.MatKhau))
            return false;

        var goc = (thongSo.ThuMucGoc ?? "").Replace('\\', '/').Trim('/');
        var request = TaoRequest(thongSo, goc, WebRequestMethods.Ftp.ListDirectory);

        try
        {
            using var response = await GoiCoHanAsync(request, ct);
            await using var luong = response.GetResponseStream();
            using var doc = new StreamReader(luong);
            await doc.ReadToEndAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Sai mat khau, host chet, thu muc goc khong co — deu la "chua dat".
            _logger.LogWarning(ex, "Thu ket noi kho {Host} / {TaiKhoan} that bai",
                thongSo.Host, thongSo.TaiKhoan);
            return false;
        }
    }

    // ------------------------------------------------------------------ nội bộ

    private async Task<(KhoFtpCoSo Kho, string MaCoSo)> LayKhoAsync(long idCoSo, CancellationToken ct)
    {
        var dong = await (
            from k in _db.KhoFtpCoSos.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on k.IdCoSo equals cs.Id
            where k.IdCoSo == idCoSo
            select new { Kho = k, cs.MaCoSo }).FirstOrDefaultAsync(ct);

        if (dong == null)
            throw new KhoCoSoKhongNoiDuocException("Cơ sở chưa khai báo kho phiếu.");

        if (!dong.Kho.Active)
            throw new KhoCoSoKhongNoiDuocException("Kho phiếu của cơ sở đang tắt.");

        return (dong.Kho, dong.MaCoSo);
    }

    /// <summary>
    /// Gọi FTP với TRẦN CỨNG <see cref="TimeoutMs"/> tính bằng đồng hồ thật.
    ///
    /// 🔴 Vì sao không tin mỗi <c>FtpWebRequest.Timeout</c>: nó KHÔNG cắt được giai đoạn
    /// TCP connect. Đo thật 12/09 với host chết <c>192.0.2.1</c> — đặt Timeout 10 giây
    /// nhưng lời gọi vẫn về sau <b>21 giây</b>, đúng mốc SYN-retry mặc định của Windows.
    /// Bệnh nhân nhìn spinner 21 giây, và tunnel cũng chết quanh mốc đó.
    ///
    /// Nên trần thật nằm ở đây: hết giờ thì <c>Abort()</c> request rồi ném ra, không chờ
    /// tầng dưới tự nghĩ lại.
    /// </summary>
    private static async Task<FtpWebResponse> GoiCoHanAsync(FtpWebRequest request, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeoutMs);

        // Abort() de socket khong bi bo lai treo cho den khi Windows tu bo cuoc.
        using var dangKy = cts.Token.Register(() =>
        {
            try { request.Abort(); } catch { /* dang huy roi, nuot */ }
        });

        return (FtpWebResponse)await request.GetResponseAsync().WaitAsync(cts.Token);
    }

    private static FtpWebRequest TaoRequest(ThongSoKho thongSo, string duongDan, string method)
    {
        var host = (thongSo.Host ?? "").Trim().TrimEnd('/');

        // Khai host kiểu "ftp://x" hay "x" đều nhận — người khai không phải nhớ luật.
        if (host.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
            host = host["ftp://".Length..];

        var url = string.IsNullOrEmpty(duongDan) ? $"ftp://{host}/" : $"ftp://{host}/{duongDan}";

        var request = (FtpWebRequest)WebRequest.Create(url);
        request.Method = method;
        request.Credentials = new NetworkCredential(thongSo.TaiKhoan, thongSo.MatKhau);
        request.UseBinary = true;
        request.UsePassive = true;
        request.KeepAlive = false;
        // 🔴 Hai dòng này là cả điểm khác biệt so với FtpService. Xem TimeoutMs.
        request.Timeout = TimeoutMs;
        request.ReadWriteTimeout = TimeoutMs;
        return request;
    }

    /// <summary>
    /// HÀNG RÀO. Trả <c>null</c> cho mọi đường không quy được về dưới
    /// <c>{ThuMucGoc}/{MaCoSo}/CongVan/</c> — khuôn <see cref="KhoAnh"/>: trả null chứ
    /// không ném, và chặn <c>..</c> / <c>\</c> trước mọi thứ khác.
    ///
    /// Đo 12/09 trên Dev_Master3: 135/135 đường <c>URLKySo</c> đúng khuôn
    /// <c>77121/CongVan/&lt;MaBN&gt;_&lt;TenKhongDau&gt;/&lt;MaVaoVien&gt;/&lt;tên&gt;.pdf</c>,
    /// dài 55–95 ký tự, 0 đường tuyệt đối ⇒ hàng rào này không chặn nhầm dữ liệu thật.
    /// </summary>
    internal static string? QuyDuong(string? thuMucGoc, string maCoSo, string? duongDan)
    {
        if (string.IsNullOrWhiteSpace(duongDan)) return null;
        if (string.IsNullOrWhiteSpace(maCoSo)) return null;

        var duong = duongDan.Replace('\\', '/').Trim();

        // Đường tuyệt đối / có lược đồ thì không phải đường tương đối của kho.
        if (duong.Contains("://", StringComparison.Ordinal)) return null;

        var doan = duong.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (doan.Length < 3) return null;
        if (doan.Any(p => p == ".." || p == ".")) return null;

        // Phải đúng {MaCoSo}/CongVan/... — đó là chỗ DUY NHẤT ký số ghi phiếu ra
        // (CaServices -> QlLuuTruCongVanServices -> URLKySo).
        if (!string.Equals(doan[0], maCoSo, StringComparison.OrdinalIgnoreCase)) return null;
        if (!string.Equals(doan[1], ThuMucPhieu, StringComparison.OrdinalIgnoreCase)) return null;

        var tuongDoi = string.Join('/', doan);
        var goc = (thuMucGoc ?? "").Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(goc) ? tuongDoi : $"{goc}/{tuongDoi}";
    }

    /// <summary>FTP 550 = tệp/thư mục không tồn tại. Mọi thứ khác coi là không với tới.</summary>
    private static bool LaKhongCoTep(WebException ex) =>
        ex.Response is FtpWebResponse phanHoi
        && phanHoi.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable;
}
