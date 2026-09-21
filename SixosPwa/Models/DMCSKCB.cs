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

    /// <summary>Doan chu quan tri vien dat tay, lam nen URL co dinh /DangKyOnline/{Slug}.
    /// KHONG tu sinh tu ten, nen doi ten co so khong gay URL.</summary>
    public string Slug { get; set; } = "";

    /// <summary>Khoa ngoai sang <see cref="DMNhomCS"/> — truoc day la chuoi LoaiCS.</summary>
    public long? IdNhomCS { get; set; }

    public string? DiaChi { get; set; }
    public int? Tinh { get; set; }
    public int? PhuongXa { get; set; }
    public string? SDT { get; set; }
    public string? Email { get; set; }

    /// <summary>Anh bia trang co so — ten cu la <c>Img</c>.</summary>
    public string? AnhBia { get; set; }

    public string? Logo { get; set; }

    /// <summary>
    /// Co so co hien tren trang cong khai / co nhan dang ky moi khong — ten cu la
    /// <c>Active</c>. ADR 0013. Dung nham voi <see cref="KetNoi_Active"/>.
    /// </summary>
    public bool HienThiCongKhai { get; set; }

    /// <summary>
    /// SO TIEN QUANG CAO DA TRA (ten cu la <c>QuangCao</c>) — khoa xep hang o
    /// <c>DM_CSKCB_TopQuangCao</c>. So nguyen VND, decimal(15,0) la CO Y.
    /// </summary>
    public decimal? QcSoTienDaTra { get; set; }

    /// <summary>Noi dung quang cao — tu <c>DM_CSKCB_QuangCao.NoiDung</c>.</summary>
    public string? QcNoiDung { get; set; }

    /// <summary>Anh quang cao — tu <c>DM_CSKCB_QuangCao.Img</c>.</summary>
    public string? QcAnh { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DateTime? NgayCapNhat { get; set; }

    /// <summary>
    /// Ten cong ty chu quan — cot phang <c>DM_CSKCB.TenCongTy</c>.
    /// 🔴 Dot 1B (C16/PA-1) bo bang <c>DM_DoiTac</c> va khoa ngoai <c>IDCongTy</c>:
    /// chi 1/12 co so tung co gia tri, khong dang mot bang rieng. Dao ADR 0011.
    /// </summary>
    public string? TenCongTy { get; set; }

    // --- Ket noi sang he HIS cua co so (tu DM_DoiTacApi) --------------------

    /// <summary>
    /// Trang rieng cua co so. CO gia tri = co so co "cua rieng", benh nhan duoc
    /// chuyen huong sang day thay vi o lai trang noi bo. Day la CHO DUY NHAT
    /// quyet dinh dieu do (cot <c>KieuApi</c> da bi bo) — xem
    /// <c>Services/Partner/CuaCoSoService.cs</c>.
    /// </summary>
    public string? KetNoi_UrlChuyenHuong { get; set; }

    /// <summary>Goc dia chi API cua HIS ben co so — tu <c>DM_DoiTacApi.BaseUrl</c>.</summary>
    public string? KetNoi_BaseUrlHIS { get; set; }

    /// <summary>Cong tat duong ket noi HIS. Dung nham voi <see cref="HienThiCongKhai"/>.</summary>
    public bool KetNoi_Active { get; set; }

    // --- Khoa API cua co so (tu HT_KhoaApiCoSo) -----------------------------
    //  🔴 Chinh cai khoa (KhoaBam) KHONG co o day — xem chu thich dau lop.

    public DateTime? Khoa_NgayCap { get; set; }
    public DateTime? Khoa_NgayHetHan { get; set; }

    // --- Kho FTP cua co so (tu HT_KhoFtpCoSo, ADR 0030) ---------------------
    //  🔴 Ftp_TaiKhoan / Ftp_MatKhau KHONG co o day — xem chu thich dau lop.

    public string? Ftp_Host { get; set; }
    public string? Ftp_ThuMucGoc { get; set; }
    public bool Ftp_Active { get; set; }

    /// <summary>Moc "Thu ket noi dat" gan nhat. Doi thong so kho thi stored tu xoa moc nay.</summary>
    public DateTime? Ftp_NgayThuDat { get; set; }
}
