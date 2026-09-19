using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Areas.Admin.Models;
using SixosPwa.Data;
using SixosPwa.Models;

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
    //  Tai khoan
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
        string? matKhauNoiBoDaBam) =>
        ExecuteWithIdAsync("dbo.HT_TaiKhoan_Save", "@IDTaiKhoan", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, id);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@Email", DbType.String, email, 50);
            AddParameter(command, "@Role", DbType.AnsiString, role, 20);
            AddParameter(command, "@MatKhauNoiBoDaBam", DbType.AnsiString, matKhauNoiBoDaBam, 255);
            // 🔴 Tham so @IDBenhNhan van con trong CHU KY stored (hop dong linked
            // server, ADR 0035 #2) nhung khong truyen tu day: C# cua cong khong
            // dung toi. Tu dot 1B stored chi con nhan Role='Admin'; vai tro khac
            // bi ghi log roi bo qua.
        });

    /// <summary>Nút <i>Thử kết nối kho</i> bấm ĐẠT thì ghi mốc. Tách khỏi Save có chủ ý.</summary>
    public Task<AdminStoredProcedureResult> GhiNhanThuDatKhoFtpAsync(long idCoSo) =>
        ExecuteAsync("dbo.DM_CSKCB_GhiNhanThuDatFtp", command =>
        {
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    /// <summary>
    /// Phân trang danh sách tài khoản theo chuẩn 0307 qua dbo.HT_TaiKhoan_Loc (hỗ trợ 20, 50, 100, 500 dòng/trang).
    /// </summary>
    public async Task<(List<TaiKhoan> Items, Dictionary<long, List<HoSoBenhNhanItemViewModel>> HoSoTheoTaiKhoan, int TongSoDong)> LocTaiKhoanAsync(
        int trang,
        int soDong,
        string? sdt,
        string? cccd,
        string? maBN,
        string? role,
        string? loaiCS)
    {
        trang = Math.Max(1, trang);
        soDong = soDong is 20 or 50 or 100 or 500 ? soDong : 50;

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        var items = new List<TaiKhoan>();
        var hoSoDict = new Dictionary<long, List<HoSoBenhNhanItemViewModel>>();
        int tongSoDong = 0;

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "dbo.HT_TaiKhoan_Loc";

            AddParameter(command, "@Trang", DbType.Int32, trang);
            AddParameter(command, "@SoDong", DbType.Int32, soDong);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@CCCD", DbType.AnsiString, cccd, 20);
            AddParameter(command, "@MaBN", DbType.String, maBN, 50);
            AddParameter(command, "@Role", DbType.AnsiString, role, 20);
            AddParameter(command, "@LoaiCS", DbType.String, loaiCS, 50);

            await using var reader = await command.ExecuteReaderAsync();

            // Result Set 1: TaiKhoan
            while (await reader.ReadAsync())
            {
                var tk = new TaiKhoan
                {
                    Id = Convert.ToInt64(reader["ID"]),
                    SDT = reader["SDT"]?.ToString() ?? "",
                    Email = reader["Email"] != DBNull.Value ? reader["Email"]?.ToString() : null,
                    Role = reader["Role"]?.ToString() ?? "BenhNhan",
                    MatKhauNoiBo = reader["MatKhauNoiBo"] != DBNull.Value ? reader["MatKhauNoiBo"]?.ToString() : null,
                    // Cot IDBenhNhan da bi xoa khoi HT_TaiKhoan (ADR 0019); ho so cua
                    // tai khoan nam o Result Set 2 duoi day, quan he 1-N.
                    NgayTao = Convert.ToDateTime(reader["NgayTao"])
                };
                items.Add(tk);
                hoSoDict[tk.Id] = new List<HoSoBenhNhanItemViewModel>();

                if (tongSoDong == 0 && reader["TongSoDong"] != DBNull.Value)
                {
                    tongSoDong = Convert.ToInt32(reader["TongSoDong"]);
                }
            }

            // Result Set 2: HoSoBenhNhan
            if (await reader.NextResultAsync())
            {
                var tempProfiles = new List<(long IdTaiKhoan, HoSoBenhNhanItemViewModel Item)>();
                while (await reader.ReadAsync())
                {
                    var idTk = Convert.ToInt64(reader["IdTaiKhoan"]);
                    var hs = new HoSoBenhNhanItemViewModel
                    {
                        Id = Convert.ToInt64(reader["IdBenhNhan"]),
                        IdTaiKhoan = idTk,
                        TenBN = reader["TenBN"]?.ToString() ?? "",
                        CCCD = reader["CCCD"]?.ToString() ?? "",
                        SDT = reader["SDT"] != DBNull.Value ? reader["SDT"]?.ToString() : null,
                        Email = reader["Email"] != DBNull.Value ? reader["Email"]?.ToString() : null,
                        DiaChi = reader["DiaChi"] != DBNull.Value ? reader["DiaChi"]?.ToString() : null,
                        NgaySinh = reader["NgaySinh"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["NgaySinh"]) : null,
                        GioiTinh = reader["GioiTinhStr"] != DBNull.Value ? reader["GioiTinhStr"]?.ToString() : null,
                        MaBN = reader["MaBN"] != DBNull.Value ? reader["MaBN"]?.ToString() : null,
                        TenCoSo = reader["TenCoSo"] != DBNull.Value ? reader["TenCoSo"]?.ToString() : null,
                        IdHoSoCoSo = reader["IdHoSoCoSo"] != DBNull.Value ? (long?)Convert.ToInt64(reader["IdHoSoCoSo"]) : null,
                        IdCoSo = reader["IDCoSo"] != DBNull.Value ? (long?)Convert.ToInt64(reader["IDCoSo"]) : null,
                        SoCoSo = reader["SoCoSo"] != DBNull.Value ? Convert.ToInt32(reader["SoCoSo"]) : 0
                    };
                    tempProfiles.Add((idTk, hs));
                }

                foreach (var group in tempProfiles.GroupBy(x => x.IdTaiKhoan))
                {
                    if (hoSoDict.ContainsKey(group.Key))
                    {
                        var patientGroups = group.Select(x => x.Item).GroupBy(p => p.Id);
                        foreach (var pg in patientGroups)
                        {
                            var firstWithMa = pg.FirstOrDefault(p => !string.IsNullOrEmpty(p.MaBN));
                            var chosen = firstWithMa ?? pg.First();
                            chosen.SoCoSo = pg.Count(p => !string.IsNullOrEmpty(p.MaBN));
                            hoSoDict[group.Key].Add(chosen);
                        }
                    }
                }
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        return (items, hoSoDict, tongSoDong);
    }

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
            // Giu tham so (hop dong linked server, ADR 0035 #1) nhung stored da
            // NHAN ROI BO: cot DM_BenhNhan.IDTaiKhoan bi xoa o dot 1B.
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
        string sdtPhien,
        long idCoSo,
        string? cccd,
        string? tenBN,
        string? sdt,
        DateTime? ngaySinh,
        string? hoTenKhongDau,
        string? gioiTinh) =>
        ExecuteAsync("dbo.DM_BenhNhan_SuaHoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@SDT", DbType.AnsiString, sdtPhien, 20);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@CCCD", DbType.AnsiString, cccd, 20);
            AddParameter(command, "@TenBN", DbType.String, tenBN, 100);
            AddParameter(command, "@SDTMoi", DbType.AnsiString, sdt, 20);
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
    public Task<AdminStoredProcedureResult> GoNoiAsync(long idBenhNhan, string sdt, long idCoSo) =>
        ExecuteAsync("dbo.DM_BenhNhan_GoNoi", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    /// <summary>
    /// Doi *Moc xem lich* cho TAT CA dong cua mot con nguoi tai mot co so (ADR 0025).
    /// Doi tung dong thi mo modal xong huy hieu van con — o *Lich kham cua toi* gop
    /// het cac ma lai thanh mot danh sach nen "da xem" phai la mot trang thai duy nhat.
    /// </summary>
    public Task<AdminStoredProcedureResult> DoiMocXemLichAsync(
        long idBenhNhan, long idCoSo, string sdt) =>
        ExecuteAsync("dbo.DM_BenhNhan_DoiMocXemLich", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
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
        ExecuteWithIdAsync("dbo.DM_BenhNhan_TaoTuKhai", "@IDBenhNhanCoSo", command =>
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
    /// Xoa mot ho so tai co so. Tu 1B chu so huu la cap (SDT phien x co so phien),
    /// khong con <c>IDTaiKhoan</c> — xem ADR 0027 (da dao) va ADR 0034.
    /// </summary>
    public Task<AdminStoredProcedureResult> XoaHoSoAsync(long idBenhNhan, string sdt, long idCoSo) =>
        ExecuteAsync("dbo.DM_BenhNhan_XoaHoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    /// <summary>
    /// Gan con nguoi vao co so kem MA THAT do co so cap — duong NOI HIS.
    ///
    /// 🔴 Tu dot V6b KHONG con cho nao goi: luong dang ky da chuyen sang
    /// <see cref="TaoHoSoTuKhaiAsync"/> (ho so tu khai, MaBN rong). Giu lai vi
    /// man *Noi ho so* cua dot sau chinh la cho dung no. Xoa di roi viet lai la
    /// mat doan chan trung MaBN da chay dung.
    /// </summary>
    /// <summary>
    /// 🔴 <paramref name="moCuaTaiLieu"/> phai truyen TUONG MINH. Thu tuc de mac
    /// dinh <c>@DaMoTaiLieu = 1</c>, nen bo trong la moi lan noi deu MO TOANG cua
    /// tai lieu — dung cai cua ma ADR 0020 dung len de chan. Mac dinh <c>true</c> o
    /// day giu nguyen hanh vi cu cho man Admin (do la duong cua bo phan ho tro).
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveBenhNhanCoSoAsync(
        long idBenhNhan,
        long idCoSo,
        string maBN,
        bool moCuaTaiLieu = true) =>
        ExecuteWithIdAsync("dbo.DM_BenhNhanCoSo_Save", "@IDBenhNhanCoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@MaBN", DbType.AnsiString, maBN, 20);
            AddParameter(command, "@DaMoTaiLieu", DbType.Boolean, moCuaTaiLieu);
        });

    /// <summary>
    /// CUA DOI MA (chot 47, ADR 0032) — doi ma benh nhan cua mot ho so DA NOI
    /// sang ma khac. Viec CO Y, CO GHI SO, danh cho bo phan ho tro sua mot lan
    /// noi sai.
    ///
    /// 🔴 Tach khoi <see cref="SaveBenhNhanCoSoAsync"/> chu khong lam mot tham
    /// so cua no: login <c>spwa_his</c> co EXECUTE tren <c>DM_BenhNhanCoSo_Save</c>
    /// (Database/25), nen mot tham so cho-phep-bo-qua thi HIS chi can truyen co
    /// la xuyen rao. Tach cua thi hang rao giu bang GRANT — <c>spwa_his</c>
    /// KHONG duoc cap stored nay, nen HIS khong goi duoc du co muon.
    ///
    /// Tra <c>Code == 4</c> khi ho so chua ton tai (tao ho so la viec cua
    /// <see cref="SaveBenhNhanCoSoAsync"/>), <c>Code == 2</c> khi ma da thuoc ve
    /// nguoi khac tai co so do.
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> DoiMaBenhNhanCoSoAsync(
        long idBenhNhan,
        long idCoSo,
        string maBN,
        string? lyDo = null) =>
        ExecuteWithIdAsync("dbo.DM_BenhNhan_DoiMa", "@IDBenhNhanCoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
            AddParameter(command, "@MaBN", DbType.AnsiString, maBN, 20);
            AddParameter(command, "@LyDo", DbType.String, lyDo, 200);
        });

    public Task<(AdminStoredProcedureResult KetQua, long Id)> SaveTaiLieuBenhNhanAsync(
        long id,
        long idCoSo,
        long? idBenhNhan,
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
            AddParameter(command, "@IDBenhNhanCoSo", DbType.Int64, idBenhNhan);
            AddParameter(command, "@MaBN", DbType.String, maBN, 50);
            AddParameter(command, "@LoaiTaiLieu", DbType.String, loaiTaiLieu, 50);
            AddParameter(command, "@TenTaiLieu", DbType.String, tenTaiLieu, 255);
            // Tran 2000 -- xem Database/27_NANG_TRAN_DUONG_DAN_FTP.sql (con so o sau cho).
            AddParameter(command, "@DuongDanFtp", DbType.String, duongDanFtp, 2000);
            AddParameter(command, "@DungLuongByte", DbType.Int64, dungLuongByte);
            AddParameter(command, "@NgayKham", DbType.DateTime, ngayKham);
            AddParameter(command, "@GhiChu", DbType.String, ghiChu);
            AddParameter(command, "@MaNguonHIS", DbType.AnsiString, maNguonHIS, 50);
            AddParameter(command, "@BamNoiDung", DbType.AnsiStringFixedLength, bamNoiDung, 64);
        });

    // ------------------------------------------------------------------
    //  Khu API nhan (dot 2 giai doan 2)
    // ------------------------------------------------------------------

    /// <summary>Mot dong nhat ky doi soat. Thu tuc nay khong tra ResultCode.</summary>
    public Task GhiLogApiCoSoAsync(
        long? idCoSo,
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
            // Cot IDKhoa da bi xoa khoi HT_LogApiCoSo va tham so @IDKhoa da go khoi stored.
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
        long idBenhNhan,
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
            AddParameter(command, "@IDBenhNhanCoSo", DbType.Int64, idBenhNhan);
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

    /// <summary>
    /// Ghi MOT co so y te. Sau dot A, <c>DM_CSKCB</c> da nuot tron ba bang con
    /// (<c>DM_DoiTacApi</c>, <c>DM_CSKCB_QuangCao</c>, <c>HT_KhoFtpCoSo</c>) nen
    /// day la duong ghi DUY NHAT cho ca ket noi HIS, quang cao va kho FTP.
    ///
    /// 🔴 Ba o BI MAT — <c>@KetNoi_KhoaGoiHIS</c>, <c>@KhoaBam</c>, <c>@Ftp_MatKhau</c> —
    /// truyen NULL nghia la GIU NGUYEN gia tri cu (man Admin khong do mat khau ra
    /// man hinh nen khong the gui lai). Vi the o day TUYET DOI khong duoc bien
    /// null thanh chuoi rong: <c>CoSoYTeController.Normalize</c> da ep o trong ve
    /// null co y, bien no thanh "" la XOA TRANG khoa that.
    /// </summary>
    public Task<AdminStoredProcedureResult> SaveCoSoYTeAsync(CoSoYTeEditViewModel model) =>
        ExecuteAsync("dbo.DM_CSKCB_Save", command =>
        {
            AddParameter(command, "@ID", DbType.Int64, model.Id);
            AddParameter(command, "@MaCoSo", DbType.AnsiString, model.MaCoSo, 10);
            AddParameter(command, "@TenCoSo", DbType.String, model.TenCoSo, 200);
            AddParameter(command, "@Slug", DbType.AnsiString, model.Slug, 100);
            AddParameter(command, "@IDNhomCS", DbType.Int64, model.SelectedNhomCSId);
            AddParameter(command, "@DiaChi", DbType.String, model.DiaChi, 255);
            AddParameter(command, "@Tinh", DbType.Int32, model.Tinh);
            AddParameter(command, "@PhuongXa", DbType.Int32, model.PhuongXa);
            AddParameter(command, "@SDT", DbType.AnsiString, model.SDT, 20);
            AddParameter(command, "@Email", DbType.AnsiString, model.Email, 100);
            AddParameter(command, "@AnhBia", DbType.String, model.AnhBia, 500);
            AddParameter(command, "@Logo", DbType.String, model.Logo, size: -1);
            AddParameter(command, "@HienThiCongKhai", DbType.Boolean, model.HienThiCongKhai);
            AddParameter(command, "@QcSoTienDaTra", DbType.Decimal, model.QcSoTienDaTra,
                precision: 15, scale: 0);
            AddParameter(command, "@QcNoiDung", DbType.String, model.QcNoiDung, size: -1);
            AddParameter(command, "@QcAnh", DbType.String, model.QcAnh, size: -1);
            AddParameter(command, "@TenCongTy", DbType.String, model.TenCongTy, 200);

            // ---- Ket noi HIS (cu la bang 1-1 DM_DoiTacApi) ----------------------
            AddParameter(command, "@KetNoi_UrlChuyenHuong", DbType.String,
                model.KetNoi_UrlChuyenHuong, 255);
            AddParameter(command, "@KetNoi_BaseUrlHIS", DbType.String,
                model.KetNoi_BaseUrlHIS, 255);
            // 🔒 null = giu nguyen
            AddParameter(command, "@KetNoi_KhoaGoiHIS", DbType.String,
                model.KetNoi_KhoaGoiHIS, 500);
            AddParameter(command, "@KetNoi_Active", DbType.Boolean, model.KetNoi_Active);

            // ---- Khoa goi API cua co so (cu la HT_KhoaApiCoSo) -------------------
            // 🔒 Man Admin khong cap khoa bam qua duong nay ⇒ luon NULL = giu nguyen.
            AddParameter(command, "@KhoaBam", DbType.Binary, null, 32);
            AddParameter(command, "@Khoa_NgayHetHan", DbType.DateTime, null);

            // ---- Kho phieu co so: FTP (cu la HT_KhoFtpCoSo) ----------------------
            AddParameter(command, "@Ftp_Host", DbType.String, model.Ftp_Host, 200);
            AddParameter(command, "@Ftp_TaiKhoan", DbType.String, model.Ftp_TaiKhoan, 100);
            // 🔒 null = giu nguyen
            AddParameter(command, "@Ftp_MatKhau", DbType.String, model.Ftp_MatKhau, 200);
            AddParameter(command, "@Ftp_ThuMucGoc", DbType.String, model.Ftp_ThuMucGoc, 200);
            AddParameter(command, "@Ftp_Active", DbType.Boolean, model.Ftp_Active);
        });

    public Task<AdminStoredProcedureResult> DeleteCoSoYTeAsync(long id) =>
        ExecuteAsync("dbo.DM_CSKCB_Delete", command =>
            AddParameter(command, "@ID", DbType.Int64, id));

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

    /// <summary>
    /// 🔴 Dot 1B: tham so la <c>@IDBenhNhan</c> (tro <c>DM_BenhNhan</c>), KHONG con
    /// <c>@IDTaiKhoan</c> — C15/PA-2a. Sai ten o day thi BUILD VAN XANH va chi no
    /// luc chay: da bat duoc bang phep doi chieu tham so C# vs stored that (§10 muc 3).
    /// </summary>
    public Task<(AdminStoredProcedureResult KetQua, long Id)> SavePushDangKyAsync(
        long idBenhNhan,
        string endpoint,
        string p256dh,
        string auth) =>
        ExecuteWithIdAsync("dbo.HT_PushDangKy_Save", "@IDDangKy", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@Endpoint", DbType.String, endpoint, 2000);
            AddParameter(command, "@P256dh", DbType.String, p256dh, 1000);
            AddParameter(command, "@Auth", DbType.String, auth, 400);
            // Tham so @MaThietBi da bi go: cot HT_PushDangKy.IDThietBi va bang
            // HT_ThietBi deu khong con.
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
