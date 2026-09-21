using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Models.Dto;

namespace SixosPwa.Services;

/// <summary>Ket qua nhan mot lo dot kham.</summary>
public record KetQuaNhanLo(bool CoHoSo, NhanDotKhamResponseData DuLieu);

public interface IDotKhamService
{
    Task<KetQuaNhanLo> NhanLoAsync(DMCSKCB coSo, NhanDotKhamRequest yeuCau);

    Task<List<string>> LocMaDaCoNguoiNhanAsync(long idCoSo, List<string> maBenhNhan);
}

/// <summary>
/// Nhan lich su kham HIS day len.
///
/// <para>
/// 🔴 Luat TU CHOI (chot 3): ma benh nhan chua co ho so nao noi ben cong thi ca
/// lo bi tu choi, cong khong luu gi. Cong khong giu du lieu y te cua nguoi chua
/// la nguoi dung. HIS giu lo do o hang doi va hoi lai bang
/// <c>/api/v1/ho-so/kiem-tra-nhan</c> de biet khi nao day duoc.
/// </para>
/// <para>
/// KHONG doan ho so bang CCCD hay so dien thoai. Do la dieu ADR 0018 cam: Thien
/// Nam co 345 nhom cung CCCD khac ten, va co so dien thoai gan toi 876 nguoi.
/// Viec noi ho so la cua NGUOI DUNG o man Noi ho so, khong phai cua duong API.
/// </para>
/// </summary>
public class DotKhamService : IDotKhamService
{
    /// <summary>Chan lo qua lon: mot nguoi vai chuc dot la binh thuong, vai nghin thi khong.</summary>
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

        // Khoa tra cuu la (co so, ma benh nhan) — dung rang buoc that cua
        // DM_BenhNhanCoSo. Khong tra theo con nguoi, vi mot nguoi tai MOT co so
        // van co the co nhieu ho so (17,3% o Thien Nam).
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

            // Ngay ra truoc ngay vao la du lieu hong — nhan vao roi man hien thi
            // se ra khoang thoi gian am.
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

        // MaBN o model van la string khong-null: script 05 moi chi NOI rang buoc
        // cho phep NULL, con du lieu thi chua dong nao NULL — viec don 18/21 ma
        // tu bia di cung dot xoa SinhMaBenhNhan(). Doi model sang string? phai
        // doi cung luc voi dot do.
        return await _db.BenhNhans.AsNoTracking()
            .Where(b => b.IdCoSo == idCoSo && ma.Contains(b.MaBN))
            .Select(b => b.MaBN)
            .Distinct()
            .ToListAsync();
    }
}
