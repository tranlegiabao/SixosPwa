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
/// MỌI đường ghi của ứng dụng đi qua đây (ADR 0008). EF chỉ còn dùng để đọc.
/// Tên lớp giữ nguyên từ thời chỉ khu Admin ghi DB; từ đợt tái kiến trúc nó phục
/// vụ cả luồng bệnh nhân.
/// </summary>
public sealed class AdminStoredProcedureService
{
    private readonly ApplicationDbContext _db;

    public AdminStoredProcedureService(ApplicationDbContext db) => _db = db;

    // ------------------------------------------------------------------
    //  Tài khoản
    // ------------------------------------------------------------------

    /// <summary>
    /// <paramref name="matKhauNoiBoDaBam"/> phải là chuỗi ĐÃ BĂM. Đợt này chưa thi
    /// hành phần băm nên mọi nơi gọi đều truyền null — xem mục Đính chính ADR 0009.
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
            // 🔴 Tham số @IDBenhNhan vẫn còn trong CHỮ KÝ stored (hợp đồng linked
            // server, ADR 0035 #2) nhưng không truyền từ đây: C# của cổng không
            // đụng tới. Từ đợt 1B stored chỉ còn nhận Role='Admin'; vai trò khác
            // bị ghi log rồi bỏ qua.
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

            // Result Set 1: Tài khoản
            while (await reader.ReadAsync())
            {
                var tk = new TaiKhoan
                {
                    Id = Convert.ToInt64(reader["ID"]),
                    SDT = reader["SDT"]?.ToString() ?? "",
                    Email = reader["Email"] != DBNull.Value ? reader["Email"]?.ToString() : null,
                    Role = reader["Role"]?.ToString() ?? "Admin",
                    // 🔴 Stored trả CoMatKhau (bit) chứ KHÔNG trả MatKhauNoiBo: một màn
                    // DANH SÁCH không được kéo mật khẩu ra khỏi CSDL. Đọc nhầm tên cột
                    // ở đây là IndexOutOfRangeException => màn báo "Lỗi tải danh sách
                    // tài khoản từ máy chủ" mà không nói vì sao.
                    // Gán một chỗ-giữ để chỗ nào chỉ hỏi "có mật khẩu chưa" vẫn đúng.
                    MatKhauNoiBo = (reader["CoMatKhau"] != DBNull.Value
                                    && Convert.ToBoolean(reader["CoMatKhau"])) ? "***" : null,
                    // Từ đợt 1B bệnh nhân KHÔNG còn tài khoản (ADR 0036) nên không còn
                    // Result Set 2 "hồ sơ theo tài khoản"; từ điển dưới đây luôn rỗng.
                    NgayTao = Convert.ToDateTime(reader["NgayTao"])
                };
                items.Add(tk);
                hoSoDict[tk.Id] = new List<HoSoBenhNhanItemViewModel>();

                if (tongSoDong == 0 && reader["TongSoDong"] != DBNull.Value)
                {
                    tongSoDong = Convert.ToInt32(reader["TongSoDong"]);
                }
            }

            // Result Set 2: Hồ sơ bệnh nhân
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
    //  Bệnh nhân
    // ------------------------------------------------------------------

    /// <summary>
    /// Thêm/cập nhật CON NGƯỜI, nhận diện bằng CCCD.
    ///
    /// <para>
    /// <paramref name="idTaiKhoan"/> là chủ sở hữu hồ sơ (ADR 0019). Thủ tục chỉ
    /// nhận chủ khi cột đang bỏ trống, và trả <c>ResultCode 3</c> khi CCCD đã
    /// thuộc một tài khoản KHÁC — "ai khai trước giữ CCCD".
    /// </para>
    /// <para>
    /// <paramref name="hoTenKhongDau"/> phải lấy từ <see cref="ChuanHoaTen.BoDau"/>,
    /// đừng tự chuẩn hóa kiểu khác — đó là ô thứ ba của luật gộp (ADR 0018).
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
            // Giữ tham số (hợp đồng linked server, ADR 0035 #1) nhưng stored đã
            // NHẬN RỒI BỎ: cột DM_BenhNhan.IDTaiKhoan bị xóa ở đợt 1B.
            AddParameter(command, "@IDTaiKhoan", DbType.Int64, idTaiKhoan);
            AddParameter(command, "@NgaySinh", DbType.DateTime, ngaySinh);
            AddParameter(command, "@HoTenKhongDau", DbType.String, hoTenKhongDau, 100);
            AddParameter(command, "@GioiTinh", DbType.AnsiString, gioiTinh, 10);
        });

    /// <summary>
    /// Màn *Sửa hồ sơ* (Đợt 4). 🔴 KHÔNG dùng lại <see cref="SaveBenhNhanAsync"/>:
    /// cái đó nhận diện bằng CCCD, mà ở đây người dùng ĐỔI ĐƯỢC cả CCCD — gọi Save
    /// với CCCD mới sẽ đẻ ra một CON NGƯỜI THỨ HAI thay vì sửa người đang có.
    ///
    /// Thủ tục tự chặn: không phải hồ sơ của mình (<c>ResultCode 5</c>), CCCD đâm
    /// vào người khác (<c>3</c>). Hồ sơ ĐÃ NỐI HIS thì bốn ô danh tính khóa cứng,
    /// chỉ còn số điện thoại sửa được — thủ tục tự bỏ qua, bên gọi không phải biết.
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
    /// *Gỡ nối* — tháo MỘT mã khỏi một hồ sơ (ADR 0024 vế 3).
    ///
    /// 🔴 Thủ tục XÓA CẢ tài liệu và đợt khám của dòng đó (sao lưu sang
    /// <c>bak.GoNoi_*_V001</c> trước). Không phải tùy chọn: khóa ngoại là
    /// NO_ACTION nên không xóa dòng được chừng nào còn con, và nếu mã bị nối NHẦM
    /// thì để tài liệu lại chính là giữ nguyên cái hại mà nút này sinh ra để chữa.
    /// Mất không vĩnh viễn — cửa <c>kiem-tra-nhan</c> sẽ đưa mã về trạng thái
    /// "chưa ai nhận" nên hàng đợi bên HIS đẩy lại được.
    /// </summary>
    public Task<AdminStoredProcedureResult> GoNoiAsync(long idBenhNhan, string sdt, long idCoSo) =>
        ExecuteAsync("dbo.DM_BenhNhan_GoNoi", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    /// <summary>
    /// Đổi *Mốc xem lịch* cho TẤT CẢ dòng của một con người tại một cơ sở (ADR 0025).
    /// Đổi từng dòng thì mở modal xong huy hiệu vẫn còn — ở *Lịch khám của tôi* gộp
    /// hết các mã lại thành một danh sách nên "đã xem" phải là một trạng thái duy nhất.
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
    /// Hồ sơ TỰ KHAI tại một cơ sở: <c>MaBN</c> để RỖNG vì cơ sở chưa cấp mã nào.
    ///
    /// 🔴 Tách khỏi <see cref="SaveBenhNhanCoSoAsync"/> chứ không gộp làm một:
    /// đường này khóa theo <c>(IDBenhNhan, IDCoSo)</c> trong phạm vi các dòng
    /// CHƯA nối HIS, còn đường kia khóa theo mã thật. Gộp lại thì phải so
    /// <c>MaBN = NULL</c>, mà trong SQL <c>NULL = NULL</c> không bao giờ đúng nên
    /// mỗi lần lưu sẽ đẻ một dòng mới.
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
    /// Xóa một hồ sơ để NHẢ CCCD ra (ADR 0019 mục 2) — không có nút này thì "ai
    /// khai trước giữ" thành cái bẫy không lối thoát cho chính chủ.
    /// Thủ tục tự chặn: không phải hồ sơ của mình, hồ sơ đã nối HIS, hoặc đã có
    /// dữ liệu khám.
    /// </summary>
    /// <summary>
    /// Xóa một hồ sơ tại cơ sở. Từ 1B chủ sở hữu là cặp (SDT phiên x cơ sở phiên),
    /// không còn <c>IDTaiKhoan</c> — xem ADR 0027 (đã đảo) và ADR 0040.
    /// </summary>
    public Task<AdminStoredProcedureResult> XoaHoSoAsync(long idBenhNhan, string sdt, long idCoSo) =>
        ExecuteAsync("dbo.DM_BenhNhan_XoaHoSo", command =>
        {
            AddParameter(command, "@IDBenhNhan", DbType.Int64, idBenhNhan);
            AddParameter(command, "@SDT", DbType.AnsiString, sdt, 20);
            AddParameter(command, "@IDCoSo", DbType.Int64, idCoSo);
        });

    /// <summary>
    /// Gán con người vào cơ sở kèm MÃ THẬT do cơ sở cấp — đường NỐI HIS.
    ///
    /// 🔴 Từ đợt V6b KHÔNG còn chỗ nào gọi: luồng đăng ký đã chuyển sang
    /// <see cref="TaoHoSoTuKhaiAsync"/> (hồ sơ tự khai, MaBN rỗng). Giữ lại vì
    /// màn *Nối hồ sơ* của đợt sau chính là chỗ dùng nó. Xóa đi rồi viết lại là
    /// mất đoạn chặn trùng MaBN đã chạy đúng.
    /// </summary>
    /// <summary>
    /// 🔴 <paramref name="moCuaTaiLieu"/> phải truyền TƯỜNG MINH. Thủ tục để mặc
    /// định <c>@DaMoTaiLieu = 1</c>, nên bỏ trống là mỗi lần nối đều MỞ TOANG cửa
    /// tài liệu — đúng cái cửa mà ADR 0020 dựng lên để chặn. Mặc định <c>true</c> ở
    /// đây giữ nguyên hành vi cũ cho màn Admin (đó là đường của bộ phận hỗ trợ).
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
    /// CỬA ĐỔI MÃ (chốt 47, ADR 0032) — đổi mã bệnh nhân của một hồ sơ ĐÃ NỐI
    /// sang mã khác. Việc CỐ Ý, CÓ GHI SỔ, dành cho bộ phận hỗ trợ sửa một lần
    /// nối sai.
    ///
    /// 🔴 Tách khỏi <see cref="SaveBenhNhanCoSoAsync"/> chứ không làm một tham
    /// số của nó: login <c>spwa_his</c> có EXECUTE trên <c>DM_BenhNhanCoSo_Save</c>
    /// (Database/25), nên một tham số cho-phép-bỏ-qua thì HIS chỉ cần truyền cờ
    /// là xuyên rào. Tách cửa thì hàng rào giữ bằng GRANT — <c>spwa_his</c>
    /// KHÔNG được cấp stored này, nên HIS không gọi được dù có muốn.
    ///
    /// Trả <c>Code == 4</c> khi hồ sơ chưa tồn tại (tạo hồ sơ là việc của
    /// <see cref="SaveBenhNhanCoSoAsync"/>), <c>Code == 2</c> khi mã đã thuộc về
    /// người khác tại cơ sở đó.
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
            // Trần 2000 -- xem Database/27_NANG_TRAN_DUONG_DAN_FTP.sql (con số ở sáu chỗ).
            AddParameter(command, "@DuongDanFtp", DbType.String, duongDanFtp, 2000);
            AddParameter(command, "@DungLuongByte", DbType.Int64, dungLuongByte);
            AddParameter(command, "@NgayKham", DbType.DateTime, ngayKham);
            AddParameter(command, "@GhiChu", DbType.String, ghiChu);
            AddParameter(command, "@MaNguonHIS", DbType.AnsiString, maNguonHIS, 50);
            AddParameter(command, "@BamNoiDung", DbType.AnsiStringFixedLength, bamNoiDung, 64);
        });

    // ------------------------------------------------------------------
    //  Khu API nhận (đợt 2 giai đoạn 2)
    // ------------------------------------------------------------------

    /// <summary>Một dòng nhật ký đối soát. Thủ tục này không trả ResultCode.</summary>
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
            // Cột IDKhoa đã bị xóa khỏi HT_LogApiCoSo và tham số @IDKhoa đã gỡ khỏi stored.
            AddParameter(command, "@Endpoint", DbType.AnsiString, endpoint, 100);
            AddParameter(command, "@MaBN", DbType.AnsiString, maBN, 20);
            AddParameter(command, "@MaNguonHIS", DbType.AnsiString, maNguonHIS, 50);
            AddParameter(command, "@KetQua", DbType.AnsiString, ketQua, 20);
            AddParameter(command, "@LyDo", DbType.AnsiString, lyDo, 50);
            AddParameter(command, "@SoLuong", DbType.Int32, soLuong);
            AddParameter(command, "@IpGoi", DbType.AnsiString, ipGoi, 45);
        });

    /// <summary>
    /// Lưu MỘT đợt khám. Gọi lặp cho cả lô từ <see cref="DotKhamService"/> —
    /// mỗi dòng tự quyết định thêm hay cập nhật theo khóa tự nhiên
    /// (IDCoSo, MaVaoVien), nên đẩy lại cả lô không đẻ dòng trùng.
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
    //  Cơ sở y tế
    // ------------------------------------------------------------------

    /// <summary>
    /// Ghi MỘT cơ sở y tế. Sau đợt A, <c>DM_CSKCB</c> đã nuốt trọn ba bảng con
    /// (<c>DM_DoiTacApi</c>, <c>DM_CSKCB_QuangCao</c>, <c>HT_KhoFtpCoSo</c>) nên
    /// đây là đường ghi DUY NHẤT cho cả kết nối HIS, quảng cáo và kho FTP.
    ///
    /// 🔴 Ba ô BÍ MẬT — <c>@KetNoi_KhoaGoiHIS</c>, <c>@KhoaBam</c>, <c>@Ftp_MatKhau</c> —
    /// truyền NULL nghĩa là GIỮ NGUYÊN giá trị cũ (màn Admin không đổ mật khẩu ra
    /// màn hình nên không thể gửi lại). Vì thế ở đây TUYỆT ĐỐI không được biến
    /// null thành chuỗi rỗng: <c>CoSoYTeController.Normalize</c> đã ép ô trống về
    /// null cố ý, biến nó thành "" là XÓA TRẮNG khóa thật.
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

            // ---- Kết nối HIS (cũ là bảng 1-1 DM_DoiTacApi) ----------------------
            AddParameter(command, "@KetNoi_UrlChuyenHuong", DbType.String,
                model.KetNoi_UrlChuyenHuong, 255);
            AddParameter(command, "@KetNoi_BaseUrlHIS", DbType.String,
                model.KetNoi_BaseUrlHIS, 255);
            // 🔒 null = giữ nguyên
            AddParameter(command, "@KetNoi_KhoaGoiHIS", DbType.String,
                model.KetNoi_KhoaGoiHIS, 500);
            AddParameter(command, "@KetNoi_Active", DbType.Boolean, model.KetNoi_Active);

            // ---- Khóa gọi API của cơ sở (cũ là HT_KhoaApiCoSo) -------------------
            // 🔒 Màn Admin không cấp khóa băm qua đường này ⇒ luôn NULL = giữ nguyên.
            AddParameter(command, "@KhoaBam", DbType.Binary, null, 32);
            AddParameter(command, "@Khoa_NgayHetHan", DbType.DateTime, null);

            // ---- Kho phiếu cơ sở: FTP (cũ là HT_KhoFtpCoSo) ----------------------
            AddParameter(command, "@Ftp_Host", DbType.String, model.Ftp_Host, 200);
            AddParameter(command, "@Ftp_TaiKhoan", DbType.String, model.Ftp_TaiKhoan, 100);
            // 🔒 null = giữ nguyên
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
    /// Ghi giờ làm việc của MỘT ngày. Màn Sửa gọi lặp 7 lần (Thứ 0..6).
    /// Gọi lặp KHÔNG nguyên tử qua cả tuần, nhưng mỗi lần là upsert idempotent
    /// theo khóa UNIQUE (IDCoSo, Thu) nên chạy lại an toàn.
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
    /// Xóa các ngày KHÔNG còn được chọn. <paramref name="danhSachThuGiuLai"/>
    /// rỗng = xóa hết giờ của cơ sở đó.
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
    //  Thông báo / thiết bị / push
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
    /// 🔴 Đợt 1B: tham số là <c>@IDBenhNhan</c> (trỏ <c>DM_BenhNhan</c>), KHÔNG còn
    /// <c>@IDTaiKhoan</c> — C15/PA-2a. Sai tên ở đây thì BUILD VẪN XANH và chỉ nổ
    /// lúc chạy: đã bắt được bằng phép đối chiếu tham số C# vs stored thật (§10 mục 3).
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
            // Tham số @MaThietBi đã bị gỡ: cột HT_PushDangKy.IDThietBi và bảng
            // HT_ThietBi đều không còn.
        });

    /// <summary>Dọn một subscription đã hết hạn (browser trả 404/410).</summary>
    public Task XoaPushDangKyAsync(string endpoint) =>
        ExecuteNoResultAsync("dbo.HT_PushDangKy_Xoa", command =>
            AddParameter(command, "@Endpoint", DbType.String, endpoint, 2000));

    // ------------------------------------------------------------------
    //  Hạ tầng gọi thủ tục
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
    /// Như <see cref="ExecuteAsync"/> nhưng thủ tục còn trả về khóa chính vừa ghi
    /// qua một tham số OUTPUT riêng.
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
    /// Cho những thủ tục KHÔNG khai báo @ResultCode/@ResultMessage. Thêm hai tham số
    /// đó vào là SQL Server báo lỗi thừa tham số, nên phải có đường gọi riêng.
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
    /// Chạy một thủ tục trả về bảng, lấy MỘT chuỗi.
    /// <paramref name="tenCot"/> BẮT BUỘC đọc theo TÊN chứ không theo thứ tự:
    /// DM_CSKCB_NoiDung_Get trả cả dòng (cột 0 là ID bigint), đọc theo thứ tự
    /// là ném InvalidCastException => HTTP 500. Đã dính ở Đợt 3.
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
