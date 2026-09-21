using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Cửa của một cơ sở: cơ sở đó có trang riêng để chuyển hướng bệnh nhân sang không.
/// </summary>
/// <param name="IDCoSo">Khóa chính <c>DM_CSKCB.ID</c>.</param>
/// <param name="UrlChuyenHuong">
/// <c>DM_CSKCB.KetNoi_UrlChuyenHuong</c>. Rỗng/NULL = cơ sở KHÔNG có cửa riêng,
/// bệnh nhân ở lại trang nội bộ của SixosPwa.
/// </param>
public sealed record CuaCoSo(long IDCoSo, string? UrlChuyenHuong);

/// <summary>
/// Thay cho cả tầng <c>IPartnerGateway</c> + <c>PartnerGatewayFactory</c> cũ (đợt A).
///
/// 🔴 Quyết định "cơ sở này có cửa riêng không" nay là DỮ LIỆU, không còn là kiểu
/// bản cài: cột <c>DM_DoiTacApi.KieuApi</c> đã bị bỏ, bảng <c>DM_DoiTacApi</c> đã
/// gộp vào <c>DM_CSKCB</c>. Chỉ cần một cột có giá trị hay không —
/// <c>KetNoi_UrlChuyenHuong</c> — nên không còn gì để rẽ nhánh bằng lớp con.
///
/// <c>KetNoi_Active</c> là công tắc tắt đường kết nối mà không phải xóa cấu hình:
/// tắt thì coi như cơ sở không có cửa.
/// </summary>
public sealed class CuaCoSoService
{
    private readonly ApplicationDbContext _db;

    public CuaCoSoService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy cửa của một cơ sở. Trả <c>null</c> khi KHÔNG tìm thấy cơ sở — màn hình
    /// phải báo thật, đừng lẫn với "cơ sở có nhưng không có cửa riêng"
    /// (trường hợp đó trả về bản ghi có <see cref="CuaCoSo.UrlChuyenHuong"/> = null).
    /// </summary>
    public async Task<CuaCoSo?> LayCuaAsync(long idCoSo)
    {
        var dong = await _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.Id == idCoSo)
            .Select(x => new { x.Id, x.KetNoi_UrlChuyenHuong, x.KetNoi_Active })
            .FirstOrDefaultAsync();

        if (dong is null) return null;

        var url = dong.KetNoi_Active && !string.IsNullOrWhiteSpace(dong.KetNoi_UrlChuyenHuong)
            ? dong.KetNoi_UrlChuyenHuong!.Trim()
            : null;

        return new CuaCoSo(dong.Id, url);
    }

    /// <summary>
    /// Cơ sở này có cửa riêng để chuyển hướng sang không.
    /// Cơ sở không tồn tại ⇒ <c>false</c> (hỏng theo hướng an toàn: giữ bệnh nhân
    /// ở lại trang nội bộ chứ không đẩy đi đâu cả).
    /// </summary>
    public async Task<bool> CoChuyenHuongAsync(long idCoSo)
        => await LayCuaAsync(idCoSo) is { UrlChuyenHuong: not null and not "" };
}

/// <param name="DoiTacHong">
/// true = chính hệ bên kia đang hỏng, KHÔNG phải người dùng gõ sai. Giữ lại từ
/// tầng cửa cũ vì các luồng nội bộ (đổi mật khẩu, mở tài khoản) vẫn dùng chung
/// kiểu kết quả này.
/// </param>
public record KetQuaThaoTac(bool ThanhCong, string ThongBao, bool DoiTacHong = false);
