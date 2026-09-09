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
    bool XoaDuoc,
    string? GioiTinh = null,
    int SoMaDaNoi = 0);

/// <summary>Mot ma da noi, hien trong man *Sua ho so* kem nut *Go noi*.</summary>
public sealed record MaDaNoi(long IdBenhNhanCoSo, string MaBN, DateTime? LanKhamCuoi);

/// <summary>Du lieu do vao man *Sua ho so*.</summary>
public sealed record HoSoDeSua(
    long IdBenhNhan,
    string TenBN,
    string Cccd,
    DateTime? NgaySinh,
    string? GioiTinh,
    string? Sdt,
    bool DaNoiHIS,
    List<MaDaNoi> DanhSachMa);

/// <summary>
/// Ket cuc cua mot lan LUU ho so — *Noi ho so* xay ra ngay trong do (ADR 0024).
/// Ba loi ra, va man phai noi duoc ca ba:
/// </summary>
public enum KetCucNoi
{
    /// <summary>Khong hoi HIS (co so chua noi), hoac hoi roi ma khong ai khop.</summary>
    KhongCoGi,

    /// <summary>*Tang 1* — khop du bon o voi CCCD hop le => da gan im lang.</summary>
    DaGanImLang,

    /// <summary>*Tang 2* — co ung vien nhung chua chac => cho benh nhan xac nhan tay.</summary>
    ChoXacNhan,

    /// <summary>Da noi nhung HIS khong tra loi luc nay.</summary>
    ChuaHoiDuocCoSo
}

public sealed record KetQuaLuuHoSo(
    bool ThanhCong,
    string ThongBao,
    long IdBenhNhan,
    KetCucNoi KetCuc = KetCucNoi.KhongCoGi,
    int SoMaVuaGan = 0,
    List<His.HoSoHis>? UngVien = null);

public interface IHoSoBenhNhanService
{
    Task<long?> LayIdTaiKhoanAsync(string dinhDanh);

    Task<List<HoSoCuaToi>> LayDanhSachAsync(long idTaiKhoan, long? idDangChon);

    Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, long idTaiKhoan);

    Task<(bool ThanhCong, string ThongBao)> XoaAsync(long idBenhNhan, long idTaiKhoan);

    Task<KetQuaLuuHoSo> TaoAsync(
        long idTaiKhoan, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh,
        string? sdt, string? gioiTinh);

    /// <summary>Do du lieu vao man *Sua ho so*; <c>null</c> khi ho so khong thuoc tai khoan.</summary>
    Task<HoSoDeSua?> LayDeSuaAsync(long idBenhNhan, long idTaiKhoan, string? maCoSo);

    Task<KetQuaLuuHoSo> SuaAsync(
        long idBenhNhan, long idTaiKhoan, string? maCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? sdt, string? gioiTinh);

    /// <summary>*Tang 2* — benh nhan tick nhung ma la cua minh roi bam nhan.</summary>
    Task<(bool ThanhCong, string ThongBao, int SoMaVuaGan)> XacNhanNoiAsync(
        long idBenhNhan, long idTaiKhoan, string? maCoSo, IEnumerable<string> maBN);

    Task<(bool ThanhCong, string ThongBao)> GoNoiAsync(long idBenhNhanCoSo, long idTaiKhoan);
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
    private readonly His.IHisDocService _his;
    private readonly ILogger<HoSoBenhNhanService> _logger;

    public HoSoBenhNhanService(ApplicationDbContext db,
                               AdminStoredProcedureService thuTuc,
                               His.IHisDocService his,
                               ILogger<HoSoBenhNhanService> logger)
    {
        _db = db;
        _thuTuc = thuTuc;
        _his = his;
        _logger = logger;
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
        // Dem luon SO MA: man *Ho so cua toi* hien con so o dong phu, con danh
        // sach ma va nut *Go noi* thi nam trong man *Sua ho so* (ADR 0024 ve 3) —
        // man nay de CHON NGUOI, khong phai de sua du lieu.
        var soMaTheoNguoi = await _db.BenhNhanCoSos.AsNoTracking()
            .Where(h => id.Contains(h.IdBenhNhan) && h.MaBN != null)
            .GroupBy(h => h.IdBenhNhan)
            .Select(nhom => new { Id = nhom.Key, So = nhom.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.So);

        var daNoi = soMaTheoNguoi.Keys.ToList();

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
                     && !coDotKham.Contains(p.Id),
            GioiTinh: p.GioiTinh,
            SoMaDaNoi: soMaTheoNguoi.TryGetValue(p.Id, out var so) ? so : 0)).ToList();
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
    /// 🔴 Tu Dot 4, LUU LA NOI (ADR 0024 ve 1): khong con nut *Noi ho so* nao tren
    /// giao dien. Benh nhan khong phai tu biet minh da tung kham o co so nay hay
    /// chua — do la cau chi HIS tra loi duoc.
    /// </para>
    /// <para>
    /// "Ai khai truoc giu CCCD": thu tuc tra <c>ResultCode 3</c> kem duong ra khi
    /// CCCD da thuoc tai khoan khac. Loi do hien nguyen van cho nguoi dung —
    /// no la loi CO ICH, khong duoc nuot.
    /// </para>
    /// </summary>
    public async Task<KetQuaLuuHoSo> TaoAsync(
        long idTaiKhoan, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh,
        string? sdt, string? gioiTinh)
    {
        cccd = (cccd ?? "").Trim();
        hoTen = (hoTen ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cccd))
            return new KetQuaLuuHoSo(false, "Chưa nhập số căn cước công dân.", 0);

        if (string.IsNullOrWhiteSpace(hoTen))
            return new KetQuaLuuHoSo(false, "Chưa nhập họ tên.", 0);

        var (luu, idBenhNhan) = await _thuTuc.SaveBenhNhanAsync(
            cccd: cccd,
            tenBN: hoTen,
            sdt: string.IsNullOrWhiteSpace(sdt) ? null : sdt.Trim(),
            email: null,
            diaChi: null,
            idTaiKhoan: idTaiKhoan,
            ngaySinh: ngaySinh,
            hoTenKhongDau: ChuanHoaTen.BoDau(hoTen),
            gioiTinh: string.IsNullOrWhiteSpace(gioiTinh) ? null : gioiTinh.Trim());

        if (!luu.Succeeded)
            return new KetQuaLuuHoSo(false, luu.Message ?? "Không tạo được hồ sơ.", 0);

        // Gan ho so vao co so cua phien de no hien ngay o trang benh nhan. Khong
        // co ma co so (phien la thuong) thi van tao duoc CON NGUOI — ho so tai co
        // so se sinh khi nguoi dung mo trang cua co so do.
        var idCoSo = await LayIdCoSoAsync(maCoSo);

        if (idCoSo is not null)
            await _thuTuc.TaoHoSoTuKhaiAsync(idBenhNhan, idCoSo.Value);

        var noi = await NoiKhiLuuAsync(idBenhNhan, idCoSo, cccd, hoTen, ngaySinh, gioiTinh);

        return noi with { ThanhCong = true, IdBenhNhan = idBenhNhan };
    }

    public async Task<HoSoDeSua?> LayDeSuaAsync(long idBenhNhan, long idTaiKhoan, string? maCoSo)
    {
        var nguoi = await _db.BenhNhans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == idBenhNhan && p.IdTaiKhoan == idTaiKhoan);

        if (nguoi is null) return null;

        var idCoSo = await LayIdCoSoAsync(maCoSo);

        // Chi liet ke ma TAI CO SO DANG XEM. Ma o co so khac khong hien o day: man
        // nay noi ve quan he giua nguoi nay va CO SO NAY, tron vao la nguoi dung
        // khong hieu minh dang go cai gi.
        var danhSachMa = idCoSo is null
            ? new List<MaDaNoi>()
            : await (from h in _db.BenhNhanCoSos.AsNoTracking()
                     where h.IdBenhNhan == idBenhNhan && h.IdCoSo == idCoSo.Value && h.MaBN != null
                     orderby h.Id
                     select new MaDaNoi(
                         h.Id,
                         h.MaBN!,
                         _db.DotKhams.Where(d => d.IdBenhNhanCoSo == h.Id)
                                     .Max(d => (DateTime?)d.NgayGioVao)))
                    .ToListAsync();

        return new HoSoDeSua(
            IdBenhNhan: nguoi.Id,
            TenBN: nguoi.TenBN,
            Cccd: nguoi.CCCD,
            NgaySinh: nguoi.NgaySinh,
            GioiTinh: nguoi.GioiTinh,
            Sdt: nguoi.SDT,
            DaNoiHIS: danhSachMa.Count > 0,
            DanhSachMa: danhSachMa);
    }

    /// <summary>
    /// Man *Sua ho so* — thu duy nhat cuu duoc nhom ho so CHINH CHU (14/19 tai
    /// khoan dang mang ten la SO DIEN THOAI): ho so cua ho tu de ra luc dang ky
    /// bang OTP, khong bao gio di qua man *Them ho so*.
    /// </summary>
    public async Task<KetQuaLuuHoSo> SuaAsync(
        long idBenhNhan, long idTaiKhoan, string? maCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? sdt, string? gioiTinh)
    {
        cccd = (cccd ?? "").Trim();
        hoTen = (hoTen ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cccd))
            return new KetQuaLuuHoSo(false, "Chưa nhập số căn cước công dân.", idBenhNhan);

        if (string.IsNullOrWhiteSpace(hoTen))
            return new KetQuaLuuHoSo(false, "Chưa nhập họ tên.", idBenhNhan);

        var ketQua = await _thuTuc.SuaHoSoAsync(
            idBenhNhan: idBenhNhan,
            idTaiKhoan: idTaiKhoan,
            cccd: cccd,
            tenBN: hoTen,
            sdt: string.IsNullOrWhiteSpace(sdt) ? null : sdt.Trim(),
            ngaySinh: ngaySinh,
            hoTenKhongDau: ChuanHoaTen.BoDau(hoTen),
            gioiTinh: string.IsNullOrWhiteSpace(gioiTinh) ? null : gioiTinh.Trim());

        if (!ketQua.Succeeded)
            return new KetQuaLuuHoSo(false, ketQua.Message ?? "Không lưu được hồ sơ.", idBenhNhan);

        var idCoSo = await LayIdCoSoAsync(maCoSo);

        // Ho so chua co cho dung tai co so dang xem thi tao dong tu khai truoc —
        // khong thi noi xong khong co dong nao de gan ma vao.
        if (idCoSo is not null
            && !await _db.BenhNhanCoSos.AnyAsync(h => h.IdBenhNhan == idBenhNhan && h.IdCoSo == idCoSo.Value))
        {
            await _thuTuc.TaoHoSoTuKhaiAsync(idBenhNhan, idCoSo.Value);
        }

        var noi = await NoiKhiLuuAsync(idBenhNhan, idCoSo, cccd, hoTen, ngaySinh, gioiTinh);

        return noi with { ThanhCong = true, IdBenhNhan = idBenhNhan };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // *Noi ho so* — hai tang (ADR 0024 ve 2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hoi HIS mot lan roi chia hai loi ra:
    /// <list type="bullet">
    ///   <item><b>Tang 1</b> — dong co <c>cccdKhop</c> (HIS da xac nhan CCCD ben goi
    ///   HOP LE va TRUNG KHIT) => gan im lang.</item>
    ///   <item><b>Tang 2</b> — con lai => tra ve cho man hien danh sach, KHONG gan
    ///   gi cho toi khi co nguoi bam.</item>
    /// </list>
    /// Ranh gioi dat dung cho <b>348 nhom</b> trung ho ten + ngay sinh + gioi tinh
    /// ma CCCD hop le KHAC NHAU: moi ca ay roi tang 2.
    ///
    /// <para>
    /// 🔴 Khong bao gio lam that bai ca lan LUU. Ho so da luu xong roi; noi duoc
    /// hay khong la chuyen sau do. HIS chet ma keo theo "khong sua duoc ten" thi
    /// dung la lay cai phu de pha cai chinh.
    /// </para>
    /// </summary>
    private async Task<KetQuaLuuHoSo> NoiKhiLuuAsync(
        long idBenhNhan, long? idCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? gioiTinh)
    {
        if (idCoSo is null || ngaySinh is null || string.IsNullOrWhiteSpace(gioiTinh))
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        var traLoi = await _his.TraCuuHoSoAsync(idCoSo.Value, cccd, hoTen, ngaySinh.Value, gioiTinh);

        if (traLoi.TrangThai == His.TrangThaiHoiHis.ChuaNoi)
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        if (traLoi.TrangThai == His.TrangThaiHoiHis.KhongHoiDuoc)
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.ChuaHoiDuocCoSo);

        var ungVien = (traLoi.DuLieu ?? new List<His.HoSoHis>())
            .Where(x => !string.IsNullOrWhiteSpace(x.MaBN))
            .ToList();

        if (ungVien.Count == 0)
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        // Bo nhung ma DA thuoc ve chinh ho so nay — ca hai tang deu bo, khong thi
        // tang 2 hoi lai nguoi dung ve thu ho da nhan roi, con tang 1 dem thua.
        var daCo = await _db.BenhNhanCoSos.AsNoTracking()
            .Where(h => h.IdBenhNhan == idBenhNhan && h.IdCoSo == idCoSo.Value && h.MaBN != null)
            .Select(h => h.MaBN!)
            .ToListAsync();

        var conLai = ungVien.Where(x => !daCo.Contains(x.MaBN!, StringComparer.Ordinal)).ToList();

        if (conLai.Count == 0)
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        var chacChan = conLai.Where(x => x.CccdKhop).ToList();

        if (chacChan.Count > 0)
        {
            var so = await GanMaAsync(idBenhNhan, idCoSo.Value, chacChan.Select(x => x.MaBN!));
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.DaGanImLang, so);
        }

        return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.ChoXacNhan, 0, conLai);
    }

    public async Task<(bool ThanhCong, string ThongBao, int SoMaVuaGan)> XacNhanNoiAsync(
        long idBenhNhan, long idTaiKhoan, string? maCoSo, IEnumerable<string> maBN)
    {
        // 🔴 Cong chan. Danh sach ma di qua trinh duyet nen khong tin duoc: thieu
        // phep nay thi go ID ho so nguoi khac vao la gan ma vao ho so cua ho.
        if (!await HoSoThuocTaiKhoanAsync(idBenhNhan, idTaiKhoan))
            return (false, "Hồ sơ này không thuộc tài khoản của bạn.", 0);

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null)
            return (false, "Chưa xác định được cơ sở.", 0);

        var ma = (maBN ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ma.Count == 0) return (true, "OK", 0);

        var so = await GanMaAsync(idBenhNhan, idCoSo.Value, ma);
        return (true, "OK", so);
    }

    public async Task<(bool ThanhCong, string ThongBao)> GoNoiAsync(long idBenhNhanCoSo, long idTaiKhoan)
    {
        // Moi phep chan nam trong thu tuc: khong phai ho so cua minh, hoac dong
        // do von la ho so tu khai (khong co ma nao de go).
        var ketQua = await _thuTuc.GoNoiAsync(idBenhNhanCoSo, idTaiKhoan);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }

    /// <summary>
    /// Gan mot loat ma vao ho so, bo qua ma da co. Tra ve so ma THUC SU vua gan.
    ///
    /// 🔴 Mot ma da co nguoi khac nhan thi thu tuc tu tu choi (unique co loc tren
    /// <c>(IDCoSo, MaBN)</c>) — nuot loi do va di tiep, khong dung ca me. Ai khai
    /// truoc giu, va nguoi thu hai khong duoc biet ma do dang thuoc ve ai.
    /// </summary>
    private async Task<int> GanMaAsync(long idBenhNhan, long idCoSo, IEnumerable<string> maBN)
    {
        var daCo = await _db.BenhNhanCoSos.AsNoTracking()
            .Where(h => h.IdCoSo == idCoSo && h.MaBN != null)
            .Select(h => h.MaBN!)
            .ToListAsync();

        int so = 0;

        foreach (var ma in maBN.Where(x => !string.IsNullOrWhiteSpace(x))
                               .Select(x => x.Trim())
                               .Distinct(StringComparer.Ordinal))
        {
            if (daCo.Contains(ma, StringComparer.Ordinal)) continue;

            var (ketQua, _) = await _thuTuc.SaveBenhNhanCoSoAsync(idBenhNhan, idCoSo, ma);

            if (ketQua.Succeeded) so++;
            else _logger.LogInformation("Khong gan duoc ma {MaBN} vao ho so {IdBenhNhan}: {ThongDiep}",
                                        ma, idBenhNhan, ketQua.Message);
        }

        return so;
    }

    private Task<long?> LayIdCoSoAsync(string? maCoSo) =>
        string.IsNullOrWhiteSpace(maCoSo)
            ? Task.FromResult<long?>(null)
            : _db.DMCSKCBs.AsNoTracking()
                  .Where(c => c.MaCoSo == maCoSo)
                  .Select(c => (long?)c.Id)
                  .FirstOrDefaultAsync();

    public async Task<(bool ThanhCong, string ThongBao)> XoaAsync(long idBenhNhan, long idTaiKhoan)
    {
        // Moi phep chan nam trong thu tuc (ADR 0008): khong phai ho so cua minh,
        // da noi HIS, hoac da co du lieu kham.
        var ketQua = await _thuTuc.XoaHoSoAsync(idBenhNhan, idTaiKhoan);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }
}
