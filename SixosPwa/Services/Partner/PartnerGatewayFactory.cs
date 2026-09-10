using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Tra cau hinh cua mot co so va chon dung ban cai IPartnerGateway.
/// Day la CHO DUY NHAT re nhanh "co API" / "khong co API" — man hinh khong duoc
/// tu kiem tra MaCoSo. Giai doan 2: them ban cai moi vao DI la xong.
/// </summary>
public interface IPartnerGatewayFactory
{
    /// <summary>
    /// Lay cau hinh + cua cua co so. Tra null khi khong tim thay ma co so
    /// (goi la co so chua duoc dang ky), luc do man hinh phai bao that.
    /// </summary>
    Task<CuaCoSo?> LayAsync(string? maCoSo, CancellationToken ct = default);
}

/// <summary>Cap cau hinh + ban cai da chon, di chung nhau khap luong.</summary>
public record CuaCoSo(DoiTacApi CauHinh, IPartnerGateway Cua, string? MaNhom = null)
{
    public bool CoBanGiao => Cua.CoBanGiao;

    /// <summary>Co so nay dung bo man cua doi tac thay cho luong OTP cua SixosPwa.</summary>
    public bool DungManDoiTac => Cua.DungManDoiTac;
}

public class PartnerGatewayFactory : IPartnerGatewayFactory
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IPartnerGateway> _cacCua;
    private readonly ILogger<PartnerGatewayFactory> _logger;

    public PartnerGatewayFactory(
        ApplicationDbContext db,
        IEnumerable<IPartnerGateway> cacCua,
        ILogger<PartnerGatewayFactory> logger)
    {
        _db = db;
        _cacCua = cacCua;
        _logger = logger;
    }

    public async Task<CuaCoSo?> LayAsync(string? maCoSo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(maCoSo)) return null;

        // Dang ky API nay khoa theo IDCoSo (khoa ngoai), khong con theo chuoi MaCoSo.
        var coSo = await _db.DMCSKCBs
            .AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo)
            .Select(x => new { x.Id, x.IdNhomCS })
            .FirstOrDefaultAsync(ct);

        if (coSo is null) return null;

        // Truy nguoc IDCoSo -> IDNhomCS -> MaNhom (benhvien, nhakhoa, pkdk, ...)
        string? maNhom = null;
        if (coSo.IdNhomCS.HasValue)
        {
            maNhom = await _db.DMNhomCSs
                .AsNoTracking()
                .Where(n => n.ID == coSo.IdNhomCS.Value)
                .Select(n => n.MaNhom)
                .FirstOrDefaultAsync(ct);
        }

        var cauHinh = await _db.DoiTacApis
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdCoSo == coSo.Id, ct);

        // Co so chua co dong nao trong DM_DoiTacApi: coi nhu khong co API rieng.
        // Dung mot ban ghi tam de luong phia sau khong phai kiem null khap noi.
        cauHinh ??= new DoiTacApi
        {
            IdCoSo = coSo.Id,
            Active = false
        };

        // Chon Gateway dua tren cau hinh (TrangChu -> ban giao Ub, BaseUrl -> HIS, con lai -> NoApi noi bo)
        IPartnerGateway? cua = null;
        if (cauHinh.Active && !string.IsNullOrWhiteSpace(cauHinh.TrangChu))
        {
            cua = _cacCua.FirstOrDefault(x => string.Equals(x.TenCong, "Ub", StringComparison.OrdinalIgnoreCase));
        }
        else if (cauHinh.Active && !string.IsNullOrWhiteSpace(cauHinh.BaseUrl))
        {
            cua = _cacCua.FirstOrDefault(x => string.Equals(x.TenCong, "His", StringComparison.OrdinalIgnoreCase));
        }

        if (cua is null)
        {
            cua = _cacCua.FirstOrDefault(x => string.Equals(x.TenCong, "NoApi", StringComparison.OrdinalIgnoreCase))
                ?? _cacCua.First();
        }

        return new CuaCoSo(cauHinh, cua, maNhom);
    }
}
