using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Tra cau hinh cua mot co so va chon dung ban cai IPartnerGateway theo KieuApi.
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
public record CuaCoSo(DoiTacApi CauHinh, IPartnerGateway Cua)
{
    public bool CoBanGiao => Cua.CoBanGiao;
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

        var cauHinh = await _db.DoiTacApis
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MaCoSo == maCoSo, ct);

        // Co so chua co dong nao trong DM_DoiTacApi: coi nhu khong co API rieng.
        // Dung mot ban ghi tam de luong phia sau khong phai kiem null khap noi.
        cauHinh ??= new DoiTacApi { MaCoSo = maCoSo, KieuApi = KieuApiDoiTac.KhongCo, Active = 1 };

        // Tat mot co so bang UPDATE Active = 0 thi no rot ve nhanh noi bo,
        // khong phai build lai app (tieu chi nghiem thu so 7).
        var kieu = cauHinh.Active == 1 ? cauHinh.KieuApi : KieuApiDoiTac.KhongCo;

        var cua = _cacCua.FirstOrDefault(x => string.Equals(x.KieuApi, kieu, StringComparison.OrdinalIgnoreCase));

        if (cua is null)
        {
            _logger.LogWarning("Khong co ban cai IPartnerGateway cho KieuApi={KieuApi}, rot ve noi bo", kieu);
            cua = _cacCua.First(x => x.KieuApi == KieuApiDoiTac.KhongCo);
        }

        return new CuaCoSo(cauHinh, cua);
    }
}
