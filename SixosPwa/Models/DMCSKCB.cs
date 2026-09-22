namespace SixosPwa.Models;

/// <summary>
/// Cơ sở khám chữa bệnh. Sau đợt A, bảng <c>DM_CSKCB</c> đã nuốt luôn
/// <c>DM_DoiTacApi</c>, <c>DM_CSKCB_QuangCao</c>, <c>HT_KhoaApiCoSo</c> và
/// <c>HT_KhoFtpCoSo</c> — nên cột nhóm theo tiền tố <c>Qc_</c>, <c>KetNoi_</c>,
/// <c>Khoa_</c>, <c>Ftp_</c>.
///
/// <para>
/// 🔴 BỐN CỘT BÍ MẬT CỐ Ý KHÔNG CÓ Ở ĐÂY — <c>KhoaBam</c>, <c>Ftp_TaiKhoan</c>,
/// <c>Ftp_MatKhau</c>, <c>KetNoi_KhoaGoiHIS</c>. Không phải quên, và đừng "bổ sung
/// cho đủ": có 59 chỗ đọc <c>DM_CSKCB</c> qua EF, trong đó
/// <c>HomeController.cs:224</c> nạp <c>ToListAsync()</c> TRỌN thực thể của MỌI cơ sở
/// ra trang công khai. Khai một property ở đây là EF tự kéo cột đó theo, và
/// <c>Ftp_MatKhau</c> đang lưu THÔ — một lần render nhầm là lộ mật khẩu FTP của
/// toàn bộ phòng khám.
/// </para>
/// <para>
/// Chỉ ba chỗ được đọc bốn cột đó, và phải đọc bằng SQL riêng (Dapper/ADO), KHÔNG
/// qua EF: <c>Services/KhoCoSoService.cs</c> · <c>Security/KhoaCoSoAttribute.cs</c> ·
/// <c>Services/His/HisDocService.cs</c>. Đường GHI duy nhất là stored
/// <c>DM_CSKCB_Save</c> (truyền NULL ⇒ giữ nguyên giá trị cũ).
/// </para>
/// </summary>
public class DMCSKCB
{
    public long Id { get; set; }
    public string MaCoSo { get; set; } = "";
    public string TenCoSo { get; set; } = "";
    public string? TenVietTat { get; set; }

    /// <summary>Đoạn chữ quản trị viên đặt tay, làm nền URL cố định /DangKyOnline/{Slug}.
    /// KHÔNG tự sinh từ tên, nên đổi tên cơ sở không gãy URL.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Khóa ngoại sang <see cref="DMNhomCS"/> — trước đây là chuỗi LoaiCS.</summary>
    public long? IdNhomCS { get; set; }

    public string? DiaChi { get; set; }
    public int? Tinh { get; set; }
    public int? PhuongXa { get; set; }
    public string? SDT { get; set; }
    public string? Email { get; set; }

    /// <summary>Ảnh bìa trang cơ sở — tên cũ là <c>Img</c>.</summary>
    public string? AnhBia { get; set; }

    public string? Logo { get; set; }

    /// <summary>
    /// Cơ sở có hiện trên trang công khai / có nhận đăng ký mới không — tên cũ là
    /// <c>Active</c>. ADR 0013. Đừng nhầm với <see cref="KetNoi_Active"/>.
    /// </summary>
    public bool HienThiCongKhai { get; set; }

    /// <summary>
    /// SỐ TIỀN QUẢNG CÁO ĐÃ TRẢ (tên cũ là <c>QuangCao</c>) — khóa xếp hạng ở
    /// <c>DM_CSKCB_TopQuangCao</c>. Số nguyên VND, decimal(15,0) là CỐ Ý.
    /// </summary>
    public decimal? QcSoTienDaTra { get; set; }

    /// <summary>Nội dung quảng cáo — từ <c>DM_CSKCB_QuangCao.NoiDung</c>.</summary>
    public string? QcNoiDung { get; set; }

    /// <summary>Ảnh quảng cáo — từ <c>DM_CSKCB_QuangCao.Img</c>.</summary>
    public string? QcAnh { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DateTime? NgayCapNhat { get; set; }

    /// <summary>
    /// Tên công ty chủ quản — cột phẳng <c>DM_CSKCB.TenCongTy</c>.
    /// 🔴 Đợt 1B (C16/PA-1) bỏ bảng <c>DM_DoiTac</c> và khóa ngoại <c>IDCongTy</c>:
    /// chỉ 1/12 cơ sở từng có giá trị, không đáng một bảng riêng. Đảo ADR 0011.
    /// </summary>
    public string? TenCongTy { get; set; }

    // --- Kết nối sang hệ HIS của cơ sở (từ DM_DoiTacApi) --------------------

    /// <summary>
    /// Trang riêng của cơ sở. CÓ giá trị = cơ sở có "cửa riêng", bệnh nhân được
    /// chuyển hướng sang đây thay vì ở lại trang nội bộ. Đây là CHỖ DUY NHẤT
    /// quyết định điều đó (cột <c>KieuApi</c> đã bị bỏ) — xem
    /// <c>Services/Partner/CuaCoSoService.cs</c>.
    /// </summary>
    public string? KetNoi_UrlChuyenHuong { get; set; }

    /// <summary>Gốc địa chỉ API của HIS bên cơ sở — từ <c>DM_DoiTacApi.BaseUrl</c>.</summary>
    public string? KetNoi_BaseUrlHIS { get; set; }

    /// <summary>Công tắc đường kết nối HIS. Đừng nhầm với <see cref="HienThiCongKhai"/>.</summary>
    public bool KetNoi_Active { get; set; }

    // --- Khóa API của cơ sở (từ HT_KhoaApiCoSo) -----------------------------
    //  🔴 Chính cái khóa (KhoaBam) KHÔNG có ở đây — xem chú thích đầu lớp.

    public DateTime? Khoa_NgayCap { get; set; }
    public DateTime? Khoa_NgayHetHan { get; set; }

    // --- Kho FTP của cơ sở (từ HT_KhoFtpCoSo, ADR 0030) ---------------------
    //  🔴 Ftp_TaiKhoan / Ftp_MatKhau KHÔNG có ở đây — xem chú thích đầu lớp.

    public string? Ftp_Host { get; set; }
    public string? Ftp_ThuMucGoc { get; set; }
    public bool Ftp_Active { get; set; }

    /// <summary>Mốc "Thử kết nối đạt" gần nhất. Đổi thông số kho thì stored tự xóa mốc này.</summary>
    public DateTime? Ftp_NgayThuDat { get; set; }
}
