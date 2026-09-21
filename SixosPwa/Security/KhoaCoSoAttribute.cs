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
/// Cua cho MAY vao: xac thuc mot cuoc goi tu HIS cua mot co so bang khoa cap
/// rieng cho co so do. Bam khuon <c>[LisAccessKey]</c> ben HisSoft dang chay
/// that (<c>C0307_LIS_IntegrationController</c>).
///
/// <para>
/// 🔴 Dat attribute nay len TUNG controller cua khu <c>api/v1</c>. Khu do CHI
/// danh cho may. Duong cho NGUOI xem tai lieu nam ben <c>HomeController</c> voi
/// cookie va <c>[Authorize]</c> — hai loai nguoi goi, hai khu rieng. Gop chung
/// mot controller roi danh dau <c>[AllowAnonymous]</c> o muc lop chinh la cach
/// duong doc tai lieu tung bi ho cho ca the gioi.
/// </para>
/// <para>
/// KHONG kiem <c>DM_CSKCB.HienThiCongKhai</c> o day. Theo ADR 0013 co ay chi
/// quyet dinh co so co hien o cong cong khai va co nhan dang nhap moi khong.
/// Mot co so tam an di de sua noi dung ma bi ngung nhan ket qua xet nghiem la
/// loi im lang. Sau dot gop, cong tat duong API la <c>Khoa_NgayHetHan</c>
/// (het han) va <c>KhoaBam IS NULL</c> (thu hoi khoa) tren chinh <c>DM_CSKCB</c>.
/// </para>
/// <para>
/// 🔴 <b>Doc <c>KhoaBam</c> bang CAU SQL RIENG, co y khong qua EF.</b> Bon cot bi
/// mat (<c>KhoaBam</c> · <c>Ftp_TaiKhoan</c> · <c>Ftp_MatKhau</c> ·
/// <c>KetNoi_KhoaGoiHIS</c>) KHONG duoc khai trong thuc the <see cref="DMCSKCB"/>:
/// co 59 cho doc <c>DM_CSKCB</c> qua EF va trang cong khai nap TRON thuc the moi
/// co so. Khai vao thuc the la lo bi mat o 59 cho; doc SQL rieng la sua 3 cho.
/// Chi SELECT dung cot can, khong bao gio <c>SELECT *</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class KhoaCoSoAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderKhoa = "X-API-Key";
    public const string HeaderMaCoSo = "X-Ma-CSKCB";

    /// <summary>Khoa doc co so da xac thuc ra khoi <c>HttpContext.Items</c>.</summary>
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

        // Tra cuu bang BAM, khong bao gio so khoa tho voi gia tri trong CSDL —
        // CSDL khong giu khoa tho. Bam theo byte UTF-8 de khop voi
        // HASHBYTES('SHA2_256', CONVERT(varchar, ...)) ma script chuyen khoa cu
        // dung. Bam nham nvarchar la ra byte UTF-16 va hai ben khong bao gio gap.
        var bam = SHA256.HashData(Encoding.UTF8.GetBytes(khoaTho));

        var khoa = await TimCoSoTheoKhoaBamAsync(db, bam, http.RequestAborted);

        if (khoa is null)
        {
            // Sau dot gop, ba ca "khong co khoa" / "khoa da thu hoi (KhoaBam NULL)"
            // / "khoa het han" deu roi ve mot cau tra loi: khong tim thay dong.
            // Cau WHERE da loc Khoa_NgayHetHan nen khong con phan biet duoc nua —
            // co y, de khong noi cho ben goi biet khoa cua ho tung ton tai.
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaSai, ip,
                "Xác thực thất bại: khóa không đúng, đã bị thu hồi hoặc đã hết hạn.");
            return;
        }

        // Khoa dung nhung go nham ma co so: chan lai thay vi lang le lay co so
        // theo khoa. Neu khong, mot co so go nham ma se day du lieu cua minh
        // sang ho so cua co so khac ma khong ai biet.
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
    /// Doc <c>KhoaBam</c> — mot trong 4 cot bi mat — bang CAU SQL RIENG (ADO thuan),
    /// KHONG qua EF, vi cot nay co y khong nam trong thuc the <see cref="DMCSKCB"/>.
    /// Chi lay dung <c>ID</c> + <c>MaCoSo</c>, khong <c>SELECT *</c>.
    ///
    /// 🔴 Dung <b>mot dong duy nhat</b> theo nghia <c>SingleOrDefault</c>: sau dot gop,
    /// <c>KhoaBam</c> la DUY NHAT toan he (index loc <c>UK_DM_CSKCB_KhoaBam</c> tren
    /// <c>IS NOT NULL</c>). Doc duoc hai dong nghia la rang buoc do da vo — phai NO ra
    /// chu khong duoc im lang chon dong dau nhu <c>FirstOrDefault</c> cu.
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
        // Cot HT_LogApiCoSo.IDKhoa da bi xoa va tham so @IDKhoa da go khoi stored
        // HT_LogApiCoSo_Ghi — con truyen idKhoa vao day la nhat ky chet im lang.
        await nhatKy.GhiAsync(endpoint, KetQuaApi.TuChoi,
            idCoSo: idCoSo, lyDo: lyDo, ipGoi: ip);

        context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail(thongBao));
    }
}

/// <summary>Doc ket qua cua <see cref="KhoaCoSoAttribute"/> trong controller.</summary>
public static class KhoaCoSoExtensions
{
    public static DMCSKCB CoSoDaXacThuc(this HttpContext http) =>
        (DMCSKCB)http.Items[KhoaCoSoAttribute.ItemCoSo]!;
}
