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
public sealed record MaDaNoi(long IdBenhNhan, string MaBN, DateTime? LanKhamCuoi);

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
    /// <summary>
    /// 🔴 Tu dot 1B khong con tra ID tai khoan (benh nhan khong con tai khoan).
    /// Tra <c>true</c> khi so nay CO LOI VAO tai co so dang xem — dung luat C7b,
    /// cung cau hoi ma man dang nhap hoi. Xem ADR 0027 (da dao) va ADR 0034.
    /// </summary>
    Task<bool> CoLoiVaoAsync(string sdt, string? maCoSo);

    Task<List<HoSoCuaToi>> LayDanhSachAsync(string sdt, string? maCoSo, string? cccdPhien, long? idDangChon);

    /// <summary>
    /// CHI DUNG CHO LUONG QUET QR PHIEU KHAM. Khi <c>MOT_HO_SO</c> bat ma quet xong
    /// van chua truy ra ho so (khong co MaBN o co so, CCCD khong khop), tra ve ho so
    /// ma man *Ho so cua toi* se hien — de mo san bang claim <c>HoSoDangChon</c> va
    /// vao thang <c>/benh-nhan</c>, khong bat nguoi quet chon lai.
    ///
    /// <para>
    /// Tra <c>null</c> khi <c>MOT_HO_SO</c> TAT: luc do tai khoan duoc phep giu nhieu
    /// ho so nen phai de nguoi dung tu chon — lay bua mot cai la bug tham lang.
    /// </para>
    /// <para>
    /// Dung DUNG MOT luat chon voi <see cref="LayDanhSachAsync"/>: hai noi lech luat
    /// thi man *Ho so cua toi* hien mot nguoi con <c>/benh-nhan</c> doc du lieu cua
    /// nguoi khac.
    /// </para>
    /// </summary>
    Task<long?> LayIdHoSoMoSanKhiQuetAsync(string? sdt, string? maCoSo, string? cccdPhien);

    Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, string sdt, string? maCoSo);

    Task<(bool ThanhCong, string ThongBao)> XoaAsync(long idBenhNhan, string sdt, string? maCoSo);

    Task<KetQuaLuuHoSo> TaoAsync(
        string sdtPhien, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh,
        string? sdt, string? gioiTinh);

    /// <summary>Do du lieu vao man *Sua ho so*; <c>null</c> khi ho so khong thuoc tai khoan.</summary>
    Task<HoSoDeSua?> LayDeSuaAsync(long idBenhNhan, string sdt, string? maCoSo);

    Task<KetQuaLuuHoSo> SuaAsync(
        long idBenhNhan, string sdtPhien, string? maCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? sdt, string? gioiTinh);

    /// <summary>*Tang 2* — benh nhan chon DUNG MOT ma la cua minh roi bam nhan.</summary>
    Task<(bool ThanhCong, string ThongBao, int SoMaVuaGan)> XacNhanNoiAsync(
        long idBenhNhan, string sdtPhien, string? maCoSo, string? maBN);

    /// <summary>
    /// Trong danh sach ma dua vao, ma nao DA co ho so khac tai co so nay nhan.
    /// Man *Sua ho so* dung de KHOA nhung ma do lai: nhan lai la doi benh an cua
    /// nguoi khac, phai qua Support thao ra truoc.
    /// </summary>
    Task<List<string>> LayMaDaCoChuAsync(
        long idBenhNhan, string? maCoSo, IEnumerable<string> maBN);

    Task<(bool ThanhCong, string ThongBao)> GoNoiAsync(long idBenhNhan, string sdt, string? maCoSo);
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
    private readonly IHTConfigService _config;
    private readonly ILogger<HoSoBenhNhanService> _logger;

    public HoSoBenhNhanService(ApplicationDbContext db,
                               AdminStoredProcedureService thuTuc,
                               His.IHisDocService his,
                               IHTConfigService config,
                               ILogger<HoSoBenhNhanService> logger)
    {
        _db = db;
        _thuTuc = thuTuc;
        _his = his;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Luat C7b: co loi vao = co dong <c>DM_BenhNhan</c> mang so nay TAI CO SO NAY.
    /// Thay cho phep hoi <c>HT_TaiKhoan</c> cu (ADR 0027 da dao).
    /// </summary>
    public async Task<bool> CoLoiVaoAsync(string sdt, string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(sdt)) return false;

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return false;

        // 🔴 Khop ca Email: moi cau doc khac trong cong (man tai lieu, DemHoSoTaiCoSo,
        // TaiLieuApiController) deu khop `SDT == dinhDanh || Email == dinhDanh`.
        // Chi khop SDT o rieng cong chan nay la nguoi dang nhap bang EMAIL bi tu
        // choi o cua, trong khi cac man khac van hien du lieu cua ho.
        return await _db.BenhNhans.AsNoTracking()
            .AnyAsync(p => (p.SDT == sdt || p.Email == sdt) && p.IdCoSo == idCoSo.Value);
    }

    /// <summary>
    /// Cac ho so ma so dien thoai nay dang giu TAI CO SO DANG XEM (luat C2).
    ///
    /// <para>
    /// 🔴 Pham vi la cap <c>(SDT, IdCoSo)</c>, khong con la <c>IdTaiKhoan</c>.
    /// Moi nguoi hien dung MOT lan — truoc 1B mot nguoi kham N co so thi ra N dong
    /// trung ten.
    /// </para>
    /// <para>
    /// 🔴 Luon loc <c>IdCoSo != null</c>: dong neo la trang thai qua do, khong
    /// duoc hien o man nao (tieu chi nghiem thu §10 muc 10).
    /// </para>
    /// <para>
    /// Toggle <c>MOT_HO_SO</c> (C4/C5-R2): bat thi chi tra DUNG MOT ho so, chon theo
    /// thu tu <c>idDangChon</c> -> CCCD cua phien -> dong dau. Khong con dung dau o
    /// CCCD: dang nhap OTP thuong khong mang CCCD, va bam *Chon* cung khong doi claim
    /// Cccd, nen lay CCCD lam tieu chi dau la tra sai nguoi. Van khong CHAN khi ca ba
    /// deu truot — chan o day la khoa chet nguoi dung that.
    /// </para>
    /// </summary>
    public async Task<List<HoSoCuaToi>> LayDanhSachAsync(
        string sdt, string? maCoSo, string? cccdPhien, long? idDangChon)
    {
        if (string.IsNullOrWhiteSpace(sdt)) return new List<HoSoCuaToi>();

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return new List<HoSoCuaToi>();

        var nguoi = await _db.BenhNhans.AsNoTracking()
            .Where(p => (p.SDT == sdt || p.Email == sdt) && p.IdCoSo == idCoSo.Value)
            .OrderBy(p => p.Id)
            .ToListAsync();

        if (nguoi.Count == 0) return new List<HoSoCuaToi>();

        if (await _config.KiemTraHieuLucAsync("MOT_HO_SO") && nguoi.Count > 1)
        {
            // 🔴 Ho so DANG CHON di truoc CCCD cua phien. Thieu ve nay thi hai duong
            // vao deu tra sai nguoi: (1) dang nhap OTP thuong khong mang CCCD => roi
            // xuong nguoi[0] tuc ho so co Id nho nhat, khong lien quan gi toi nguoi
            // dang dung; (2) bam *Chon* sang ho so khac thi PhatLaiClaimAsync chi thay
            // claim HoSoDangChon va GIU NGUYEN claim Cccd cu => man nay hien mot ho so
            // trong khi /benh-nhan doc du lieu cua ho so khac.
            nguoi = new List<BenhNhan> { ChonMotHoSo(nguoi, cccdPhien, idDangChon) };
        }

        var id = nguoi.Select(p => p.Id).ToList();

        // Co du lieu kham roi thi khong cho xoa — xoa la mat du lieu y te that.
        var coTaiLieu = await _db.TaiLieuBenhNhans.AsNoTracking()
            .Where(t => t.IdBenhNhan != null && id.Contains(t.IdBenhNhan.Value))
            .Select(t => t.IdBenhNhan!.Value).Distinct().ToListAsync();

        var coDotKham = await _db.DotKhams.AsNoTracking()
            .Where(d => id.Contains(d.IdBenhNhan))
            .Select(d => d.IdBenhNhan).Distinct().ToListAsync();

        return nguoi.Select(p => new HoSoCuaToi(
            IdBenhNhan: p.Id,
            TenBN: p.TenBN,
            Cccd: p.CCCD,
            NgaySinh: p.NgaySinh,
            Sdt: p.SDT,
            DaNoiHIS: p.MaBN != null,
            DangChon: idDangChon == p.Id,
            XoaDuoc: p.MaBN == null
                     && !coTaiLieu.Contains(p.Id)
                     && !coDotKham.Contains(p.Id),
            GioiTinh: p.GioiTinh,
            // Mot dong = mot ho so tai mot co so => nhieu nhat mot ma (ADR 0032).
            SoMaDaNoi: p.MaBN != null ? 1 : 0)).ToList();
    }

    /// <summary>
    /// 🔴 Cong chan truoc khi phat lai claim *ho so dang chon*. Thieu phep kiem
    /// nay thi go ID ho so nguoi khac vao la xem duoc benh an cua ho — dung loai
    /// lo hong ma duong doc tai lieu tung mac.
    /// </summary>
    public async Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, string sdt, string? maCoSo)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null || string.IsNullOrWhiteSpace(sdt)) return false;

        return await _db.BenhNhans.AsNoTracking()
            .AnyAsync(p => p.Id == idBenhNhan && p.SDT == sdt && p.IdCoSo == idCoSo.Value);
    }

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
        string sdtPhien, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh,
        string? sdt, string? gioiTinh)
    {
        cccd = (cccd ?? "").Trim();
        hoTen = (hoTen ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cccd))
            return new KetQuaLuuHoSo(false, "Chưa nhập số căn cước công dân.", 0);

        if (string.IsNullOrWhiteSpace(hoTen))
            return new KetQuaLuuHoSo(false, "Chưa nhập họ tên.", 0);

        // 🔴 Cung luat voi SuaAsync: SDT la thu dung de DANG NHAP nen BAT BUOC. Bat
        // o mot cua ma tha o cua kia thi khong phai la bat buoc — nguoi dung tao ho
        // so khong so, roi den lan sua dau tien moi bi chan.
        if (string.IsNullOrWhiteSpace(sdt))
            return new KetQuaLuuHoSo(false, "Chưa nhập số điện thoại.", 0);

        if (LaMaGia(cccd) && (!ngaySinh.HasValue || ngaySinh.Value.Date == new DateTime(1900, 1, 1)))
            return new KetQuaLuuHoSo(false, "Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân.", 0);

        var (luu, idBenhNhan) = await _thuTuc.SaveBenhNhanAsync(
            cccd: cccd,
            tenBN: hoTen,
            sdt: string.IsNullOrWhiteSpace(sdt) ? null : sdt.Trim(),
            email: null,
            diaChi: null,
            idTaiKhoan: null,   // hop dong giu tham so, stored nhan-roi-bo (ADR 0035 #1)
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

    public async Task<HoSoDeSua?> LayDeSuaAsync(long idBenhNhan, string sdt, string? maCoSo)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo);

        var nguoi = idCoSo is null ? null : await _db.BenhNhans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == idBenhNhan && p.SDT == sdt && p.IdCoSo == idCoSo.Value);

        if (nguoi is null) return null;

        // Chi liet ke ma TAI CO SO DANG XEM. Ma o co so khac khong hien o day: man
        // nay noi ve quan he giua nguoi nay va CO SO NAY, tron vao la nguoi dung
        // khong hieu minh dang go cai gi.
        // Mot dong = mot ho so tai mot co so => nhieu nhat MOT ma (ADR 0032).
        // Giu kieu danh sach de man *Sua ho so* khong phai viet lai.
        var danhSachMa = nguoi.MaBN is null
            ? new List<MaDaNoi>()
            : new List<MaDaNoi>
              {
                  new(nguoi.Id, nguoi.MaBN,
                      await _db.DotKhams.AsNoTracking()
                          .Where(d => d.IdBenhNhan == nguoi.Id)
                          .MaxAsync(d => (DateTime?)d.NgayGioVao))
              };

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
        long idBenhNhan, string sdtPhien, string? maCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? sdt, string? gioiTinh)
    {
        cccd = (cccd ?? "").Trim();
        hoTen = (hoTen ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cccd))
            return new KetQuaLuuHoSo(false, "Chưa nhập số căn cước công dân.", idBenhNhan);

        if (string.IsNullOrWhiteSpace(hoTen))
            return new KetQuaLuuHoSo(false, "Chưa nhập họ tên.", idBenhNhan);

        // 🔴 SDT BAT BUOC: no la thu dung de DANG NHAP vao cong. Ho so khong co so
        // thi den luc can vao lai la khong con duong nao. Chan o day chu khong chi
        // dat `required` tren o input — thuoc tinh do go bang DevTools la xong.
        if (string.IsNullOrWhiteSpace(sdt))
            return new KetQuaLuuHoSo(false, "Chưa nhập số điện thoại.", idBenhNhan);

        if (LaMaGia(cccd) && (!ngaySinh.HasValue || ngaySinh.Value.Date == new DateTime(1900, 1, 1)))
            return new KetQuaLuuHoSo(false, "Ngày sinh không hợp lệ. Vui lòng nhập ngày sinh chính xác của bệnh nhân.", idBenhNhan);

        var idCoSoPhien = await LayIdCoSoAsync(maCoSo);
        if (idCoSoPhien is null)
            return new KetQuaLuuHoSo(false, "Chưa xác định được cơ sở.", idBenhNhan);

        var ketQua = await _thuTuc.SuaHoSoAsync(
            idBenhNhan: idBenhNhan,
            sdtPhien: sdtPhien,
            idCoSo: idCoSoPhien.Value,
            cccd: cccd,
            tenBN: hoTen,
            sdt: string.IsNullOrWhiteSpace(sdt) ? null : sdt.Trim(),
            ngaySinh: ngaySinh,
            hoTenKhongDau: ChuanHoaTen.BoDau(hoTen),
            gioiTinh: string.IsNullOrWhiteSpace(gioiTinh) ? null : gioiTinh.Trim());

        if (!ketQua.Succeeded)
            return new KetQuaLuuHoSo(false, ketQua.Message ?? "Không lưu được hồ sơ.", idBenhNhan);

        // Dong da la ho so TAI CO SO roi (KEEP-ID, dot 1B) nen khong con buoc
        // "tao dong tu khai" nao o day nua.
        var idCoSo = idCoSoPhien;

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

        // 🔴 DA NOI ROI THI DUNG LAI — khong hoi HIS, khong doi gi. Mot ho so giu
        // DUNG MOT ma (luat nghiep vu chot 09/09: MaBN la danh tinh cua benh nhan
        // tai co so, nhieu ma cung mot nguoi la du lieu nhap lon). Truoc day moi
        // lan LUU deu tra lai roi ghi de, nen ma dang noi TU DOI sang ma khac ma
        // khong mot dau hieu nao — do la doi benh an cua nguoi ta sau lung ho.
        // Muon doi thi bam *Go noi* truoc, dung nhu man *Sua ho so* dang huong dan.
        var daNoiMa = await _db.BenhNhans.AsNoTracking()
            .AnyAsync(h => h.Id == idBenhNhan
                        && h.IdCoSo == idCoSo.Value
                        && h.MaBN != null);

        if (daNoiMa)
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        // 🔴 CHAN HAN duong noi cho ho so mang ma gia "khong co can cuoc" (chot user
        // 10/09, ADR 0028). Thoat TRUOC khi hoi HIS, co y:
        //   * khong hoi thi khong co danh sach ung vien nao de lo. Ke go ho ten +
        //     ngay sinh + gioi tinh cua nguoi khac se khong thay gi het — ba o do in
        //     tren moi toa thuoc nen chung KHONG phai bang chung danh tinh.
        //   * do that tren PKDK_ThienNam: 11.533 ho so khong co can cuoc, trong do
        //     2.717 nam trong 1.203 nhom trung ca ho ten + ngay sinh + gioi tinh.
        //     Noi theo ba o do la giao benh an cho nguoi trung ten.
        // Cai gia da biet va CHAP NHAN: nhom nay khong keo duoc benh an ve cong.
        // Muon mo lai thi phai co duong "go dung Ma BN" (SPWA_TraCuuTheoMaBN ben HIS
        // da san sang, co chan do 20 lan/15 phut) — chua lam.
        if (LaMaGia(cccd))
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

        // *Tang 1* chi con DUNG MOT truong hop: co so cap dung MOT ma, CCCD trung
        // khit, va ma do CHUA co ho so nao khac nhan. Ra tu HAI ma tro len la KHONG
        // duoc tu quyet, du CCCD khop het.
        // 🔴 Nhanh "ma gia cung duoc tu gan" (them 10/09) DA BI GO: ho so mang ma gia
        // khong con di toi day nua — bi chan tu tren, truoc ca luc hoi HIS.
        if (ungVien.Count == 1 && ungVien[0].CccdKhop)
        {
            // 🔴 CHI CCCD trung khit moi la yeu to xac thuc. Nhanh ma gia
            // (11111111111 / 111111111111) noi duoc nho khop ba o danh tinh CONG KHAI,
            // nen no KHONG duoc mo cua tai lieu: ai biet ho ten + ngay sinh + gioi tinh
            // cua nguoi khac cung go ra duoc. Ho van xem duoc tom tat dot kham (tang 1
            // cua ADR 0020), chi don thuoc + ket qua CLS la con dong.
            var so = await GanMotMaAsync(idBenhNhan, idCoSo.Value, ungVien[0].MaBN!,
                                         daXacThuc: ungVien[0].CccdKhop);

            // so = 0 nghia la ma da co chu. KHONG duoc bao "da noi" (man se in ra o
            // ma rong), cung KHONG duoc im lang: day xuong tang 2 de man khoa ma lai
            // va chi duong sang Support.
            if (so > 0)
                return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.DaGanImLang, so);
        }

        return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.ChoXacNhan, 0, ungVien);
    }

    public async Task<(bool ThanhCong, string ThongBao, int SoMaVuaGan)> XacNhanNoiAsync(
        long idBenhNhan, string sdtPhien, string? maCoSo, string? maBN)
    {
        // 🔴 Cong chan. Ma di qua trinh duyet nen khong tin duoc: thieu phep nay
        // thi go ID ho so nguoi khac vao la gan ma vao ho so cua ho.
        if (!await HoSoThuocTaiKhoanAsync(idBenhNhan, sdtPhien, maCoSo))
            return (false, "Hồ sơ này không thuộc tài khoản của bạn.", 0);

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null)
            return (false, "Chưa xác định được cơ sở.", 0);

        var ma = (maBN ?? string.Empty).Trim();

        if (ma.Length == 0) return (true, "OK", 0);

        // 🔴 Cong chan thu hai cua luat "ma gia thi khong noi" (ADR 0028). NoiKhiLuuAsync
        // da chan tu tren nen binh thuong khong ai toi day duoc voi ma gia — nhung
        // action nay POST tran duoc: giu lai mot form cu, hoac doi CCCD ho so thanh ma
        // gia sau khi danh sach ung vien da nam trong tay, la di vong duoc cong tren.
        var cccdHoSo = await _db.BenhNhans.AsNoTracking()
            .Where(x => x.Id == idBenhNhan)
            .Select(x => x.CCCD)
            .FirstOrDefaultAsync();

        if (LaMaGia(cccdHoSo))
            return (false, "Hồ sơ chưa có số căn cước nên không nối được bệnh án. Bạn vui lòng bổ sung số căn cước, hoặc liên hệ bộ phận hỗ trợ của cơ sở.", 0);

        // Cung cong chan voi NoiKhiLuuAsync: mot ho so DUNG MOT ma. Gui lai form
        // cu (nut Back, bam hai lan) khong duoc de doi ma dang noi.
        var daNoiMa = await _db.BenhNhans.AsNoTracking()
            .AnyAsync(h => h.Id == idBenhNhan
                        && h.IdCoSo == idCoSo.Value
                        && h.MaBN != null);

        if (daNoiMa)
            return (false, "Hồ sơ này đã nối một mã rồi. Muốn đổi thì gỡ nối trước.", 0);

        // Tang 2 giu nguyen hanh vi cu (mo cua) — siet cho nay la doi ca luong,
        // phai co duong "go dung Ma BN de mo" cua ADR 0020 truoc, khong thi khoa
        // chet nguoi dung that. Xem ADR 0028 muc "Con thieu".
        var so = await GanMotMaAsync(idBenhNhan, idCoSo.Value, ma, daXacThuc: true);

        // 🔴 Ma da co ho so khac nhan thi DUNG HAN o day — cong khong tu thao ra
        // duoc. Nhan lai la keo benh an dang thuoc ve nguoi khac sang minh; go nham
        // thi ca hai ben deu mat. Phai qua bo phan ho tro cua co so.
        return so > 0
            ? (true, "OK", so)
            : (false, "Mã " + ma + " đã có hồ sơ khác nhận. Bạn cần liên hệ bộ phận hỗ trợ "
                    + "của cơ sở để được gỡ, cổng không tự tháo được.", 0);
    }

    public async Task<List<string>> LayMaDaCoChuAsync(
        long idBenhNhan, string? maCoSo, IEnumerable<string> maBN)
    {
        var ma = (maBN ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ma.Count == 0) return new List<string>();

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return new List<string>();

        // "Chu" o day la ho so KHAC. Ma cua chinh ho so nay khong tinh la vuong.
        return await _db.BenhNhans.AsNoTracking()
            .Where(h => h.IdCoSo == idCoSo.Value
                     && h.MaBN != null
                     && h.Id != idBenhNhan
                     && ma.Contains(h.MaBN))
            .Select(h => h.MaBN!)
            .ToListAsync();
    }

    public async Task<(bool ThanhCong, string ThongBao)> GoNoiAsync(
        long idBenhNhan, string sdt, string? maCoSo)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return (false, "Chưa xác định được cơ sở.");

        // Moi phep chan nam trong thu tuc: khong phai ho so cua minh, hoac dong
        // do von la ho so tu khai (khong co ma nao de go).
        var ketQua = await _thuTuc.GoNoiAsync(idBenhNhan, sdt, idCoSo.Value);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }

    /// <summary>
    /// Gan DUNG MOT ma vao ho so. Tra <c>1</c> neu gan duoc, <c>0</c> neu khong.
    ///
    /// <para>
    /// 🔴 KHONG nhan danh sach, va do la co y. Thu tuc <c>DM_BenhNhanCoSo_Save</c>
    /// tra khoa theo <c>(IDBenhNhan, IDCoSo)</c> roi UPDATE, nen goi no nhieu lan
    /// cho cung mot ho so chi GHI DE mot dong: vong lap cu bao "da noi N ma" trong
    /// khi CSDL chi giu ma CUOI CUNG — mat im lang, do duoc ngay 09/09 (ma 111457
    /// bien mat khi 100992 duoc gan de len). Mot ho so &lt;-&gt; mot ma.
    /// </para>
    ///
    /// <para>
    /// 🔴 Ma da co nguoi khac nhan thi tu choi (unique co loc tren
    /// <c>(IDCoSo, MaBN)</c>). Chan o day de con thong bao tu te, nhung KHONG noi
    /// ma do dang thuoc ve ai.
    /// </para>
    /// </summary>
    /// <param name="daXacThuc">
    /// Lan noi nay CO mot yeu to chi dung nguoi moi co hay khong (hien tai: CCCD
    /// hop le va trung khit). Khop ho ten + ngay sinh + gioi tinh KHONG tinh — ba o
    /// do in tren moi toa thuoc, ai cung go duoc. Co nay quyet dinh *Cua tai lieu*
    /// (ADR 0020): khong co yeu to nao thi ho so van noi duoc va van xem duoc TOM TAT
    /// dot kham, nhung don thuoc + ket qua CLS thi dong.
    /// </param>
    /// <summary>
    /// Ma gia HIS dung de danh dau "khong co can cuoc". 🔴 Ho so mang ma nay
    /// KHONG BAO GIO duoc noi (chot 10/09) — xem <see cref="MaGiaKhongCanCuoc"/>.
    /// </summary>
    private static readonly string[] MaGiaKhongCanCuoc = { "11111111111", "111111111111" };

    /// <summary>Can cuoc nay that ra la dau "khong co can cuoc" chu khong phai so that.</summary>
    private static bool LaMaGia(string? cccd) =>
        MaGiaKhongCanCuoc.Contains((cccd ?? string.Empty).Trim(), StringComparer.Ordinal);

    private async Task<int> GanMotMaAsync(long idBenhNhan, long idCoSo, string maBN,
                                          bool daXacThuc)
    {
        var ma = (maBN ?? string.Empty).Trim();

        if (ma.Length == 0) return 0;

        var daCoChu = await _db.BenhNhans.AsNoTracking()
            .AnyAsync(h => h.IdCoSo == idCoSo && h.MaBN == ma && h.Id != idBenhNhan);

        if (daCoChu) return 0;

        var (ketQua, _) = await _thuTuc.SaveBenhNhanCoSoAsync(idBenhNhan, idCoSo, ma,
                                                              moCuaTaiLieu: daXacThuc);

        if (ketQua.Succeeded) return 1;

        _logger.LogInformation("Khong gan duoc ma {MaBN} vao ho so {IdBenhNhan}: {ThongDiep}",
                               ma, idBenhNhan, ketQua.Message);
        return 0;
    }

    /// <summary>
    /// Luat chon MOT ho so khi <c>MOT_HO_SO</c> bat — dung o CA HAI noi
    /// (<see cref="LayDanhSachAsync"/> va <see cref="LayIdHoSoMoSanKhiQuetAsync"/>)
    /// nen chi duoc viet MOT lan o day.
    ///
    /// <para>
    /// Thu tu: ho so DANG CHON -> CCCD cua phien -> dong dau. Ho so dang chon phai di
    /// truoc vi bam *Chon* khong doi claim Cccd (HoSoController.PhatLaiClaimAsync),
    /// lay CCCD lam tieu chi dau la tra ve nguoi vua bi chuyen khoi.
    /// </para>
    /// </summary>
    private static BenhNhan ChonMotHoSo(List<BenhNhan> nguoi, string? cccdPhien, long? idDangChon) =>
        nguoi.FirstOrDefault(p => idDangChon != null && p.Id == idDangChon.Value)
        ?? nguoi.FirstOrDefault(p => !string.IsNullOrWhiteSpace(cccdPhien)
                                  && string.Equals(p.CCCD, cccdPhien, StringComparison.Ordinal))
        ?? nguoi[0];

    /// <inheritdoc />
    public async Task<long?> LayIdHoSoMoSanKhiQuetAsync(string? sdt, string? maCoSo, string? cccdPhien)
    {
        if (string.IsNullOrWhiteSpace(sdt)) return null;

        // MOT_HO_SO tat => tai khoan duoc giu nhieu ho so, phai de nguoi dung tu chon.
        if (!await _config.KiemTraHieuLucAsync("MOT_HO_SO")) return null;

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return null;

        var nguoi = await _db.BenhNhans.AsNoTracking()
            .Where(p => (p.SDT == sdt || p.Email == sdt) && p.IdCoSo == idCoSo.Value)
            .OrderBy(p => p.Id)
            .ToListAsync();

        // Phien vua quet xong thi chua co claim HoSoDangChon => truyen null, luat lui
        // ve CCCD cua phieu vua quet.
        return nguoi.Count == 0 ? null : ChonMotHoSo(nguoi, cccdPhien, null).Id;
    }

    private Task<long?> LayIdCoSoAsync(string? maCoSo) =>
        string.IsNullOrWhiteSpace(maCoSo)
            ? Task.FromResult<long?>(null)
            : _db.DMCSKCBs.AsNoTracking()
                  .Where(c => c.MaCoSo == maCoSo)
                  .Select(c => (long?)c.Id)
                  .FirstOrDefaultAsync();

    public async Task<(bool ThanhCong, string ThongBao)> XoaAsync(
        long idBenhNhan, string sdt, string? maCoSo)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return (false, "Chưa xác định được cơ sở.");

        // Moi phep chan nam trong thu tuc (ADR 0008): khong phai ho so cua minh,
        // da noi HIS, hoac da co du lieu kham.
        var ketQua = await _thuTuc.XoaHoSoAsync(idBenhNhan, sdt, idCoSo.Value);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }
}
