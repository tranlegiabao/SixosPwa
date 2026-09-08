using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>Mot dong o man *Ho so cua toi*.</summary>
public sealed record HoSoCuaToi(
    long IdBenhNhan,
    string TenBN,
    string Cccd,
    DateTime? NgaySinh,
    string? Sdt,
    bool DaNoiHIS,
    bool DangChon,
    bool XoaDuoc);

public interface IHoSoBenhNhanService
{
    Task<long?> LayIdTaiKhoanAsync(string dinhDanh);

    Task<List<HoSoCuaToi>> LayDanhSachAsync(long idTaiKhoan, long? idDangChon);

    Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, long idTaiKhoan);

    Task<(bool ThanhCong, string ThongBao)> XoaAsync(long idBenhNhan, long idTaiKhoan);

    Task<(bool ThanhCong, string ThongBao, long IdBenhNhan)> TaoAsync(
        long idTaiKhoan, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh, string? sdt);
}

/// <summary>
/// *Ho so cua toi* — mot tai khoan quan nhieu con nguoi (ADR 0019).
///
/// <para>
/// Bam khuon <c>DangKyOnlineUB/QL_HoSoBenhNhanServices</c> dang chay that: liet
/// ke ho so cua tai khoan, cho tu tao, va doi ho so bang cach PHAT LAI claim.
/// </para>
/// </summary>
public class HoSoBenhNhanService : IHoSoBenhNhanService
{
    private readonly ApplicationDbContext _db;
    private readonly AdminStoredProcedureService _thuTuc;

    public HoSoBenhNhanService(ApplicationDbContext db, AdminStoredProcedureService thuTuc)
    {
        _db = db;
        _thuTuc = thuTuc;
    }

    public Task<long?> LayIdTaiKhoanAsync(string dinhDanh) =>
        _db.TaiKhoans.AsNoTracking()
            .Where(t => t.SDT == dinhDanh || t.Email == dinhDanh)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Cac con nguoi tai khoan nay dang quan.
    ///
    /// <para>
    /// 🔴 Loc bang <c>IdTaiKhoan</c>, KHONG bang so dien thoai cua con nguoi.
    /// Ho so cua me do con tao thi mang so cua me (hoac khong co so nao) —
    /// loc theo so dien thoai la chinh nhung ho so can quan nhat lai bien mat.
    /// </para>
    /// <para>
    /// Nhanh thu hai la duong LUI cho du lieu chua kip do sang cot moi. Script 09
    /// do het mot lan, nhung phien dang song van phai chay dung.
    /// </para>
    /// </summary>
    public async Task<List<HoSoCuaToi>> LayDanhSachAsync(long idTaiKhoan, long? idDangChon)
    {
        var sdtTaiKhoan = await _db.TaiKhoans.AsNoTracking()
            .Where(t => t.Id == idTaiKhoan)
            .Select(t => t.SDT)
            .FirstOrDefaultAsync();

        var nguoi = await _db.BenhNhans.AsNoTracking()
            .Where(p => p.IdTaiKhoan == idTaiKhoan
                        || (p.IdTaiKhoan == null && sdtTaiKhoan != null && p.SDT == sdtTaiKhoan))
            .OrderBy(p => p.Id)
            .ToListAsync();

        if (nguoi.Count == 0) return new List<HoSoCuaToi>();

        var id = nguoi.Select(p => p.Id).ToList();

        // Da noi HIS = co it nhat mot ho so mang MA THAT do co so cap.
        var daNoi = await _db.BenhNhanCoSos.AsNoTracking()
            .Where(h => id.Contains(h.IdBenhNhan) && h.MaBN != null)
            .Select(h => h.IdBenhNhan)
            .Distinct()
            .ToListAsync();

        // Co du lieu kham roi thi khong cho xoa — xoa la mat du lieu y te that.
        var coTaiLieu = await (
            from t in _db.TaiLieuBenhNhans.AsNoTracking()
            join h in _db.BenhNhanCoSos.AsNoTracking() on t.IdBenhNhanCoSo equals h.Id
            where id.Contains(h.IdBenhNhan)
            select h.IdBenhNhan).Distinct().ToListAsync();

        var coDotKham = await (
            from d in _db.DotKhams.AsNoTracking()
            join h in _db.BenhNhanCoSos.AsNoTracking() on d.IdBenhNhanCoSo equals h.Id
            where id.Contains(h.IdBenhNhan)
            select h.IdBenhNhan).Distinct().ToListAsync();

        return nguoi.Select(p => new HoSoCuaToi(
            IdBenhNhan: p.Id,
            TenBN: p.TenBN,
            Cccd: p.CCCD,
            NgaySinh: p.NgaySinh,
            Sdt: p.SDT,
            DaNoiHIS: daNoi.Contains(p.Id),
            DangChon: idDangChon == p.Id,
            XoaDuoc: !daNoi.Contains(p.Id)
                     && !coTaiLieu.Contains(p.Id)
                     && !coDotKham.Contains(p.Id))).ToList();
    }

    /// <summary>
    /// 🔴 Cong chan truoc khi phat lai claim *ho so dang chon*. Thieu phep kiem
    /// nay thi go ID ho so nguoi khac vao la xem duoc benh an cua ho — dung loai
    /// lo hong ma duong doc tai lieu tung mac.
    /// </summary>
    public Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, long idTaiKhoan) =>
        _db.BenhNhans.AsNoTracking()
            .AnyAsync(p => p.Id == idBenhNhan && p.IdTaiKhoan == idTaiKhoan);

    /// <summary>
    /// Nguoi dung TU GO tao mot ho so moi (ADR 0019 muc 1) — con dang ky ho me,
    /// khong doi co so phai co san nguoi do.
    ///
    /// <para>
    /// Ho so sinh ra la *tu khai*: <c>MaBN</c> rong vi co so chua cap ma nao.
    /// No thanh *da noi HIS* khi nguoi dung go dung Ma BN o man Noi ho so.
    /// </para>
    /// <para>
    /// "Ai khai truoc giu CCCD": thu tuc tra <c>ResultCode 3</c> kem duong ra khi
    /// CCCD da thuoc tai khoan khac. Loi do hien nguyen van cho nguoi dung —
    /// no la loi CO ICH, khong duoc nuot.
    /// </para>
    /// </summary>
    public async Task<(bool ThanhCong, string ThongBao, long IdBenhNhan)> TaoAsync(
        long idTaiKhoan, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh, string? sdt)
    {
        cccd = (cccd ?? "").Trim();
        hoTen = (hoTen ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cccd))
            return (false, "Chưa nhập số căn cước công dân.", 0);

        if (string.IsNullOrWhiteSpace(hoTen))
            return (false, "Chưa nhập họ tên.", 0);

        var (luu, idBenhNhan) = await _thuTuc.SaveBenhNhanAsync(
            cccd: cccd,
            tenBN: hoTen,
            sdt: string.IsNullOrWhiteSpace(sdt) ? null : sdt.Trim(),
            email: null,
            diaChi: null,
            idTaiKhoan: idTaiKhoan,
            ngaySinh: ngaySinh,
            hoTenKhongDau: ChuanHoaTen.BoDau(hoTen));

        if (!luu.Succeeded)
            return (false, luu.Message ?? "Không tạo được hồ sơ.", 0);

        // Gan ho so vao co so cua phien de no hien ngay o trang benh nhan. Khong
        // co ma co so (phien la thuong) thi van tao duoc CON NGUOI — ho so tai co
        // so se sinh khi nguoi dung mo trang cua co so do.
        if (!string.IsNullOrWhiteSpace(maCoSo))
        {
            var idCoSo = await _db.DMCSKCBs.AsNoTracking()
                .Where(c => c.MaCoSo == maCoSo)
                .Select(c => (long?)c.Id)
                .FirstOrDefaultAsync();

            if (idCoSo is not null)
                await _thuTuc.TaoHoSoTuKhaiAsync(idBenhNhan, idCoSo.Value);
        }

        return (true, "OK", idBenhNhan);
    }

    public async Task<(bool ThanhCong, string ThongBao)> XoaAsync(long idBenhNhan, long idTaiKhoan)
    {
        // Moi phep chan nam trong thu tuc (ADR 0008): khong phai ho so cua minh,
        // da noi HIS, hoac da co du lieu kham.
        var ketQua = await _thuTuc.XoaHoSoAsync(idBenhNhan, idTaiKhoan);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }
}
