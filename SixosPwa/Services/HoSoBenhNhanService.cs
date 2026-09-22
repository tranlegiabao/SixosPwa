using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;

namespace SixosPwa.Services;

/// <summary>Một dòng ở màn *Hồ sơ của tôi*.</summary>
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

/// <summary>Một mã đã nối, hiện trong màn *Sửa hồ sơ* kèm nút *Gỡ nối*.</summary>
public sealed record MaDaNoi(long IdBenhNhan, string MaBN, DateTime? LanKhamCuoi);

/// <summary>Dữ liệu đổ vào màn *Sửa hồ sơ*.</summary>
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
/// Kết cục của một lần LƯU hồ sơ — *Nối hồ sơ* xảy ra ngay trong đó (ADR 0024).
/// Ba lối ra, và màn phải nói được cả ba:
/// </summary>
public enum KetCucNoi
{
    /// <summary>Không hỏi HIS (cơ sở chưa nối), hoặc hỏi rồi mà không ai khớp.</summary>
    KhongCoGi,

    /// <summary>*Tầng 1* — khớp đủ bốn ô với CCCD hợp lệ => đã gán im lặng.</summary>
    DaGanImLang,

    /// <summary>*Tầng 2* — có ứng viên nhưng chưa chắc => chờ bệnh nhân xác nhận tay.</summary>
    ChoXacNhan,

    /// <summary>Đã nối nhưng HIS không trả lời lúc này.</summary>
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
    /// 🔴 Từ đợt 1B không còn trả ID tài khoản (bệnh nhân không còn tài khoản).
    /// Trả <c>true</c> khi số này CÓ LỐI VÀO tại cơ sở đang xem — đúng luật C7b,
    /// cùng câu hỏi mà màn đăng nhập hỏi. Xem ADR 0027 (đã đảo) và ADR 0040.
    /// </summary>
    Task<bool> CoLoiVaoAsync(string sdt, string? maCoSo);

    Task<List<HoSoCuaToi>> LayDanhSachAsync(string sdt, string? maCoSo, string? cccdPhien, long? idDangChon);

    /// <summary>
    /// CHỈ DÙNG CHO LUỒNG QUÉT QR PHIẾU KHÁM. Khi <c>MOT_HO_SO</c> bật mà quét xong
    /// vẫn chưa truy ra hồ sơ (không có MaBN ở cơ sở, CCCD không khớp), trả về hồ sơ
    /// mà màn *Hồ sơ của tôi* sẽ hiện — để mở sẵn bằng claim <c>HoSoDangChon</c> và
    /// vào thẳng <c>/benh-nhan</c>, không bắt người quét chọn lại.
    ///
    /// <para>
    /// Trả <c>null</c> khi <c>MOT_HO_SO</c> TẮT: lúc đó tài khoản được phép giữ nhiều
    /// hồ sơ nên phải để người dùng tự chọn — lấy bừa một cái là bug thầm lặng.
    /// </para>
    /// <para>
    /// Dùng ĐÚNG MỘT luật chọn với <see cref="LayDanhSachAsync"/>: hai nơi lệch luật
    /// thì màn *Hồ sơ của tôi* hiện một người còn <c>/benh-nhan</c> đọc dữ liệu của
    /// người khác.
    /// </para>
    /// </summary>
    Task<long?> LayIdHoSoMoSanKhiQuetAsync(string? sdt, string? maCoSo, string? cccdPhien);

    Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, string sdt, string? maCoSo);

    Task<(bool ThanhCong, string ThongBao)> XoaAsync(long idBenhNhan, string sdt, string? maCoSo);

    Task<KetQuaLuuHoSo> TaoAsync(
        string sdtPhien, string? maCoSo, string cccd, string hoTen, DateTime? ngaySinh,
        string? sdt, string? gioiTinh);

    /// <summary>Đổ dữ liệu vào màn *Sửa hồ sơ*; <c>null</c> khi hồ sơ không thuộc tài khoản.</summary>
    Task<HoSoDeSua?> LayDeSuaAsync(long idBenhNhan, string sdt, string? maCoSo);

    Task<KetQuaLuuHoSo> SuaAsync(
        long idBenhNhan, string sdtPhien, string? maCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? sdt, string? gioiTinh);

    /// <summary>*Tầng 2* — bệnh nhân chọn ĐÚNG MỘT mã là của mình rồi bấm nhận.</summary>
    Task<(bool ThanhCong, string ThongBao, int SoMaVuaGan)> XacNhanNoiAsync(
        long idBenhNhan, string sdtPhien, string? maCoSo, string? maBN);

    /// <summary>
    /// Trong danh sách mã đưa vào, mã nào ĐÃ có hồ sơ khác tại cơ sở này nhận.
    /// Màn *Sửa hồ sơ* dùng để KHÓA những mã đó lại: nhận lại là đổi bệnh án của
    /// người khác, phải qua Support tháo ra trước.
    /// </summary>
    Task<List<string>> LayMaDaCoChuAsync(
        long idBenhNhan, string? maCoSo, IEnumerable<string> maBN);

    Task<(bool ThanhCong, string ThongBao)> GoNoiAsync(long idBenhNhan, string sdt, string? maCoSo);
}

/// <summary>
/// *Hồ sơ của tôi* — một tài khoản quản nhiều con người (ADR 0019).
///
/// <para>
/// Bám khuôn <c>DangKyOnlineUB/QL_HoSoBenhNhanServices</c> đang chạy thật: liệt
/// kê hồ sơ của tài khoản, cho tự tạo, và đổi hồ sơ bằng cách PHÁT LẠI claim.
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
    /// Luật C7b: có lối vào = có dòng <c>DM_BenhNhan</c> mang số này TẠI CƠ SỞ NÀY.
    /// Thay cho phép hỏi <c>HT_TaiKhoan</c> cũ (ADR 0027 đã đảo).
    /// </summary>
    public async Task<bool> CoLoiVaoAsync(string sdt, string? maCoSo)
    {
        if (string.IsNullOrWhiteSpace(sdt)) return false;

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return false;

        // 🔴 Khớp cả Email: mọi câu đọc khác trong cổng (màn tài liệu, DemHoSoTaiCoSo,
        // TaiLieuApiController) đều khớp `SDT == dinhDanh || Email == dinhDanh`.
        // Chỉ khớp SDT ở riêng cổng chặn này là người đăng nhập bằng EMAIL bị từ
        // chối ở cửa, trong khi các màn khác vẫn hiện dữ liệu của họ.
        return await _db.BenhNhans.AsNoTracking()
            .AnyAsync(p => (p.SDT == sdt || p.Email == sdt) && p.IdCoSo == idCoSo.Value);
    }

    /// <summary>
    /// Các hồ sơ mà số điện thoại này đang giữ TẠI CƠ SỞ ĐANG XEM (luật C2).
    ///
    /// <para>
    /// 🔴 Phạm vi là cặp <c>(SDT, IdCoSo)</c>, không còn là <c>IdTaiKhoan</c>.
    /// Mỗi người hiện đúng MỘT lần — trước 1B một người khám N cơ sở thì ra N dòng
    /// trùng tên.
    /// </para>
    /// <para>
    /// 🔴 Luôn lọc <c>IdCoSo != null</c>: dòng neo là trạng thái quá độ, không
    /// được hiện ở màn nào (tiêu chí nghiệm thu §10 mục 10).
    /// </para>
    /// <para>
    /// Toggle <c>MOT_HO_SO</c> (C4/C5-R2): bật thì chỉ trả ĐÚNG MỘT hồ sơ, chọn theo
    /// thứ tự <c>idDangChon</c> -> CCCD của phiên -> dòng đầu. Không còn dừng đầu ở
    /// CCCD: đăng nhập OTP thường không mang CCCD, và bấm *Chọn* cũng không đổi claim
    /// Cccd, nên lấy CCCD làm tiêu chí đầu là trả sai người. Vẫn không CHẶN khi cả ba
    /// đều trượt — chặn ở đây là khóa chết người dùng thật.
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
            // 🔴 Hồ sơ ĐANG CHỌN đi trước CCCD của phiên. Thiếu vế này thì hai đường
            // vào đều trả sai người: (1) đăng nhập OTP thường không mang CCCD => rơi
            // xuống nguoi[0] tức hồ sơ có Id nhỏ nhất, không liên quan gì tới người
            // đang dùng; (2) bấm *Chọn* sang hồ sơ khác thì PhatLaiClaimAsync chỉ thay
            // claim HoSoDangChon và GIỮ NGUYÊN claim Cccd cũ => màn này hiện một hồ sơ
            // trong khi /benh-nhan đọc dữ liệu của hồ sơ khác.
            nguoi = new List<BenhNhan> { ChonMotHoSo(nguoi, cccdPhien, idDangChon) };
        }

        var id = nguoi.Select(p => p.Id).ToList();

        // Có dữ liệu khám rồi thì không cho xóa — xóa là mất dữ liệu y tế thật.
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
            // Một dòng = một hồ sơ tại một cơ sở => nhiều nhất một mã (ADR 0032).
            SoMaDaNoi: p.MaBN != null ? 1 : 0)).ToList();
    }

    /// <summary>
    /// 🔴 Cổng chặn trước khi phát lại claim *hồ sơ đang chọn*. Thiếu phép kiểm
    /// này thì gõ ID hồ sơ người khác vào là xem được bệnh án của họ — đúng loại
    /// lỗ hổng mà đường đọc tài liệu từng mắc.
    /// </summary>
    public async Task<bool> HoSoThuocTaiKhoanAsync(long idBenhNhan, string sdt, string? maCoSo)
    {
        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null || string.IsNullOrWhiteSpace(sdt)) return false;

        return await _db.BenhNhans.AsNoTracking()
            .AnyAsync(p => p.Id == idBenhNhan && p.SDT == sdt && p.IdCoSo == idCoSo.Value);
    }

    /// <summary>
    /// Người dùng TỰ GÕ tạo một hồ sơ mới (ADR 0019 mục 1) — con đăng ký hộ mẹ,
    /// không đòi cơ sở phải có sẵn người đó.
    ///
    /// <para>
    /// 🔴 Từ Đợt 4, LƯU LÀ NỐI (ADR 0024 vế 1): không còn nút *Nối hồ sơ* nào trên
    /// giao diện. Bệnh nhân không phải tự biết mình đã từng khám ở cơ sở này hay
    /// chưa — đó là câu chỉ HIS trả lời được.
    /// </para>
    /// <para>
    /// "Ai khai trước giữ CCCD": thủ tục trả <c>ResultCode 3</c> kèm đường ra khi
    /// CCCD đã thuộc tài khoản khác. Lỗi đó hiện nguyên văn cho người dùng —
    /// nó là lỗi CÓ ÍCH, không được nuốt.
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

        // 🔴 Cùng luật với SuaAsync: SDT là thứ dùng để ĐĂNG NHẬP nên BẮT BUỘC. Bắt
        // ở một cửa mà thả ở cửa kia thì không phải là bắt buộc — người dùng tạo hồ
        // sơ không số, rồi đến lần sửa đầu tiên mới bị chặn.
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

        // Gán hồ sơ vào cơ sở của phiên để nó hiện ngay ở trang bệnh nhân. Không
        // có mã cơ sở (phiên lạ thường) thì vẫn tạo được CON NGƯỜI — hồ sơ tại cơ
        // sở sẽ sinh khi người dùng mở trang của cơ sở đó.
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

        // Chỉ liệt kê mã TẠI CƠ SỞ ĐANG XEM. Mã ở cơ sở khác không hiện ở đây: màn
        // này nói về quan hệ giữa người này và CƠ SỞ NÀY, trộn vào là người dùng
        // không hiểu mình đang gõ cái gì.
        // Một dòng = một hồ sơ tại một cơ sở => nhiều nhất MỘT mã (ADR 0032).
        // Giữ kiểu danh sách để màn *Sửa hồ sơ* không phải viết lại.
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
    /// Màn *Sửa hồ sơ* — thứ duy nhất cứu được nhóm hồ sơ CHÍNH CHỦ (14/19 tài
    /// khoản đang mang tên là SỐ ĐIỆN THOẠI): hồ sơ của họ tự đẻ ra lúc đăng ký
    /// bằng OTP, không bao giờ đi qua màn *Thêm hồ sơ*.
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

        // 🔴 SDT BẮT BUỘC: nó là thứ dùng để ĐĂNG NHẬP vào cổng. Hồ sơ không có số
        // thì đến lúc cần vào lại là không còn đường nào. Chặn ở đây chứ không chỉ
        // đặt `required` trên ô input — thuộc tính đó gỡ bằng DevTools là xong.
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

        // Dòng đã là hồ sơ TẠI CƠ SỞ rồi (KEEP-ID, đợt 1B) nên không còn bước
        // "tạo dòng tự khai" nào ở đây nữa.
        var idCoSo = idCoSoPhien;

        var noi = await NoiKhiLuuAsync(idBenhNhan, idCoSo, cccd, hoTen, ngaySinh, gioiTinh);

        return noi with { ThanhCong = true, IdBenhNhan = idBenhNhan };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // *Nối hồ sơ* — hai tầng (ADR 0024 vế 2)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hỏi HIS một lần rồi chia hai lối ra:
    /// <list type="bullet">
    ///   <item><b>Tầng 1</b> — dòng có <c>cccdKhop</c> (HIS đã xác nhận CCCD bên gọi
    ///   HỢP LỆ và TRÙNG KHỚP) => gán im lặng.</item>
    ///   <item><b>Tầng 2</b> — còn lại => trả về cho màn hiện danh sách, KHÔNG gán
    ///   gì cho tới khi có người bấm.</item>
    /// </list>
    /// Ranh giới đặt đúng cho <b>348 nhóm</b> trùng họ tên + ngày sinh + giới tính
    /// mà CCCD hợp lệ KHÁC NHAU: mọi ca ấy rơi tầng 2.
    ///
    /// <para>
    /// 🔴 Không bao giờ làm thất bại cả lần LƯU. Hồ sơ đã lưu xong rồi; nối được
    /// hay không là chuyện sau đó. HIS chết mà kéo theo "không sửa được tên" thì
    /// đúng là lấy cái phụ để phá cái chính.
    /// </para>
    /// </summary>
    private async Task<KetQuaLuuHoSo> NoiKhiLuuAsync(
        long idBenhNhan, long? idCoSo, string cccd, string hoTen,
        DateTime? ngaySinh, string? gioiTinh)
    {
        if (idCoSo is null || ngaySinh is null || string.IsNullOrWhiteSpace(gioiTinh))
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        // 🔴 ĐÃ NỐI RỒI THÌ DỪNG LẠI — không hỏi HIS, không đổi gì. Một hồ sơ giữ
        // ĐÚNG MỘT mã (luật nghiệp vụ chốt 09/09: MaBN là danh tính của bệnh nhân
        // tại cơ sở, nhiều mã cùng một người là dữ liệu nhập lộn). Trước đây mỗi
        // lần LƯU đều tra lại rồi ghi đè, nên mã đang nối TỰ ĐỔI sang mã khác mà
        // không một dấu hiệu nào — đó là đổi bệnh án của người ta sau lưng họ.
        // Muốn đổi thì bấm *Gỡ nối* trước, đúng như màn *Sửa hồ sơ* đang hướng dẫn.
        var daNoiMa = await _db.BenhNhans.AsNoTracking()
            .AnyAsync(h => h.Id == idBenhNhan
                        && h.IdCoSo == idCoSo.Value
                        && h.MaBN != null);

        if (daNoiMa)
            return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.KhongCoGi);

        // 🔴 CHẶN HẲN đường nối cho hồ sơ mang mã giả "không có căn cước" (chốt user
        // 10/09, ADR 0028). Thoát TRƯỚC khi hỏi HIS, cố ý:
        //   * không hỏi thì không có danh sách ứng viên nào để lộ. Kẻ gõ họ tên +
        //     ngày sinh + giới tính của người khác sẽ không thấy gì hết — ba ô đó in
        //     trên mọi toa thuốc nên chúng KHÔNG phải bằng chứng danh tính.
        //   * đo thật trên PKDK_ThienNam: 11.533 hồ sơ không có căn cước, trong đó
        //     2.717 nằm trong 1.203 nhóm trùng cả họ tên + ngày sinh + giới tính.
        //     Nối theo ba ô đó là giao bệnh án cho người trùng tên.
        // Cái giá đã biết và CHẤP NHẬN: nhóm này không kéo được bệnh án về cổng.
        // Muốn mở lại thì phải có đường "gõ đúng Mã BN" (SPWA_TraCuuTheoMaBN bên HIS
        // đã sẵn sàng, có chặn dồ 20 lần/15 phút) — chưa làm.
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

        // *Tầng 1* chỉ còn ĐÚNG MỘT trường hợp: cơ sở cấp đúng MỘT mã, CCCD trùng
        // khớp, và mã đó CHƯA có hồ sơ nào khác nhận. Ra từ HAI mã trở lên là KHÔNG
        // được tự quyết, dù CCCD khớp hết.
        // 🔴 Nhánh "mã giả cũng được tự gán" (thêm 10/09) ĐÃ BỊ GỠ: hồ sơ mang mã giả
        // không còn đi tới đây nữa — bị chặn từ trên, trước cả lúc hỏi HIS.
        if (ungVien.Count == 1 && ungVien[0].CccdKhop)
        {
            // 🔴 CHỈ CCCD trùng khớp mới là yếu tố xác thực. Nhánh mã giả
            // (11111111111 / 111111111111) nối được nhờ khớp ba ô danh tính CÔNG KHAI,
            // nên nó KHÔNG được mở cửa tài liệu: ai biết họ tên + ngày sinh + giới tính
            // của người khác cũng gõ ra được. Họ vẫn xem được tóm tắt đợt khám (tầng 1
            // của ADR 0020), chỉ đơn thuốc + kết quả CLS là còn đóng.
            var so = await GanMotMaAsync(idBenhNhan, idCoSo.Value, ungVien[0].MaBN!,
                                         daXacThuc: ungVien[0].CccdKhop);

            // so = 0 nghĩa là mã đã có chủ. KHÔNG được báo "đã nối" (màn sẽ in ra ô
            // mã rỗng), cũng KHÔNG được im lặng: đẩy xuống tầng 2 để màn khóa mã lại
            // và chỉ đường sang Support.
            if (so > 0)
                return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.DaGanImLang, so);
        }

        return new KetQuaLuuHoSo(true, "OK", idBenhNhan, KetCucNoi.ChoXacNhan, 0, ungVien);
    }

    public async Task<(bool ThanhCong, string ThongBao, int SoMaVuaGan)> XacNhanNoiAsync(
        long idBenhNhan, string sdtPhien, string? maCoSo, string? maBN)
    {
        // 🔴 Cổng chặn. Mã đi qua trình duyệt nên không tin được: thiếu phép này
        // thì gõ ID hồ sơ người khác vào là gán mã vào hồ sơ của họ.
        if (!await HoSoThuocTaiKhoanAsync(idBenhNhan, sdtPhien, maCoSo))
            return (false, "Hồ sơ này không thuộc tài khoản của bạn.", 0);

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null)
            return (false, "Chưa xác định được cơ sở.", 0);

        var ma = (maBN ?? string.Empty).Trim();

        if (ma.Length == 0) return (true, "OK", 0);

        // 🔴 Cổng chặn thứ hai của luật "mã giả thì không nối" (ADR 0028). NoiKhiLuuAsync
        // đã chặn từ trên nên bình thường không ai tới đây được với mã giả — nhưng
        // action này POST trần được: giữ lại một form cũ, hoặc đổi CCCD hồ sơ thành mã
        // giả sau khi danh sách ứng viên đã nằm trong tay, là đi vòng được cổng trên.
        var cccdHoSo = await _db.BenhNhans.AsNoTracking()
            .Where(x => x.Id == idBenhNhan)
            .Select(x => x.CCCD)
            .FirstOrDefaultAsync();

        if (LaMaGia(cccdHoSo))
            return (false, "Hồ sơ chưa có số căn cước nên không nối được bệnh án. Bạn vui lòng bổ sung số căn cước, hoặc liên hệ bộ phận hỗ trợ của cơ sở.", 0);

        // Cùng cổng chặn với NoiKhiLuuAsync: một hồ sơ ĐÚNG MỘT mã. Gửi lại form
        // cũ (nút Back, bấm hai lần) không được để đổi mã đang nối.
        var daNoiMa = await _db.BenhNhans.AsNoTracking()
            .AnyAsync(h => h.Id == idBenhNhan
                        && h.IdCoSo == idCoSo.Value
                        && h.MaBN != null);

        if (daNoiMa)
            return (false, "Hồ sơ này đã nối một mã rồi. Muốn đổi thì gỡ nối trước.", 0);

        // Tầng 2 giữ nguyên hành vi cũ (mở cửa) — siết chỗ này là đổi cả luồng,
        // phải có đường "gõ đúng Mã BN để mở" của ADR 0020 trước, không thì khóa
        // chết người dùng thật. Xem ADR 0028 mục "Còn thiếu".
        var so = await GanMotMaAsync(idBenhNhan, idCoSo.Value, ma, daXacThuc: true);

        // 🔴 Mã đã có hồ sơ khác nhận thì DỪNG HẲN ở đây — cổng không tự tháo ra
        // được. Nhận lại là kéo bệnh án đang thuộc về người khác sang mình; gỡ nhầm
        // thì cả hai bên đều mất. Phải qua bộ phận hỗ trợ của cơ sở.
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

        // "Chủ" ở đây là hồ sơ KHÁC. Mã của chính hồ sơ này không tính là vướng.
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

        // Mọi phép chặn nằm trong thủ tục: không phải hồ sơ của mình, hoặc dòng
        // đó vốn là hồ sơ tự khai (không có mã nào để gỡ).
        var ketQua = await _thuTuc.GoNoiAsync(idBenhNhan, sdt, idCoSo.Value);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }

    /// <summary>
    /// Gán ĐÚNG MỘT mã vào hồ sơ. Trả <c>1</c> nếu gán được, <c>0</c> nếu không.
    ///
    /// <para>
    /// 🔴 KHÔNG nhận danh sách, và đó là cố ý. Thủ tục <c>DM_BenhNhanCoSo_Save</c>
    /// tra khóa theo <c>(IDBenhNhan, IDCoSo)</c> rồi UPDATE, nên gọi nó nhiều lần
    /// cho cùng một hồ sơ chỉ GHI ĐÈ một dòng: vòng lặp cũ báo "đã nối N mã" trong
    /// khi CSDL chỉ giữ mã CUỐI CÙNG — mất im lặng, đo được ngày 09/09 (mã 111457
    /// biến mất khi 100992 được gán đè lên). Một hồ sơ &lt;-&gt; một mã.
    /// </para>
    ///
    /// <para>
    /// 🔴 Mã đã có người khác nhận thì từ chối (unique có lọc trên
    /// <c>(IDCoSo, MaBN)</c>). Chặn ở đây để còn thông báo tử tế, nhưng KHÔNG nói
    /// mã đó đang thuộc về ai.
    /// </para>
    /// </summary>
    /// <param name="daXacThuc">
    /// Lần nối này CÓ một yếu tố chỉ đúng người mới có hay không (hiện tại: CCCD
    /// hợp lệ và trùng khớp). Khớp họ tên + ngày sinh + giới tính KHÔNG tính — ba ô
    /// đó in trên mọi toa thuốc, ai cũng gõ được. Cờ này quyết định *Cửa tài liệu*
    /// (ADR 0020): không có yếu tố nào thì hồ sơ vẫn nối được và vẫn xem được TÓM TẮT
    /// đợt khám, nhưng đơn thuốc + kết quả CLS thì đóng.
    /// </param>
    /// <summary>
    /// Mã giả HIS dùng để đánh dấu "không có căn cước". 🔴 Hồ sơ mang mã này
    /// KHÔNG BAO GIỜ được nối (chốt 10/09) — xem <see cref="MaGiaKhongCanCuoc"/>.
    /// </summary>
    private static readonly string[] MaGiaKhongCanCuoc = { "11111111111", "111111111111" };

    /// <summary>Căn cước này thật ra là dấu "không có căn cước" chứ không phải số thật.</summary>
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
    /// Luật chọn MỘT hồ sơ khi <c>MOT_HO_SO</c> bật — dùng ở CẢ HAI nơi
    /// (<see cref="LayDanhSachAsync"/> và <see cref="LayIdHoSoMoSanKhiQuetAsync"/>)
    /// nên chỉ được viết MỘT lần ở đây.
    ///
    /// <para>
    /// Thứ tự: hồ sơ ĐANG CHỌN -> CCCD của phiên -> dòng đầu. Hồ sơ đang chọn phải đi
    /// trước vì bấm *Chọn* không đổi claim Cccd (HoSoController.PhatLaiClaimAsync),
    /// lấy CCCD làm tiêu chí đầu là trả về người vừa bị chuyển khỏi.
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

        // MOT_HO_SO tắt => tài khoản được giữ nhiều hồ sơ, phải để người dùng tự chọn.
        if (!await _config.KiemTraHieuLucAsync("MOT_HO_SO")) return null;

        var idCoSo = await LayIdCoSoAsync(maCoSo);
        if (idCoSo is null) return null;

        var nguoi = await _db.BenhNhans.AsNoTracking()
            .Where(p => (p.SDT == sdt || p.Email == sdt) && p.IdCoSo == idCoSo.Value)
            .OrderBy(p => p.Id)
            .ToListAsync();

        // Phiên vừa quét xong thì chưa có claim HoSoDangChon => truyền null, luật lùi
        // về CCCD của phiếu vừa quét.
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

        // Mọi phép chặn nằm trong thủ tục (ADR 0008): không phải hồ sơ của mình,
        // đã nối HIS, hoặc đã có dữ liệu khám.
        var ketQua = await _thuTuc.XoaHoSoAsync(idBenhNhan, sdt, idCoSo.Value);
        return (ketQua.Succeeded, ketQua.Message ?? "");
    }
}
