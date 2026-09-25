using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Models.Dto;
using SixosPwa.Services;

namespace SixosPwa.Security;

/// <summary>
/// Cửa cho MÁY vào: xác thực một cuộc gọi từ HIS của một cơ sở bằng khóa cấp
/// riêng cho cơ sở đó. Bám khuôn <c>[LisAccessKey]</c> bên HisSoft đang chạy
/// thật (<c>C0307_LIS_IntegrationController</c>).
///
/// <para>
/// 🔴 Đặt attribute này lên TỪNG controller của khu <c>api/v1</c>. Khu đó CHỈ
/// dành cho máy. Đường cho NGƯỜI xem tài liệu nằm bên <c>HomeController</c> với
/// cookie và <c>[Authorize]</c> — hai loại người gọi, hai khu riêng. Gộp chung
/// một controller rồi đánh dấu <c>[AllowAnonymous]</c> ở mức lớp chính là cách
/// đường đọc tài liệu từng bị hở cho cả thế giới.
/// </para>
/// <para>
/// KHÔNG kiểm <c>DM_CSKCB.HienThiCongKhai</c> ở đây. Theo ADR 0013 cờ ấy chỉ
/// quyết định cơ sở có hiện ở cổng công khai và có nhận đăng nhập mới không.
/// Một cơ sở tạm ẩn đi để sửa nội dung mà bị ngưng nhận kết quả xét nghiệm là
/// lỗi im lặng. Sau đợt gộp, công tắc đường API là <c>Khoa_NgayHetHan</c>
/// (hết hạn) và <c>KhoaBam IS NULL</c> (thu hồi khóa) trên chính <c>DM_CSKCB</c>.
/// </para>
/// <para>
/// 🔴 <b>Đọc <c>KhoaBam</c> bằng CÂU SQL RIÊNG, cố ý không qua EF.</b> Bốn cột bí
/// mật (<c>KhoaBam</c> · <c>Ftp_TaiKhoan</c> · <c>Ftp_MatKhau</c> ·
/// <c>KetNoi_KhoaGoiHIS</c>) KHÔNG được khai trong thực thể <see cref="DMCSKCB"/>:
/// có 59 chỗ đọc <c>DM_CSKCB</c> qua EF và trang công khai nạp TRỌN thực thể mọi
/// cơ sở. Khai vào thực thể là lộ bí mật ở 59 chỗ; đọc SQL riêng là sửa 3 chỗ.
/// Chỉ SELECT đúng cột cần, không bao giờ <c>SELECT *</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class KhoaCoSoAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderKhoa = "X-API-Key";
    public const string HeaderMaCoSo = "X-Ma-CSKCB";

    /// <summary>Khóa đọc cơ sở đã xác thực ra khỏi <c>HttpContext.Items</c>.</summary>
    public const string ItemCoSo = "KhoaCoSo.CoSo";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
        var nhatKy = http.RequestServices.GetRequiredService<INhatKyApi>();

        var endpoint = http.Request.Path.Value ?? "";
        var ip = http.Connection.RemoteIpAddress?.ToString();

        var khoaTho = http.Request.Headers[HeaderKhoa].ToString().Trim();
        var maCoSo = http.Request.Headers[HeaderMaCoSo].ToString().Trim();

        if (string.IsNullOrWhiteSpace(khoaTho) || string.IsNullOrWhiteSpace(maCoSo))
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.ThieuHeader, ip,
                $"Thiếu header bắt buộc '{HeaderKhoa}' hoặc '{HeaderMaCoSo}'.");
            return;
        }

        // Tra cứu bằng BĂM, không bao giờ so khóa thô với giá trị trong CSDL —
        // CSDL không giữ khóa thô. Băm theo byte UTF-8 để khớp với
        // HASHBYTES('SHA2_256', CONVERT(varchar, ...)) mà script chuyển khóa cũ
        // dùng. Băm nhầm nvarchar là ra byte UTF-16 và hai bên không bao giờ gặp.
        var bam = SHA256.HashData(Encoding.UTF8.GetBytes(khoaTho));

        var khoa = await TimCoSoTheoKhoaBamAsync(db, bam, http.RequestAborted);

        if (khoa is null)
        {
            // Sau đợt gộp, ba ca "không có khóa" / "khóa đã thu hồi (KhoaBam NULL)"
            // / "khóa hết hạn" đều rơi về một câu trả lời: không tìm thấy dòng.
            // Câu WHERE đã lọc Khoa_NgayHetHan nên không còn phân biệt được nữa —
            // cố ý, để không nói cho bên gọi biết khóa của họ từng tồn tại.
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaSai, ip,
                "Xác thực thất bại: khóa không đúng, đã bị thu hồi hoặc đã hết hạn.");
            return;
        }

        // Khóa đúng nhưng gõ nhầm mã cơ sở: chặn lại thay vì lặng lẽ lấy cơ sở
        // theo khóa. Nếu không, một cơ sở gõ nhầm mã sẽ đẩy dữ liệu của mình
        // sang hồ sơ của cơ sở khác mà không ai biết.
        if (!string.Equals(khoa.Value.MaCoSo, maCoSo, StringComparison.OrdinalIgnoreCase))
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaKhacCoSo, ip,
                "Khóa không thuộc cơ sở đã khai trong header.", khoa.Value.Id);
            return;
        }

        var coSo = await db.DMCSKCBs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == khoa.Value.Id);

        if (coSo == null)
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaSai, ip,
                "Xác thực thất bại: khóa không đúng hoặc cơ sở chưa được cấp khóa.");
            return;
        }

        http.Items[ItemCoSo] = coSo;
    }

    /// <summary>
    /// Đọc <c>KhoaBam</c> — một trong 4 cột bí mật — bằng CÂU SQL RIÊNG (ADO thuần),
    /// KHÔNG qua EF, vì cột này cố ý không nằm trong thực thể <see cref="DMCSKCB"/>.
    /// Chỉ lấy đúng <c>ID</c> + <c>MaCoSo</c>, không <c>SELECT *</c>.
    ///
    /// 🔴 Đúng <b>một dòng duy nhất</b> theo nghĩa <c>SingleOrDefault</c>: sau đợt gộp,
    /// <c>KhoaBam</c> là DUY NHẤT toàn hệ (index lọc <c>UK_DM_CSKCB_KhoaBam</c> trên
    /// <c>IS NOT NULL</c>). Đọc được hai dòng nghĩa là ràng buộc đó đã vỡ — phải NỔ ra
    /// chứ không được im lặng chọn dòng đầu như <c>FirstOrDefault</c> cũ.
    /// </summary>
    private static async Task<(long Id, string MaCoSo)?> TimCoSoTheoKhoaBamAsync(
        ApplicationDbContext db, byte[] bam, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT ID, MaCoSo
FROM dbo.DM_CSKCB
WHERE KhoaBam = @bam
  AND (Khoa_NgayHetHan IS NULL OR Khoa_NgayHetHan > GETDATE());";

        var p = cmd.CreateParameter();
        p.ParameterName = "@bam";
        p.DbType = System.Data.DbType.Binary;
        p.Size = 32;
        p.Value = bam;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct)) return null;

        var ketQua = (reader.GetInt64(0), reader.GetString(1));

        if (await reader.ReadAsync(ct))
            throw new InvalidOperationException(
                "DM_CSKCB.KhoaBam bị trùng — ràng buộc UK_DM_CSKCB_KhoaBam đã vỡ.");

        return ketQua;
    }

    private static async Task ChanAsync(
        AuthorizationFilterContext context,
        INhatKyApi nhatKy,
        string endpoint,
        string lyDo,
        string? ip,
        string thongBao,
        long? idCoSo = null)
    {
        // Cột HT_LogApiCoSo.IDKhoa đã bị xóa và tham số @IDKhoa đã gỡ khỏi stored
        // HT_LogApiCoSo_Ghi — còn truyền idKhoa vào đây là nhật ký chết im lặng.
        await nhatKy.GhiAsync(endpoint, KetQuaApi.TuChoi,
            idCoSo: idCoSo, lyDo: lyDo, ipGoi: ip);

        context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail(thongBao));
    }
}

/// <summary>Đọc kết quả của <see cref="KhoaCoSoAttribute"/> trong controller.</summary>
public static class KhoaCoSoExtensions
{
    public static DMCSKCB CoSoDaXacThuc(this HttpContext http) =>
        (DMCSKCB)http.Items[KhoaCoSoAttribute.ItemCoSo]!;
}
