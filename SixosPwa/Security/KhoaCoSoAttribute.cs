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
/// KHONG kiem <c>DM_CSKCB.Active</c> o day. Theo ADR 0013 co ay chi quyet dinh
/// co so co hien o cong cong khai va co nhan dang nhap moi khong. Mot co so tam
/// an di de sua noi dung ma bi ngung nhan ket qua xet nghiem la loi im lang.
/// Cong tat duong API la <c>HT_KhoaApiCoSo.Active</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class KhoaCoSoAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderKhoa = "X-API-Key";
    public const string HeaderMaCoSo = "X-Ma-CSKCB";

    /// <summary>Khoa doc co so da xac thuc ra khoi <c>HttpContext.Items</c>.</summary>
    public const string ItemCoSo = "KhoaCoSo.CoSo";
    public const string ItemIdKhoa = "KhoaCoSo.IdKhoa";

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
        // HASHBYTES('SHA2_256', CONVERT(varchar, ...)) ma script 01 dung khi
        // chuyen 3 khoa cu sang. Bam nham nvarchar la ra byte UTF-16 va hai ben
        // khong bao gio gap nhau.
        var bam = SHA256.HashData(Encoding.UTF8.GetBytes(khoaTho));

        var khoa = await db.KhoaApiCoSos.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KhoaBam == bam);

        if (khoa == null)
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaSai, ip,
                "Xác thực thất bại: khóa không đúng hoặc cơ sở chưa được cấp khóa.");
            return;
        }

        if (!khoa.Active)
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaTat, ip,
                "Khóa này đã bị thu hồi.", khoa.IdCoSo, khoa.Id);
            return;
        }

        if (khoa.NgayHetHan.HasValue && khoa.NgayHetHan.Value < DateTime.Now)
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaHetHan, ip,
                "Khóa này đã hết hạn.", khoa.IdCoSo, khoa.Id);
            return;
        }

        var coSo = await db.DMCSKCBs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == khoa.IdCoSo);

        // Khoa dung nhung go nham ma co so: chan lai thay vi lang le lay co so
        // theo khoa. Neu khong, mot co so go nham ma se day du lieu cua minh
        // sang ho so cua co so khac ma khong ai biet.
        if (coSo == null || !string.Equals(coSo.MaCoSo, maCoSo, StringComparison.OrdinalIgnoreCase))
        {
            await ChanAsync(context, nhatKy, endpoint, LyDoApi.KhoaKhacCoSo, ip,
                "Khóa không thuộc cơ sở đã khai trong header.", khoa.IdCoSo, khoa.Id);
            return;
        }

        http.Items[ItemCoSo] = coSo;
        http.Items[ItemIdKhoa] = khoa.Id;
    }

    private static async Task ChanAsync(
        AuthorizationFilterContext context,
        INhatKyApi nhatKy,
        string endpoint,
        string lyDo,
        string? ip,
        string thongBao,
        long? idCoSo = null,
        long? idKhoa = null)
    {
        await nhatKy.GhiAsync(endpoint, KetQuaApi.TuChoi,
            idCoSo: idCoSo, idKhoa: idKhoa, lyDo: lyDo, ipGoi: ip);

        context.Result = new UnauthorizedObjectResult(ApiResponse<object>.Fail(thongBao));
    }
}

/// <summary>Doc ket qua cua <see cref="KhoaCoSoAttribute"/> trong controller.</summary>
public static class KhoaCoSoExtensions
{
    public static DMCSKCB CoSoDaXacThuc(this HttpContext http) =>
        (DMCSKCB)http.Items[KhoaCoSoAttribute.ItemCoSo]!;

    public static long IdKhoaDaXacThuc(this HttpContext http) =>
        (long)http.Items[KhoaCoSoAttribute.ItemIdKhoa]!;
}
