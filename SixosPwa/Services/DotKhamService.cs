using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Models.Dto;

namespace SixosPwa.Services;

public record KetQuaNhanLo(bool CoHoSo, NhanDotKhamResponseData DuLieu);

public interface IDotKhamService
{
    Task<KetQuaNhanLo> NhanLoAsync(DMCSKCB coSo, NhanDotKhamRequest yeuCau);

    Task<List<string>> LocMaDaCoNguoiNhanAsync(long idCoSo, List<string> maBenhNhan);
}

/// <summary>
/// Nhận lịch sử khám HIS đẩy lên.
///
/// <para>
/// 🔴 Luật TỪ CHỐI (chốt 3): mã bệnh nhân chưa có hồ sơ nào nối bên cổng thì cả
/// lô bị từ chối, cổng không lưu gì. Cổng không giữ dữ liệu y tế của người chưa
/// là người dùng. HIS giữ lô đó ở hàng đợi và hỏi lại bằng
/// <c>/api/v1/ho-so/kiem-tra-nhan</c> để biết khi nào đẩy được.
/// </para>
/// <para>
/// KHÔNG đoán hồ sơ bằng CCCD hay số điện thoại. Đó là điều ADR 0018 cấm: Thiên
/// Nam có 345 nhóm cùng CCCD khác tên, và có số điện thoại gán tới 876 người.
/// Việc nối hồ sơ là của NGƯỜI DÙNG ở màn Nối hồ sơ, không phải của đường API.
/// </para>
/// </summary>
public class DotKhamService : IDotKhamService
{
    /// <summary>Chặn lô quá lớn: một người vài chục đợt là bình thường, vài nghìn thì không.</summary>
    private const int ToiDaMoiLo = 500;

    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _thuTuc;
    private readonly ILogger<DotKhamService> _logger;

    public DotKhamService(
        ApplicationDbContext db,
        AdminStoredProcedureService thuTuc,
        ILogger<DotKhamService> logger)
    {
        _db = db;
        _thuTuc = thuTuc;
        _logger = logger;
    }

    public async Task<KetQuaNhanLo> NhanLoAsync(DMCSKCB coSo, NhanDotKhamRequest yeuCau)
    {
        var maBN = (yeuCau.MaBenhNhan ?? "").Trim();

        if (string.IsNullOrWhiteSpace(maBN))
            throw new ArgumentException("Mã bệnh nhân (maBenhNhan) không được để trống.");

        if (yeuCau.DotKham == null || yeuCau.DotKham.Count == 0)
            throw new ArgumentException("Lô đợt khám (dotKham) không được rỗng.");

        if (yeuCau.DotKham.Count > ToiDaMoiLo)
            throw new ArgumentException($"Một lô tối đa {ToiDaMoiLo} đợt khám, lô này có {yeuCau.DotKham.Count}.");

        // Khóa tra cứu là (cơ sở, mã bệnh nhân) — đúng ràng buộc thật của
        // DM_BenhNhanCoSo. Không tra theo con người, vì một người tại MỘT cơ sở
        // vẫn có thể có nhiều hồ sơ (17,3% ở Thiên Nam).
        var hoSo = await _db.BenhNhans.AsNoTracking()
            .FirstOrDefaultAsync(b => b.IdCoSo == coSo.Id && b.MaBN == maBN);

        if (hoSo == null)
        {
            return new KetQuaNhanLo(false, new NhanDotKhamResponseData { MaBenhNhan = maBN });
        }

        var ketQua = new NhanDotKhamResponseData { MaBenhNhan = maBN };

        foreach (var dong in yeuCau.DotKham)
        {
            var maVaoVien = (dong.MaVaoVien ?? "").Trim();

            if (string.IsNullOrWhiteSpace(maVaoVien))
            {
                ketQua.SoBoQua++;
                ketQua.DongBoQua.Add("Thiếu maVaoVien.");
                continue;
            }

            if (dong.NgayGioVao == default)
            {
                ketQua.SoBoQua++;
                ketQua.DongBoQua.Add($"{maVaoVien}: thiếu ngayGioVao.");
                continue;
            }

            // Ngày ra trước ngày vào là dữ liệu hỏng — nhận vào rồi màn hiện thị
            // sẽ ra khoảng thời gian âm.
            if (dong.NgayGioRa.HasValue && dong.NgayGioRa.Value < dong.NgayGioVao)
            {
                ketQua.SoBoQua++;
                ketQua.DongBoQua.Add($"{maVaoVien}: ngayGioRa trước ngayGioVao.");
                continue;
            }

            var (kq, _) = await _thuTuc.SaveDotKhamAsync(
                idCoSo: coSo.Id,
                idBenhNhan: hoSo.Id,
                maVaoVien: maVaoVien,
                maBN: maBN,
                ngayGioVao: dong.NgayGioVao,
                ngayGioRa: dong.NgayGioRa,
                tenKhoa: dong.TenKhoa?.Trim(),
                tenBacSi: dong.TenBacSi?.Trim(),
                chanDoan: dong.ChanDoan?.Trim());

            if (kq.Succeeded)
            {
                ketQua.SoDaNhan++;
            }
            else
            {
                ketQua.SoBoQua++;
                ketQua.DongBoQua.Add($"{maVaoVien}: {kq.Message}");
                _logger.LogWarning("Bo qua dot kham {MaVaoVien} cua co so {MaCoSo}: {Message}",
                    maVaoVien, coSo.MaCoSo, kq.Message);
            }
        }

        return new KetQuaNhanLo(true, ketQua);
    }

    public async Task<List<string>> LocMaDaCoNguoiNhanAsync(long idCoSo, List<string> maBenhNhan)
    {
        var ma = maBenhNhan
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct()
            .ToList();

        if (ma.Count == 0) return new List<string>();

        // MaBN ở model vẫn là string không-null: script 05 mới chỉ NỚI ràng buộc
        // cho phép NULL, còn dữ liệu thì chưa dòng nào NULL — việc dồn 18/21 mã
        // tự bịa đi cùng đợt xóa SinhMaBenhNhan(). Đổi model sang string? phải
        // đổi cùng lúc với đợt đó.
        return await _db.BenhNhans.AsNoTracking()
            .Where(b => b.IdCoSo == idCoSo && ma.Contains(b.MaBN))
            .Select(b => b.MaBN)
            .Distinct()
            .ToListAsync();
    }
}
