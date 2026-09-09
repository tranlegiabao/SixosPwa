using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;

namespace SixosPwa.Services;

public sealed record AdminStoredProcedureResult(int Code, string? Message)
{
    public bool Succeeded => Code == 1;
}

/// <summary>
/// MOI duong ghi cua ung dung di qua day (ADR 0008). EF chi con dung de doc.
/// Ten lop giu nguyen tu thoi chi khu Admin ghi DB; tu dot tai kien truc no phuc
/// vu ca luong benh nhan.
/// </summary>
public sealed class AdminStoredProcedureService
{
    private readonly ApplicationDbContext _db;

    public AdminStoredProcedureService(ApplicationDbContext db) => _db = db;

    // ------------------------------------------------------------------
    //  Tai khoan / doi tac
    // ------------------------------------------------------------------

    /// <summary>
    /// <paramref name="matKhauNoiBoDaBam"/> phai la chuoi DA BAM. Dot nay chua thi
    /// hanh phan bam nen moi noi goi deu truyen null — xem muc Dinh chinh ADR 0009.
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveTaiKhoanAsync(
        long id,
        string sdt,
        string? email,
        string role,
        string? matKhauNoiBoDaBam,
        long? idBenhNhan) =>
        ExecuteWithIdAsync("dbo.HT_TaiKhoan_Save", "@IDTaiKhoan", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, id);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@Email", DbType.String, email, 50);
            AddParameter(command, "@Role", DbType.AnsiString, role, 20);
            AddParameter(command, "@MatKhauNoiBoDaBam", DbType.AnsiString, matKhauNoiBoDaBam, 255);
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
        });

    public Task<AdminStoredProcedureResult> SaveDoiTacAsync(
        DoiTacEditViewModel model,
        bool updatePassword) =>
        ExecuteAsync("dbo.DM_DoiTac_Save", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, model.Id);
            AddParameter(command, "@MaDT", DbType.AnsiString, model.MaDT, 20);
            AddParameter(command, "@TenDT", DbType.String, model.TenDT, 100);
            AddParameter(command, "@DiaChi", DbType.String, model.DiaChi, 255);
            AddParameter(command, "@SDT", DbType.AnsiString, model.SDT, 20);
            AddParameter(command, "@Email", DbType.AnsiString, model.Email, 100);
            AddParameter(command, "@IDPM", DbType.Int64, null);
            AddParameter(command, "@BrandName", DbType.String, model.BrandName, 100);
            AddParameter(command, "@MatKhauDoiTac", DbType.String,
                updatePassword ? model.MatKhauDoiTac : null, 255);
        });

    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveTaiKhoanDoiTacAsync(
        long idTaiKhoan,
        long idCoSo,
        string? matKhau,
        string? maXacNhanTam,
        bool daLienKet) =>
        ExecuteWithIdAsync("dbo.HT_TaiKhoanDoiTac_Save", "@IDLienKet", command =>
        {
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@MatKhau", DbType.String, matKhau, 255);
            AddParameter(command, "@MaXacNhanTam", DbType.AnsiString, maXacNhanTam, 10);
            AddParameter(command, "@DaLienKet", DbType.Boolean, daLienKet);
        });

    /// <summary>Ma xac nhan chi dung MOT lan o man Ban giao roi phai bien mat ngay.</summary>
    public Task XoaMaXacNhanAsync(long idTaiKhoan, long idCoSo) =>
        ExecuteNoResultAsync("dbo.HT_TaiKhoanDoiTac_XoaMaXacNhan", command =>
        {
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    // ------------------------------------------------------------------
    //  Benh nhan
    // ------------------------------------------------------------------

    /// <summary>
    /// Them/cap nhat CON NGUOI, nhan dien bang CCCD.
    ///
    /// <para>
    /// <paramref name="idTaiKhoan"/> la chu so huu ho so (ADR 0019). Thu tuc chi
    /// nhan chu khi cot dang bo trong, va tra <c>ResultCode 3</c> khi CCCD da
    /// thuoc mot tai khoan KHAC — "ai khai truoc giu CCCD".
    /// </para>
    /// <para>
    /// <paramref name="hoTenKhongDau"/> phai lay tu <see cref="ChuanHoaTen.BoDau"/>,
    /// dung tu chuan hoa kieu khac — do la o thu ba cua luat gop (ADR 0018).
    /// </para>
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveBenhNhanAsync(
        string cccd,
        string tenBN,
        string? sdt,
        string? email,
        string? diaChi,
        long? idTaiKhoan = null,
        DateTime? ngaySinh = null,
        string? hoTenKhongDau = null,
        string? gioiTinh = null) =>
        ExecuteWithIdAsync("dbo.DM_BenhNhan_Save", "@IDBenhNhan", command =>
        {
            AddParameter(command, "@CCCD", DbType.AnsiString, cccd, 20);
            AddParameter(command, "@TenBN", DbType.String, tenBN, 100);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@Email", DbType.AnsiString, email, 100);
            AddParameter(command, "@DiaChi", DbType.String, diaChi, 255);
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@NgaySinh", DbType.DateTime, ngaySinh);
            AddParameter(command, "@HoTenKhongDau", DbType.String, hoTenKhongDau, 100);
            AddParameter(command, "@GioiTinh", DbType.AnsiString, gioiTinh, 10);
        });

    /// <summary>
    /// Man *Sua ho so* (Dot 4). 🔴 KHONG dung lai <see cref="SaveBenhNhanAsync"/>:
    /// cai do nhan dien bang CCCD, ma o day nguoi dung DOI DUOC ca CCCD — goi Save
    /// voi CCCD moi se de ra mot CON NGUOI THU HAI thay vi sua nguoi dang co.
    ///
    /// Thu tuc tu chan: khong phai ho so cua minh (<c>ResultCode 5</c>), CCCD dam
    /// vao nguoi khac (<c>3</c>). Ho so DA NOI HIS thi bon o danh tinh khoa cung,
    /// chi con so dien thoai sua duoc — thu tuc tu bo qua, ben goi khong phai biet.
    /// </summary>
    public Task<AdminStoredProcedureResult> SuaHoSoAsync(
        long idBenhNhan,
        long idTaiKhoan,
        string? cccd,
        string? tenBN,
        string? sdt,
        DateTime? ngaySinh,
        string? hoTenKhongDau,
        string? gioiTinh) =>
        ExecuteAsync("dbo.DM_BenhNhan_SuaHoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@CCCD", DbType.AnsiString, cccd, 20);
            AddParameter(command, "@TenBN", DbType.String, tenBN, 100);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@NgaySinh", DbType.DateTime, ngaySinh);
            AddParameter(command, "@HoTenKhongDau", DbType.String, hoTenKhongDau, 100);
            AddParameter(command, "@GioiTinh", DbType.AnsiString, gioiTinh, 10);
        });

    /// <summary>
    /// *Go noi* — thao MOT ma khoi mot ho so (ADR 0024 ve 3).
    ///
    /// 🔴 Thu tuc XOA CA tai lieu va dot kham cua dong do (sao luu sang
    /// <c>bak.GoNoi_*_V001</c> truoc). Khong phai tuy chon: khoa ngoai la
    /// NO_ACTION nen khong xoa dong duoc chung nao con con, va neu ma bi noi NHAM
    /// thi de tai lieu lai chinh la giu nguyen cai hai ma nut nay sinh ra de chua.
    /// Mat khong vinh vien — cua <c>kiem-tra-nhan</c> se dua ma ve trang thai
    /// "chua ai nhan" nen hang doi ben HIS day lai duoc.
    /// </summary>
    public Task<AdminStoredProcedureResult> GoNoiAsync(long idBenhNhanCoSo, long idTaiKhoan) =>
        ExecuteAsync("dbo.DM_BenhNhanCoSo_GoNoi", command =>
        {
            AddParameter(command, "@IDBenhNhanCoSo", DbType.Int64, idBenhNhanCoSo);
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
        });

    /// <summary>
    /// Doi *Moc xem lich* cho TAT CA dong cua mot con nguoi tai mot co so (ADR 0025).
    /// Doi tung dong thi mo modal xong huy hieu van con — o *Lich kham cua toi* gop
    /// het cac ma lai thanh mot danh sach nen "da xem" phai la mot trang thai duy nhat.
    /// </summary>
    public Task<AdminStoredProcedureResult> DoiMocXemLichAsync(
        long idBenhNhan, long idCoSo, long idTaiKhoan) =>
        ExecuteAsync("dbo.DM_BenhNhanCoSo_DoiMocXemLich", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
        });

    /// <summary>
    /// Ho so TU KHAI tai mot co so: <c>MaBN</c> de RONG vi co so chua cap ma nao.
    ///
    /// 🔴 Tach khoi <see cref="SaveBenhNhanCoSoAsync"/> chu khong gop lam mot:
    /// duong nay khoa theo <c>(IDBenhNhan, IDCoSo)</c> trong pham vi cac dong
    /// CHUA noi HIS, con duong kia khoa theo ma that. Gop lai thi phai so
    /// <c>MaBN = NULL</c>, ma trong SQL <c>NULL = NULL</c> khong bao gio dung nen
    /// moi lan luu se de mot dong moi.
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> TaoHoSoTuKhaiAsync(
        long idBenhNhan,
        long idCoSo) =>
        ExecuteWithIdAsync("dbo.DM_BenhNhanCoSo_TaoTuKhai", "@IDBenhNhanCoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    /// <summary>
    /// Xoa mot ho so de NHA CCCD ra (ADR 0019 muc 2) — khong co nut nay thi "ai
    /// khai truoc giu" thanh cai bay khong loi thoat cho chinh chu.
    /// Thu tuc tu chan: khong phai ho so cua minh, ho so da noi HIS, hoac da co
    /// du lieu kham.
    /// </summary>
    /// <summary>
    /// Gan chu so huu cho mot ho so, CHI khi cot dang bo trong.
    ///
    /// Tach khoi <see cref="SaveBenhNhanAsync"/> vi trong luong dang ky, CON
    /// NGUOI duoc tao TRUOC tai khoan nen luc do chua biet chu la ai. Da co chu
    /// khac thi tra <c>ResultCode 3</c> — "ai khai truoc giu CCCD" (ADR 0019).
    /// </summary>
    public Task<AdminStoredProcedureResult> NhanChuSoHuuAsync(long idBenhNhan, long idTaiKhoan) =>
        ExecuteAsync("dbo.DM_BenhNhan_NhanChuSoHuu", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
        });

    public Task<AdminStoredProcedureResult> XoaHoSoAsync(long idBenhNhan, long idTaiKhoan) =>
        ExecuteAsync("dbo.DM_BenhNhan_XoaHoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
        });

    /// <summary>
    /// Gan con nguoi vao co so kem MA THAT do co so cap — duong NOI HIS.
    ///
    /// 🔴 Tu dot V6b KHONG con cho nao goi: luong dang ky da chuyen sang
    /// <see cref="TaoHoSoTuKhaiAsync"/> (ho so tu khai, MaBN rong). Giu lai vi
    /// man *Noi ho so* cua dot sau chinh la cho dung no. Xoa di roi viet lai la
    /// mat doan chan trung MaBN da chay dung.
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveBenhNhanCoSoAsync(
        long idBenhNhan,
        long idCoSo,
        string maBN) =>
        ExecuteWithIdAsync("dbo.DM_BenhNhanCoSo_Save", "@IDBenhNhanCoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@MaBN", DbType.AnsiString, maBN, 20);
        });

    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveTaiLieuBenhNhanAsync(
        long id,
        long idCoSo,
        long? idBenhNhanCoSo,
        string maBN,
        string loaiTaiLieu,
        string tenTaiLieu,
        string duongDanFtp,
        long dungLuongByte,
        DateTime? ngayKham,
        string? ghiChu,
        string? maNguonHIS = null,
        string? bamNoiDung = null) =>
        ExecuteWithIdAsync("dbo.QL_TaiLieuBenhNhan_Save", "@IDTaiLieu", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, id);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@IDBenhNhanCoSo", DbType.Int64, idBenhNhanCoSo);
            AddParameter(command, "@MaBN", DbType.String, maBN, 50);
            AddParameter(command, "@LoaiTaiLieu", DbType.String, loaiTaiLieu, 50);
            AddParameter(command, "@TenTaiLieu", DbType.String, tenTaiLieu, 255);
            AddParameter(command, "@DuongDanFtp", DbType.String, duongDanFtp, 500);
            AddParameter(command, "@DungLuongByte", DbType.Int64, dungLuongByte);
            AddParameter(command, "@NgayKham", DbType.DateTime, ngayKham);
            AddParameter(command, "@GhiChu", DbType.String, ghiChu);
            AddParameter(command, "@MaNguonHIS", DbType.AnsiString, maNguonHIS, 50);
            AddParameter(command, "@BamNoiDung", DbType.AnsiStringFixedLength, bamNoiDung, 64);
        });

    // ------------------------------------------------------------------
    //  Khu API nhan (dot 2 giai doan 2)
    // ------------------------------------------------------------------

    /// <summary>
    /// Cap hoac sua mot khoa API cua co so. <paramref name="khoaTho"/> chi bat
    /// buoc khi CAP MOI (<paramref name="id"/> = 0); thu tuc tu bam, khoa tho
    /// khong bao gio duoc luu.
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveKhoaApiCoSoAsync(
        long id,
        long idCoSo,
        string tenKhoa,
        string? khoaTho,
        bool active,
        DateTime? ngayHetHan) =>
        ExecuteWithIdAsync("dbo.HT_KhoaApiCoSo_Save", "@IDKhoa", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, id);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@TenKhoa", DbType.String, tenKhoa, 100);
            AddParameter(command, "@KhoaTho", DbType.AnsiString, khoaTho, 100);
            AddParameter(command, "@Active", DbType.Boolean, active);
            AddParameter(command, "@NgayHetHan", DbType.DateTime, ngayHetHan);
        });

    /// <summary>Mot dong nhat ky doi soat. Thu tuc nay khong tra ResultCode.</summary>
    public Task GhiLogApiCoSoAsync(
        long? idCoSo,
        long? idKhoa,
        string endpoint,
        string? maBN,
        string? maNguonHIS,
        string ketQua,
        string? lyDo,
        int? soLuong,
        string? ipGoi) =>
        ExecuteNoResultAsync("dbo.HT_LogApiCoSo_Ghi", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@IDKhoa", DbType.Int64, idKhoa);
            AddParameter(command, "@Endpoint", DbType.AnsiString, endpoint, 100);
            AddParameter(command, "@MaBN", DbType.AnsiString, maBN, 20);
            AddParameter(command, "@MaNguonHIS", DbType.AnsiString, maNguonHIS, 50);
            AddParameter(command, "@KetQua", DbType.AnsiString, ketQua, 20);
            AddParameter(command, "@LyDo", DbType.AnsiString, lyDo, 50);
            AddParameter(command, "@SoLuong", DbType.Int32, soLuong);
            AddParameter(command, "@IpGoi", DbType.AnsiString, ipGoi, 45);
        });

    /// <summary>
    /// Luu MOT dot kham. Goi lap cho ca lo tu <see cref="DotKhamService"/> —
    /// moi dong tu quyet dinh them hay cap nhat theo khoa tu nhien
    /// (IDCoSo, MaVaoVien), nen day lai ca lo khong de dong trung.
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveDotKhamAsync(
        long idCoSo,
        long idBenhNhanCoSo,
        string maVaoVien,
        string maBN,
        DateTime ngayGioVao,
        DateTime? ngayGioRa,
        string? tenKhoa,
        string? tenBacSi,
        string? chanDoan) =>
        ExecuteWithIdAsync("dbo.QL_DotKham_Save", "@IDDotKham", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@IDBenhNhanCoSo", DbType.Int64, idBenhNhanCoSo);
            AddParameter(command, "@MaVaoVien", DbType.AnsiString, maVaoVien, 50);
            AddParameter(command, "@MaBN", DbType.AnsiString, maBN, 20);
            AddParameter(command, "@NgayGioVao", DbType.DateTime, ngayGioVao);
            AddParameter(command, "@NgayGioRa", DbType.DateTime, ngayGioRa);
            AddParameter(command, "@TenKhoa", DbType.String, tenKhoa, 255);
            AddParameter(command, "@TenBacSi", DbType.String, tenBacSi, 255);
            AddParameter(command, "@ChanDoan", DbType.String, chanDoan);
        });

    // ------------------------------------------------------------------
    //  Co so y te
    // ------------------------------------------------------------------

    public Task<AdminStoredProcedureResult> SaveCoSoYTeAsync(CoSoYTeEditViewModel model) =>
        ExecuteAsync("dbo.DM_CSKCB_Save", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, model.Id);
            AddParameter(command, "@MaCoSo", DbType.AnsiString, model.MaCoSo, 10);
            AddParameter(command, "@TenCoSo", DbType.String, model.TenCoSo, 100);
            AddParameter(command, "@Slug", DbType.AnsiString, model.Slug, 100);
            AddParameter(command, "@IDNhomCS", DbType.Int64, model.SelectedNhomCSId);
            AddParameter(command, "@DiaChi", DbType.String, model.DiaChi, 255);
            AddParameter(command, "@SoToaNha", DbType.String, model.SoToaNha, 100);
            AddParameter(command, "@Tinh", DbType.Int32, model.Tinh);
            AddParameter(command, "@PhuongXa", DbType.Int32, model.PhuongXa);
            AddParameter(command, "@SDT", DbType.AnsiString, model.SDT, 20);
            AddParameter(command, "@Email", DbType.AnsiString, model.Email, 100);
            AddParameter(command, "@TenTM", DbType.String, model.TenTM, 100);
            AddParameter(command, "@Img", DbType.String, model.Img, 500);
            AddParameter(command, "@Logo", DbType.String, model.Logo, size: -1);
            AddParameter(command, "@Active", DbType.Boolean, model.Active);
            AddParameter(command, "@QuangCao", DbType.Decimal, model.QuangCao, precision: 15, scale: 0);
        });

    public Task<AdminStoredProcedureResult> DeleteCoSoYTeAsync(long id) =>
        ExecuteAsync("dbo.DM_CSKCB_Delete", command =>
            AddParameter(command, "@ID", DbType.Int64, id));

    public Task<AdminStoredProcedureResult> SaveQCKCBAsync(
        long idCoSo,
        string? noiDung,
        string? img) =>
        ExecuteAsync("dbo.DM_CSKCB_QuangCao_Save", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@NoiDung", DbType.String, noiDung, size: -1);
            AddParameter(command, "@Img", DbType.String, img, size: -1);
        });

    public Task<AdminStoredProcedureResult> SaveNoiDungCskcbAsync(
        long idCoSo,
        long idChuDe,
        string? noiDung) =>
        ExecuteAsync("dbo.DM_CSKCB_NoiDung_Save", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@IDChuDe", DbType.Int64, idChuDe);
            AddParameter(command, "@NoiDung", DbType.String, noiDung, size: -1);
        });

    /// <summary>
    /// Ghi gio lam viec cua MOT ngay. Man Sua goi lap 7 lan (Thu 0..6).
    /// Goi lap KHONG nguyen tu qua ca tuan, nhung moi lan la upsert idempotent
    /// theo khoa UNIQUE (IDCoSo, Thu) nen chay lai an toan.
    /// </summary>
    public Task<AdminStoredProcedureResult> SaveGioLamViecAsync(
        long idCoSo,
        byte thu,
        TimeSpan gioMoCua,
        TimeSpan gioDongCua) =>
        ExecuteAsync("dbo.DM_CSKCB_GioLamViec_Save", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@Thu", DbType.Byte, thu);
            AddParameter(command, "@GioMoCua", DbType.Time, gioMoCua);
            AddParameter(command, "@GioDongCua", DbType.Time, gioDongCua);
        });

    /// <summary>
    /// Xoa cac ngay KHONG con duoc chon. <paramref name="danhSachThuGiuLai"/>
    /// rong = xoa het gio cua co so do.
    /// </summary>
    public Task<AdminStoredProcedureResult> XoaGioLamViecAsync(
        long idCoSo,
        string? danhSachThuGiuLai) =>
        ExecuteAsync("dbo.DM_CSKCB_GioLamViec_Xoa", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@DanhSachThu", DbType.AnsiString, danhSachThuGiuLai, 50);
        });

    public Task<string?> GetNoiDungCskcbAsync(long idCoSo, long idChuDe) =>
        QueryStringAsync("dbo.DM_CSKCB_NoiDung_Get", "NoiDung", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@IDChuDe", DbType.Int64, idChuDe);
        });

    // ------------------------------------------------------------------
    //  Thong bao / thiet bi / push
    // ------------------------------------------------------------------

    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveThongBaoAsync(
        long idNguoiGui,
        long idNguoiNhan,
        string noiDung) =>
        ExecuteWithIdAsync("dbo.HT_ThongBao_Save", "@IDThongBao", command =>
        {
            AddParameter(command, "@IDNguoiGui", DbType.Int64, idNguoiGui);
            AddParameter(command, "@IDNguoiNhan", DbType.Int64, idNguoiNhan);
            AddParameter(command, "@NoiDung", DbType.String, noiDung, 2000);
        });

    public Task DanhDauThongBaoDaDocAsync(long idNguoiNhan) =>
        ExecuteNoResultAsync("dbo.HT_ThongBao_DanhDauDaDoc", command =>
            AddParameter(command, "@IDNguoiNhan", DbType.Int64, idNguoiNhan));

    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveThietBiAsync(
        long idTaiKhoan,
        string maThietBi,
        string? tenThietBi,
        bool trangThai) =>
        ExecuteWithIdAsync("dbo.HT_ThietBi_Save", "@IDThietBi", command =>
        {
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@MaThietBi", DbType.AnsiString, maThietBi, 100);
            AddParameter(command, "@TenThietBi", DbType.String, tenThietBi, 255);
            AddParameter(command, "@TrangThai", DbType.Boolean, trangThai);
        });

    public Task<(AdminStoredProcedureResult KetQua, long Id)> SavePushDangKyAsync(
        long idTaiKhoan,
        string endpoint,
        string p256dh,
        string auth,
        string? maThietBi) =>
        ExecuteWithIdAsync("dbo.HT_PushDangKy_Save", "@IDDangKy", command =>
        {
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@Endpoint", DbType.String, endpoint, 2000);
            AddParameter(command, "@P256dh", DbType.String, p256dh, 1000);
            AddParameter(command, "@Auth", DbType.String, auth, 400);
            AddParameter(command, "@MaThietBi", DbType.AnsiString, maThietBi, 100);
        });

    /// <summary>Don mot subscription da het han (browser tra 404/410).</summary>
    public Task XoaPushDangKyAsync(string endpoint) =>
        ExecuteNoResultAsync("dbo.HT_PushDangKy_Xoa", command =>
            AddParameter(command, "@Endpoint", DbType.String, endpoint, 2000));

    // ------------------------------------------------------------------
    //  Ha tang goi thu tuc
    // ------------------------------------------------------------------

    private async Task<AdminStoredProcedureResult> ExecuteAsync(
        string procedureName,
        Action<DbCommand> configure)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);

            var resultCode = AddOutputParameter(command, "@ResultCode", DbType.Int32, 4);
            var resultMessage = AddOutputParameter(command, "@ResultMessage", DbType.String, 4000);
            await command.ExecuteNonQueryAsync();

            return DocKetQua(resultCode, resultMessage);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Nhu <see cref="ExecuteAsync"/> nhung thu tuc con tra ve khoa chinh vua ghi
    /// qua mot tham so OUTPUT rieng.
    /// </summary>
    private async Task<(AdminStoredProcedureResult KetQua, long Id)> ExecuteWithIdAsync(
        string procedureName,
        string idParameterName,
        Action<DbCommand> configure)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);

            var id = AddOutputParameter(command, idParameterName, DbType.Int64, 8);
            var resultCode = AddOutputParameter(command, "@ResultCode", DbType.Int32, 4);
            var resultMessage = AddOutputParameter(command, "@ResultMessage", DbType.String, 4000);
            await command.ExecuteNonQueryAsync();

            var idMoi = id.Value == DBNull.Value || id.Value is null ? 0L : Convert.ToInt64(id.Value);
            return (DocKetQua(resultCode, resultMessage), idMoi);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Cho nhung thu tuc KHONG khai bao @ResultCode/@ResultMessage. Them hai tham so
    /// do vao la SQL Server bao loi thua tham so, nen phai co duong goi rieng.
    /// </summary>
    private async Task ExecuteNoResultAsync(string procedureName, Action<DbCommand> configure)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Chay mot thu tuc tra ve bang, lay MOT chuoi.
    /// <paramref name="tenCot"/> BAT BUOC doc theo TEN chu khong theo thu tu:
    /// DM_CSKCB_NoiDung_Get tra ca dong (cot 0 la ID bigint), doc theo thu tu
    /// la nem InvalidCastException => HTTP 500. Da dinh o Dot 3.
    /// </summary>
    private async Task<string?> QueryStringAsync(
        string procedureName,
        string tenCot,
        Action<DbCommand> configure)
    {
        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure(command);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var thuTu = reader.GetOrdinal(tenCot);
            return await reader.IsDBNullAsync(thuTu) ? null : reader.GetString(thuTu);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static AdminStoredProcedureResult DocKetQua(DbParameter code, DbParameter message)
    {
        var ma = code.Value == DBNull.Value ? 0 : Convert.ToInt32(code.Value);
        var loi = message.Value == DBNull.Value ? null : message.Value?.ToString();
        return new AdminStoredProcedureResult(ma, loi);
    }

    private static DbParameter AddOutputParameter(
        DbCommand command,
        string name,
        DbType type,
        int size)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Direction = ParameterDirection.Output;
        parameter.Size = size;
        command.Parameters.Add(parameter);
        return parameter;
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null,
        byte? precision = null,
        byte? scale = null)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
            parameter.Size = size.Value;
        if (precision.HasValue)
            parameter.Precision = precision.Value;
        if (scale.HasValue)
            parameter.Scale = scale.Value;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
