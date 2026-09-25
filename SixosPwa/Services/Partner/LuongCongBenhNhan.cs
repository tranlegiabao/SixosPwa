using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SixosPwa.Data;
using SixosPwa.Models;
using SixosPwa.Services;

namespace SixosPwa.Services.Partner;

/// <summary>
/// Cây quyết định của cổng bệnh nhân: sau khi xác thực thì đi đâu, tạo hồ sơ nội
/// bộ thế nào. Tách khỏi controller để các màn dùng chung MỘT cây, không ai tự
/// chế lại.
///
/// 🔴 Đợt A đã gỡ HẾT bộ màn đối tác (ADR 0014): không còn IPartnerGateway,
/// không còn HT_TaiKhoanDoiTac, không còn màn Bàn giao. Cơ sở có trang riêng thì
/// ChonDichDenAsync trả thẳng <c>DM_CSKCB.KetNoi_UrlChuyenHuong</c> — xem
/// <see cref="CuaCoSoService"/>.
/// </summary>
public interface ILuongCongBenhNhan
{
    /// <summary>Chờ hạ cánh sau khi OTP đúng.</summary>
    Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl,
                                  DanhTinhQuet? quet = null, CancellationToken ct = default);

    /// <summary>Cơ sở có đang hiển thị công khai không (DM_CSKCB.HienThiCongKhai). ADR 0013.</summary>
    Task<bool> CoSoDangHienThiAsync(string? maCoSo, CancellationToken ct = default);

    /// <summary>Tạo hồ sơ nội bộ + đặt mật khẩu nội bộ.</summary>
    Task<KetQuaBuoc> MoTaiKhoanAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, string matKhau, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>Đổi mật khẩu nội bộ. Cơ sở có cửa riêng thì từ chối — mật khẩu là của họ.</summary>
    Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default);
}

/// <summary>Kết quả một bước có đích đến kế tiếp.</summary>
/// <param name="DoiTacHong">Chuyển tiếp từ <see cref="KetQuaThaoTac.DoiTacHong"/> — xem chú thích ở đó.</param>
public record KetQuaBuoc(bool ThanhCong, string ThongBao, string? DichDen, bool DoiTacHong = false);

public class LuongCongBenhNhan : ILuongCongBenhNhan
{
    /// <summary>Claim giữ số CCCD của bệnh nhân trong phiên.</summary>
    public const string ClaimCccd = "Cccd";

    /// <summary>Claim giữ mã cơ sở bệnh nhân đang dùng trong phiên.</summary>
    public const string ClaimMaCoSo = "MaCoSo";

    /// <summary>
    /// Claim giữ ID của *hồ sơ đang chọn* — con người nào trong số các hồ sơ của
    /// tài khoản đang được xem (ADR 0019). Một tài khoản quản nhiều hồ sơ: con
    /// đặt khám cho mẹ, mẹ theo dõi kết quả cho con.
    ///
    /// <para>
    /// 🔴 Đổi hồ sơ = PHÁT LẠI claim này (khuôn <c>DangKyOnlineUB</c>:
    /// <c>ThemIdXemThongTinBenhNhan</c> gọi <c>AddClaimsAsync</c>). Màn chỉ đọc,
    /// không bao giờ nhận ID hồ sơ từ tham số URL — nhận từ URL thì gõ số khác
    /// là xem được hồ sơ người ta.
    /// </para>
    /// <para>
    /// Thiếu claim này (phiên cũ đang sống, hoặc tài khoản một hồ sơ) thì các màn
    /// tự chọn hồ sơ đầu tiên — xem <c>HomeController.LayHoSoDangDungAsync</c>.
    /// </para>
    /// </summary>
    public const string ClaimHoSoDangChon = "HoSoDangChon";

    /// <summary>
    /// Dấu ấn: phiên này do CHÍNH đối tác xác thực (mật khẩu thật hoặc mã SMS của
    /// họ), không phải OTP của SixosPwa. Chỉ CapPhienBenhNhanAsync đóng dấu này.
    ///
    /// 🔴 Đây là thứ duy nhất phân biệt một phiên THẬT với một phiên đúc từ
    /// XacNhanOtp — action đó gọi trần được, mà OTP còn đang kê tạm một giá trị cố
    /// định, và cả Cccd lẫn MaCoSo đều lấy thẳng từ thân request. Thiếu dấu ấn này
    /// thì ai biết CCCD của người khác cũng mở được màn Bàn giao và đọc được mật
    /// khẩu thật của họ. Xem ADR 0016.
    /// </summary>
    public const string ClaimDoiTacXacThuc = "DoiTacXacThuc";


    private readonly ApplicationDbContext _db;
    private readonly CuaCoSoService _cua;
    private readonly ILogger<LuongCongBenhNhan> _logger;
    private readonly AdminStoredProcedureService _thuTuc;

    public LuongCongBenhNhan(
        ApplicationDbContext db,
        CuaCoSoService cua,
        ILogger<LuongCongBenhNhan> logger,
        AdminStoredProcedureService thuTuc)
    {
        _db = db;
        _cua = cua;
        _logger = logger;
        _thuTuc = thuTuc;
    }

    /// <summary>Đổi mã cơ sở (chuỗi) sang khóa chính DM_CSKCB.</summary>
    private Task<long?> LayIdCoSoAsync(string maCoSo, CancellationToken ct) =>
        _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == maCoSo)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Cơ sở có đang hiển thị công khai không (DM_CSKCB.HienThiCongKhai — tên cũ là
    /// <c>Active</c>). Đây là cổng DUY NHẤT quyết định cơ sở có nhận ĐĂNG NHẬP /
    /// ĐĂNG KÝ MỚI hay không. Không tìm thấy mã cơ sở thì trả false (hỏng theo
    /// hướng an toàn). Xem ADR 0013.
    ///
    /// CẢNH BÁO: đừng nhầm với <c>KetNoi_Active</c> (công tắc đường kết nối HIS) —
    /// hai công tắc khác nhau, cùng nằm trên một bảng.
    /// </summary>
    public Task<bool> CoSoDangHienThiAsync(string? maCoSo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(maCoSo)) return Task.FromResult(false);

        var ma = maCoSo.Trim();
        return _db.DMCSKCBs.AsNoTracking()
            .Where(x => x.MaCoSo == ma)
            .Select(x => x.HienThiCongKhai)
            .FirstOrDefaultAsync(ct);
    }

    // ------------------------------------------------------------------
    //  Cây quyết định sau OTP
    // ------------------------------------------------------------------

    public async Task<string> ChonDichDenAsync(string maCoSo, string cccd, string dinhDanh, string? returnUrl,
                                               DanhTinhQuet? quet = null, CancellationToken ct = default)
    {
        // 🔴 Đợt A: "cơ sở có cửa riêng không" nay là DỮ LIỆU
        // (DM_CSKCB.KetNoi_UrlChuyenHuong), không còn là kiểu bản cài
        // (DM_DoiTacApi.KieuApi đã bị bỏ). Có URL thì chuyển hướng THẲNG sang
        // trang của cơ sở; không có thì ở lại trang bệnh nhân nội bộ.
        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
        var cua = idCoSo is null ? null : await _cua.LayCuaAsync(idCoSo.Value);

        // 🔴 Hồ sơ nội bộ phải có TRƯỚC khi trả bất kỳ đích đến nào — kể cả đường
        // chuyển hướng sang trang của cơ sở. Đường đối tác cũ cũng làm đúng thứ tự
        // này (GhiHoSoRoiChoBanGiaoAsync gọi TaoHoSoNoiBoAsync rồi mới bàn giao).
        // Trả URL trước rồi mới tính chuyện tạo hồ sơ thì bệnh nhân xong OTP sẽ được
        // phát cookie và bay thẳng sang cơ sở, mà bên này KHÔNG có dòng DM_BenhNhan /
        // DM_BenhNhanCoSo / HT_TaiKhoan nào: quay lại /benh-nhan là hồ sơ trống trơn,
        // còn TaiLieuService thì ném ChuaCoNguoiNhanException vì không nhận ra họ.
        await BaoDamHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, quet, ct);

        if (cua?.UrlChuyenHuong is { Length: > 0 } urlChuyenHuong)
        {
            _logger.LogInformation("Co so {MaCoSo} co cua rieng — chuyen huong sang {Url}", maCoSo, urlChuyenHuong);
            return urlChuyenHuong;
        }

        // Một tài khoản quản nhiều hồ sơ (ADR 0019) => phải biết đang xem AI
        // trước khi vào trang bệnh nhân. Bám khuôn DangKyOnlineUB: đăng nhập
        // xong là về màn chọn hồ sơ (HT_DangNhap_FE.js:35 đẩy thẳng tới
        // /QuanLy/QL_HoSoBenhNhan).
        //
        // Khác UB ở một chỗ: chỉ bắt chọn khi THẬT SỰ có trên một hồ sơ. Bên
        // UB ai cũng nhiều hồ sơ nên họ luôn qua màn đó; bên này phần lớn tài
        // khoản chỉ có đúng một hồ sơ, bắt họ bấm thêm một lần là phiền vô ích
        // — một hồ sơ thì không có gì để chọn.
        // Nếu người bệnh quét mã trên phiếu khám HIS, ta đã tự động chọn đúng hồ sơ đó,
        // nên đưa thẳng vào trang chủ /benh-nhan thay vì bắt quay về màn danh sách hồ sơ /benh-nhan/ho-so.
        if (quet != null && (quet.LaNguonHis || !string.IsNullOrWhiteSpace(quet.MaBN)))
        {
            return (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl != "/" && returnUrl != "/Home" && !returnUrl.StartsWith("/DangNhap"))
                ? returnUrl
                : "/benh-nhan";
        }

        var soHoSo = await DemHoSoTaiCoSoAsync(maCoSo, dinhDanh, ct);
        return soHoSo > 1 ? "/benh-nhan/ho-so" : "/benh-nhan";
    }

    // ------------------------------------------------------------------
    //  Màn Đăng ký
    // ------------------------------------------------------------------

    public async Task<KetQuaBuoc> MoTaiKhoanAsync(string maCoSo, string cccd, string dinhDanh, string hoTen, string matKhau, string? returnUrl = null, CancellationToken ct = default)
    {
        // Cơ sở đang ẩn thì không mở tài khoản mới. Phiên CŨ vẫn dùng bình thường —
        // cửa này là đường tạo MỚI có chủ đích, không phải đường dùng lại. ADR 0013.
        if (!await CoSoDangHienThiAsync(maCoSo, ct))
        {
            return new KetQuaBuoc(false, "Cơ sở này đang tạm ngưng tiếp nhận đăng ký trực tuyến.", null);
        }

        _ = await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, hoTen, ct: ct);

        // 🔴 Đợt 1B: KHÔNG còn ghi mật khẩu nội bộ cho bệnh nhân — họ không có
        // tài khoản nữa, đăng nhập bằng OTP (ADR 0036). Cột HT_TaiKhoan.MatKhauNoiBo
        // chỉ còn phục vụ Admin.

        // Cùng luật với ChonDichDenAsync — hai lối vào (đăng nhập / đăng ký) phải
        // đi cùng một đường, nếu không người dùng thấy hai hành vi khác nhau cho
        // cùng một trạng thái.
        var soHoSo = await DemHoSoTaiCoSoAsync(maCoSo, dinhDanh, ct);
        return new KetQuaBuoc(true, "Đã tạo tài khoản",
            soHoSo > 1 ? "/benh-nhan/ho-so" : "/benh-nhan");
    }

    // ------------------------------------------------------------------
    //  Màn Đổi mật khẩu
    // ------------------------------------------------------------------

    /// <summary>
    /// Băm mật khẩu nội bộ. Dùng <c>PasswordHasher&lt;TaiKhoan&gt;</c> của ASP.NET Core
    /// (có sẵn trong shared framework, không phải thêm gói NuGet) — đúng ba việc mà
    /// mục "Điều kiện để gỡ đính chính" của ADR 0009 chỉ định.
    /// </summary>
    private static readonly IPasswordHasher<TaiKhoan> BamMatKhau = new PasswordHasher<TaiKhoan>();

    /// <summary>
    /// Ghi mật khẩu nội bộ vào <c>HT_TaiKhoan.MatKhauNoiBo</c> qua thủ tục
    /// <c>HT_TaiKhoan_Save</c> (mọi đường ghi đi qua stored — ADR 0008).
    ///
    /// 🔴 Thay cho <c>HT_TaiKhoanDoiTac</c> đã bị xóa ở đợt A: mật khẩu không còn
    /// cắt theo TỪNG CƠ SỞ nữa, vì không còn hệ đối tác nào để bàn giao sang.
    ///
    /// 🔴 BẮT BUỘC BĂM TRƯỚC KHI TRUYỀN (ADR 0009): tham số của thủ tục tên là
    /// <c>@MatKhauNoiBoDaBam</c> và tầng T-SQL KHÔNG BAO GIỜ tự băm. Truyền chuỗi
    /// thô vào đây là để mật khẩu bệnh nhân tự chọn nằm nguyên văn trên đúng cái cột
    /// mà đường đăng nhập Admin/DoiTac đem ra so — đổi Role một cái là chuỗi đó mở
    /// được khu quản trị. ADR 0005 (lưu không băm) chỉ áp cho mật khẩu ĐỐI TÁC, và
    /// bảng đó (<c>HT_TaiKhoanDoiTac</c>) đã bị xóa ở đợt A.
    /// </summary>
    private Task GhiMatKhauNoiBoAsync(TaiKhoan taiKhoan, string matKhau) =>
        _thuTuc.SaveTaiKhoanAsync(
            taiKhoan.Id,
            taiKhoan.SDT,
            taiKhoan.Email,
            string.IsNullOrWhiteSpace(taiKhoan.Role) ? "BenhNhan" : taiKhoan.Role,
            string.IsNullOrEmpty(matKhau) ? null : BamMatKhau.HashPassword(taiKhoan, matKhau));

    public async Task<KetQuaThaoTac> DoiMatKhauAsync(string maCoSo, ClaimsPrincipal nguoiDung, string matKhauMoi, CancellationToken ct = default)
    {
        var cccd = LayClaim(nguoiDung, ClaimCccd);
        if (string.IsNullOrWhiteSpace(cccd))
        {
            return new KetQuaThaoTac(false, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại");
        }

        // 🔴 Đợt 1B: bệnh nhân KHÔNG CÒN tài khoản, nên cũng không còn mật khẩu
        // nội bộ để đổi (HT_TaiKhoan chỉ còn Admin — ADR 0036). Bản trung gian của
        // đợt này trả "Không tìm thấy tài khoản của bạn" — ĐÚNG kết quả nhưng SAI
        // nguyên nhân, người dùng sẽ đi tìm lại tài khoản không tồn tại.
        // Màn này đang là nợ của đợt A (§7): hoặc bỏ hẳn, hoặc cho nó đúng thật.
        return new KetQuaThaoTac(false,
            "Cổng không còn dùng mật khẩu cho bệnh nhân — bạn đăng nhập bằng mã OTP gửi tới số điện thoại.");
    }


    // ------------------------------------------------------------------
    //  Hồ sơ nội bộ
    // ------------------------------------------------------------------

    /// <summary>
    /// CCCD này thuộc DM_BenhNhan chứ không còn nằm trên tài khoản — 5/18 tài khoản
    /// là Admin/DoiTac, không có CCCD là ĐÚNG chứ không phải dữ liệu thiếu.
    /// </summary>
    private async Task<TaiKhoan?> TimTaiKhoanAsync(string cccd, CancellationToken ct)
    {
        // 🔴 Đợt 1B: bệnh nhân KHÔNG CÒN tài khoản (HT_TaiKhoan chỉ còn Admin),
        // nên không còn đường nào đi từ CCCD sang tài khoản. Xem ADR 0040.
        TaiKhoan? theoChuSoHuu = null;

        // 🔴 Đợt A đã bỏ cột HT_TaiKhoan.IDBenhNhan, nên nhánh lùi về chiều CŨ
        // (join t.IdBenhNhan = p.ID) không còn nữa. Script 09 đã dồn hết dữ liệu
        // sang DM_BenhNhan.IDTaiKhoan từ trước — ADR 0019.
        return theoChuSoHuu;
    }



    /// <summary>
    /// Số hồ sơ mà tài khoản này đang quản TẠI MỘT CƠ SỞ — dùng để quyết định có
    /// phải qua màn chọn hồ sơ không.
    ///
    /// Phải khớp đúng điều kiện của <c>HomeController.LayHoSoDangDungAsync</c>
    /// (lọc theo tài khoản + <c>DaMoTaiLieu</c>), nếu không sẽ có cảnh: đếm ra 2
    /// nên đẩy sang màn chọn, mà màn kia chỉ hiện 1.
    /// </summary>
    private async Task<int> DemHoSoTaiCoSoAsync(string maCoSo, string dinhDanh, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dinhDanh) || string.IsNullOrWhiteSpace(maCoSo)) return 0;

        var idTaiKhoan = await _db.TaiKhoans.AsNoTracking()
            .Where(t => t.SDT == dinhDanh || t.Email == dinhDanh)
            .Select(t => (long?)t.Id)
            .FirstOrDefaultAsync(ct);

        // Đợt 1B: phạm vi là cặp (SDT x cơ sở) — luật C2.
        return await (
            from h in _db.BenhNhans.AsNoTracking()
            join cs in _db.DMCSKCBs.AsNoTracking() on h.IdCoSo equals (long?)cs.Id
            where (h.SDT == dinhDanh || h.Email == dinhDanh)
                  && cs.MaCoSo == maCoSo
                  && h.DaMoTaiLieu
            select h.Id).CountAsync(ct);
    }

    /// <summary>
    /// 🔴 Từ đợt 1B KHÔNG trả <c>TaiKhoan</c> nữa. Bản trung gian kết thúc bằng
    /// <c>_db.TaiKhoans.FirstAsync(...)</c>, mà <c>HT_TaiKhoan_Save</c> nay no-op
    /// với vai trò khác Admin nên <c>idTaiKhoan</c> = 0 =>
    /// <c>InvalidOperationException: Sequence contains no elements</c> ngay cuối
    /// bước xác nhận OTP. Trả ID HỒ SƠ vừa dùng.
    /// </summary>
    private async Task<long> TaoHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh, string hoTen,
                                                   DanhTinhQuet? quet = null, CancellationToken ct = default)
    {
        var laEmail = dinhDanh.Contains('@');
        var sdt = laEmail ? string.Empty : dinhDanh;
        var email = laEmail ? dinhDanh : null;

        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);

        // Con người trước (khóa CCCD), rồi mới đến hồ sơ tại cơ sở.
        // Tên/ngày sinh/giới tính/địa chỉ tính ở một chỗ duy nhất, để luật "chỉ điền ô
        // trống" không bị viết lại hai kiểu ở hai đường.
        var o = await TinhOCanDienAsync(cccd, dinhDanh, hoTen, quet, ct);

        var luuNguoi = await _thuTuc.SaveBenhNhanAsync(
            cccd,
            o.Ten,
            sdt,
            email,
            o.DiaChi,
            // Chủ sở hữu chưa biết ở bước này — tài khoản được tạo SAU. Nhận chủ
            // ở cuối hàm bằng DM_BenhNhan_NhanChuSoHuu.
            idTaiKhoan: null,
            ngaySinh: o.Ngay,
            // Ô thứ ba của luật gộp (ADR 0018). Chuẩn hóa MỘT BẢN DUY NHẤT qua
            // ChuanHoaTen — đừng tự bỏ dấu kiểu khác ở chỗ khác.
            hoTenKhongDau: o.Slug,
            gioiTinh: o.GioiTinh);

        // 🔴 KHÔNG còn bịa mã bệnh nhân (chốt 12 đợt 1). Hồ sơ vừa tạo là *tự
        // khai*: cơ sở chưa cấp mã nào cho người này, nên MaBN để RỖNG. Trước đây
        // cổng sinh BN-yyyyMMdd-#### cho có chỗ lấp, nhưng đó không phải mã cơ sở
        // cấp nên nó đánh lừa cả người đọc dữ liệu lẫn đường nhận tài liệu.
        //
        // Thủ tục tự lo chống trùng, và tự bỏ qua nếu người này ĐÃ có hồ sơ nối
        // HIS tại cơ sở — lúc đó không cần thêm dòng tự khai.
        if (idCoSo is not null && luuNguoi.Id > 0)
        {
            if (!string.IsNullOrWhiteSpace(quet?.MaBN))
            {
                await _thuTuc.SaveBenhNhanCoSoAsync(luuNguoi.Id, idCoSo.Value, quet.MaBN.Trim(), true);
            }
            else
            {
                await _thuTuc.TaoHoSoTuKhaiAsync(luuNguoi.Id, idCoSo.Value);
            }
        }

        var taiKhoan = await TimTaiKhoanAsync(cccd, ct)
                       ?? await _db.TaiKhoans.FirstOrDefaultAsync(x => x.SDT == dinhDanh, ct);

        // MatKhauNoiBo để null: đợt này chưa thi hành phần băm (Đính chính ADR 0009).
        var luuTaiKhoan = await _thuTuc.SaveTaiKhoanAsync(
            taiKhoan?.Id ?? 0,
            string.IsNullOrWhiteSpace(sdt) ? (taiKhoan?.SDT ?? dinhDanh) : sdt,
            email ?? taiKhoan?.Email,
            taiKhoan?.Role ?? "BenhNhan",
            null);

        var idTaiKhoan = luuTaiKhoan.Id > 0 ? luuTaiKhoan.Id : (taiKhoan?.Id ?? 0);

        // Nhận chủ sở hữu (ADR 0019). Đã có chủ KHÁC thì thủ tục trả ResultCode 3
        // và không đổi gì — đây là "ai khai trước giữ CCCD".
        //
        // 🔴 KHÔNG chặn đăng nhập khi trùng chủ: phiên vẫn phải vào được, chỉ là
        // hồ sơ đó không thuộc về tài khoản này. Chặn ở đây thì người gõ nhầm một
        // số CCCD sẽ bị khóa hoàn toàn khỏi cổng mà không hiểu vì sao. Màn *Hồ sơ
        // của tôi* mới là chỗ hiện lỗi và chỉ đường ra.
        // 🔴 Cửa 3 "nhận chủ sở hữu" đã chết từ đợt 1B: cột DM_BenhNhan.IDTaiKhoan
        // không còn, và "hồ sơ thuộc về ai" nay là cặp (SDT x cơ sở) của chính
        // dòng đó. Xem ADR 0040 và CONTEXT.md mục *Lối vào*.

        return luuNguoi.Id;
    }

    /// <summary>
    /// Bệnh nhân vào thẳng nhánh nội bộ thì chưa qua màn Đăng ký nên chưa có tên.
    /// Tạo hồ sơ tối thiểu để trang bệnh nhân có cái mà chào.
    /// </summary>
    private async Task BaoDamHoSoNoiBoAsync(string maCoSo, string cccd, string dinhDanh,
                                            DanhTinhQuet? quet, CancellationToken ct)
    {
        // Không được dừng ở "đã có tài khoản": tài khoản là một, nhưng hồ sơ thì
        // MỖI CƠ SỞ MỘT CÁI. Bệnh nhân từng dùng cơ sở A sang cơ sở B mà chỉ kiểm
        // tài khoản thì B không có hồ sơ nào, và trang bệnh nhân không biết chào ai.
        var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
        if (idCoSo is null) return;

        var daCoHoSo = await _db.BenhNhans
            .AnyAsync(h => (h.SDT == dinhDanh || h.Email == dinhDanh)
                        && h.IdCoSo == idCoSo.Value, ct);

        if (daCoHoSo)
        {
            // Đã có hồ sơ rồi thì không phải dựng thêm. NHƯNG nếu bệnh nhân vừa quét mã
            // thì đây là cơ hội điền những ô còn trống (chốt 11/09) — nhất là nhóm hồ sơ
            // đang mang tên là SỐ ĐIỆN THOẠI, do đăng ký bằng OTP tự đẻ ra.
            if (quet is not null) await DienOTrongTuMaAsync(maCoSo, cccd, dinhDanh, quet, ct);
            return;
        }

        await TaoHoSoNoiBoAsync(maCoSo, cccd, dinhDanh, string.Empty, quet, ct);
    }

    /// <summary>
    /// Điền dữ liệu từ mã QR vào hồ sơ ĐÃ CÓ — điền những ô đang TRỐNG, cộng với ô tên khi
    /// tên cũ là RÁC (bằng đúng định danh). Lưu MaBN nếu có quét được từ phiếu khám.
    /// </summary>
    private async Task DienOTrongTuMaAsync(string maCoSo, string cccd, string dinhDanh, DanhTinhQuet quet, CancellationToken ct)
    {
        var o = await TinhOCanDienAsync(cccd, dinhDanh, string.Empty, quet, ct);

        // Chỉ gọi SaveBenhNhan khi có ít nhất một trường thay đổi
        if (!string.IsNullOrEmpty(o.Ten) || o.Ngay is not null
            || !string.IsNullOrWhiteSpace(o.GioiTinh) || !string.IsNullOrWhiteSpace(o.DiaChi))
        {
            await _thuTuc.SaveBenhNhanAsync(
                cccd, o.Ten, null, null, o.DiaChi,
                idTaiKhoan: null,
                ngaySinh: o.Ngay,
                hoTenKhongDau: o.Slug,
                gioiTinh: o.GioiTinh);
        }

        // Lưu/cập nhật MaBN vào DM_BenhNhanCoSo nếu quét được mã trên phiếu khám
        if (!string.IsNullOrWhiteSpace(quet?.MaBN))
        {
            var idCoSo = await LayIdCoSoAsync(maCoSo, ct);
            if (idCoSo is not null)
            {
                var idTaiKhoan = await _db.TaiKhoans.AsNoTracking()
                    .Where(t => t.SDT == dinhDanh || t.Email == dinhDanh)
                    .Select(t => (long?)t.Id)
                    .FirstOrDefaultAsync(ct);

                var idBenhNhan = await _db.BenhNhans.AsNoTracking()
                    .Where(p => p.IdCoSo == idCoSo.Value
                             && (p.SDT == dinhDanh || p.Email == dinhDanh
                                 || (!string.IsNullOrWhiteSpace(cccd) && !LaMaGia(cccd) && p.CCCD == cccd)))
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(ct);

                if (idBenhNhan > 0)
                {
                    var hoSoCoSo = await _db.BenhNhans.AsNoTracking()
                        .FirstOrDefaultAsync(h => h.Id == idBenhNhan && h.IdCoSo == idCoSo.Value, ct);

                    if (hoSoCoSo == null || string.IsNullOrWhiteSpace(hoSoCoSo.MaBN))
                    {
                        await _thuTuc.SaveBenhNhanCoSoAsync(idBenhNhan, idCoSo.Value, quet.MaBN.Trim(), true);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 🔴 Thi hành chốt 11/09: <b>dữ liệu từ mã QR chỉ được điền vào ô TRỐNG hoặc ô RÁC</b>,
    /// không đè lên bản bệnh nhân đã tự sửa tay.
    ///
    /// Cách thi hành dựa hẳn vào <c>DM_BenhNhan_Save</c>: mọi cột đều là
    /// <c>ISNULL(NULLIF(@X,''), X)</c> nên <b>gửi NULL (hay chuỗi rỗng cho tên) là giữ
    /// nguyên bản cũ</b> — khỏi phải sửa thủ tục. Chỉ cần ở đây quyết định gửi gì.
    ///
    /// Hai chỗ phải cẩn thận:
    ///  · Hồ sơ CHƯA có thì nhánh INSERT lấy THẲNG <c>@TenBN</c>, nên lúc đó buộc phải gửi
    ///    tên thật chứ không được gửi rỗng.
    ///  · CCCD là <b>mã giả</b> thì tra cứu theo CCCD vô nghĩa (nhiều hồ sơ chung một mã,
    ///    từ sau chỉ mục có lọc <c>19_</c>) — bỏ qua, cứ xử như hồ sơ mới.
    /// </summary>
    private async Task<(string Ten, DateTime? Ngay, string? GioiTinh, string? DiaChi, string? Slug)>
        TinhOCanDienAsync(string cccd, string dinhDanh, string hoTen, DanhTinhQuet? quet, CancellationToken ct)
    {
        var cu = (!string.IsNullOrWhiteSpace(cccd) && !LaMaGia(cccd))
            ? await _db.BenhNhans.AsNoTracking().FirstOrDefaultAsync(x => x.CCCD == cccd, ct)
            : null;

        var tenMuon = !string.IsNullOrWhiteSpace(hoTen) ? hoTen.Trim()
                    : !string.IsNullOrWhiteSpace(quet?.HoTen) ? quet!.HoTen!.Trim()
                    : dinhDanh;

        // Tên cũ là RÁC khi nó bằng đúng định danh: đó là hồ sơ tự đẻ ra lúc đăng ký bằng
        // OTP, chưa bao giờ đi qua màn *Thêm hồ sơ*.
        var tenCuLaRac = cu is null
                         || string.IsNullOrWhiteSpace(cu.TenBN)
                         || string.Equals(cu.TenBN.Trim(), dinhDanh, StringComparison.OrdinalIgnoreCase);

        var ten  = tenCuLaRac ? tenMuon : string.Empty;
        var slug = string.IsNullOrEmpty(ten) ? cu?.HoTenKhongDau : ChuanHoaTen.BoDau(ten);

        return (
            Ten:      ten,
            Ngay:     cu?.NgaySinh is null                    ? quet?.DoiNgay()       : null,
            GioiTinh: string.IsNullOrWhiteSpace(cu?.GioiTinh) ? quet?.DoiGioiTinh()   : null,
            DiaChi:   string.IsNullOrWhiteSpace(cu?.DiaChi)   ? Rong(quet?.DiaChi)    : null,
            Slug:     slug);
    }

    private static string? Rong(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Mã giả "không có căn cước" — cùng bộ với <c>HoSoBenhNhanService</c>.</summary>
    private static readonly string[] MaGiaKhongCanCuoc = { "11111111111", "111111111111" };

    public static bool LaMaGia(string? cccd) =>
        !string.IsNullOrWhiteSpace(cccd) && MaGiaKhongCanCuoc.Contains(cccd.Trim());


    private static string? LayClaim(ClaimsPrincipal nguoiDung, string ten)
        => nguoiDung.FindFirst(ten)?.Value;

    private static string LayDienThoai(string dinhDanh, TaiKhoan taiKhoan)
        => dinhDanh.Contains('@') ? taiKhoan.SDT : dinhDanh;

    private static string? LayEmail(string dinhDanh, TaiKhoan taiKhoan)
        => dinhDanh.Contains('@') ? dinhDanh : taiKhoan.Email;

}
