using System.ComponentModel.DataAnnotations;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Models;

public sealed class DashboardViewModel
{
            public int AccountCount { get; init; }
    public IReadOnlyList<FacilityGroupStat> FacilityGroupStats { get; init; } = Array.Empty<FacilityGroupStat>();
    public int AdminAccountCount { get; init; }
    public int PatientAccountCount { get; init; }
    public int PartnerCount { get; init; }
    public int PatientCount { get; init; }
    public int FacilityCount { get; init; }
    public int VisibleFacilityCount { get; init; }
    public int NotificationCount { get; init; }
    public int PushSubscriptionCount { get; init; }
    public int UnreadNotificationCount { get; init; }
    public IReadOnlyList<ThongBao> RecentNotifications { get; init; } = Array.Empty<ThongBao>();
    public IReadOnlyList<TaiKhoan> RecentAccounts { get; init; } = Array.Empty<TaiKhoan>();
    public IReadOnlyList<DMCSKCB> RecentFacilities { get; init; } = Array.Empty<DMCSKCB>();
    public IReadOnlyList<DMNhomCS> NhomCSList { get; init; } = Array.Empty<DMNhomCS>();
    public IReadOnlyList<DMChuDe> ChuDeList { get; init; } = Array.Empty<DMChuDe>();
    public IReadOnlyList<DMCSKCB> FacilityList { get; init; } = Array.Empty<DMCSKCB>();
    public long? SelectedNhomCSId { get; init; }
    public long? SelectedFacilityId { get; init; }
    public long? SelectedTopicId { get; init; }
    public string? NoiDung { get; init; }
}

public sealed class DashboardContentEditViewModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn cơ sở y tế.")]
    public long FacilityId { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn chủ đề.")]
    public long TopicId { get; set; }

    public string? NoiDung { get; set; }
}

public sealed class HoSoBenhNhanItemViewModel
{
    public long Id { get; set; }
    public long? IdTaiKhoan { get; set; }
    public string TenBN { get; set; } = "";
    public string CCCD { get; set; } = "";
    public string? SDT { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? MaBN { get; set; }
    public string? TenCoSo { get; set; }

    /// <summary>ID dong <c>DM_BenhNhanCoSo</c> — thu ma <c>DM_BenhNhanCoSo_GoNoi</c> nhan vao.</summary>
    public long? IdHoSoCoSo { get; set; }

    /// <summary>Co so cua chinh dong tren. Khong duoc suy ra tu danh sach co so.</summary>
    public long? IdCoSo { get; set; }

    public int SoCoSo { get; set; }
}

public sealed class CapNhatHoSoAdminRequest
{
    public long Id { get; set; }
    public long? IdTaiKhoan { get; set; }
    public string TenBN { get; set; } = "";
    public string CCCD { get; set; } = "";
    public string? SDT { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? DiaChi { get; set; }
    public string? MaBN { get; set; }

    /// <summary>
    /// Co so de gan <see cref="MaBN"/> vao. 🔴 BAT BUOC khi ho so chua co dong
    /// <c>DM_BenhNhanCoSo</c> nao: khong co so nay thi may chu KHONG duoc doan, vi
    /// doan la noi ma vao nham co so ma khong ai thay.
    /// </summary>
    public long? IdCoSo { get; set; }
}

/// <summary>
/// *Go noi* mot ma khoi mot ho so, do ADMIN bam (ADR 0024 ve 3 — cua benh nhan da dong).
/// </summary>
public sealed class GoNoiHoSoAdminRequest
{
    /// <summary>ID dong <c>DM_BenhNhanCoSo</c> can thao.</summary>
    public long IdHoSoCoSo { get; set; }
}

public sealed class XoaHoSoAdminRequest
{
    public long Id { get; set; }
    public long? IdTaiKhoan { get; set; }
}

public sealed class TaoHoSoAdminRequest
{
    public long IdTaiKhoan { get; set; }
    public string TenBN { get; set; } = "";
    public string CCCD { get; set; } = "";
    public string? SDT { get; set; }
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? DiaChi { get; set; }
    public long? IdCoSo { get; set; }
}

public sealed class TaiKhoanListViewModel
{
    public IReadOnlyList<TaiKhoan> Items { get; init; } = Array.Empty<TaiKhoan>();
    public IReadOnlyDictionary<long, List<HoSoBenhNhanItemViewModel>> HoSoTheoTaiKhoan { get; init; } = new Dictionary<long, List<HoSoBenhNhanItemViewModel>>();
    public IReadOnlyList<DMCSKCB> DanhSachCoSo { get; init; } = Array.Empty<DMCSKCB>();
    public IReadOnlyList<DMGioiTinh> DanhMucGioiTinh { get; init; } = Array.Empty<DMGioiTinh>();
    public string? Query { get; init; }
    public string? Role { get; init; }
    public string? LoaiCS { get; init; }
    public string? CCCD { get; init; }
    public string? SDT { get; init; }
    public string? MaBN { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; } = 50;
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)(PageSize > 0 ? PageSize : 50)));
    public bool DaLoc { get; init; }
}

public sealed class TaiKhoanEditViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string SDT { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = "Admin";
}

public sealed class CoSoYTeListViewModel
{
    public IReadOnlyList<DMCSKCB> Items { get; init; } = Array.Empty<DMCSKCB>();

    /// <summary>Ma nhom co so tra cuu theo IDNhomCS — nhom nay la khoa ngoai, khong con la chuoi tren bang co so.</summary>
    public IReadOnlyDictionary<long, string> MaNhomTheoId { get; init; } = new Dictionary<long, string>();
    public string? Query { get; init; }
    public string? LoaiCS { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}

public sealed class CoSoYTeEditViewModel
{
    public long Id { get; set; }

    [StringLength(10, ErrorMessage = "Mã cơ sở tối đa 10 ký tự.")]
    public string? MaCoSo { get; set; }

    [StringLength(100, ErrorMessage = "Đường dẫn tối đa 100 ký tự.")]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$",
        ErrorMessage = "Đường dẫn chỉ gồm chữ thường không dấu, số và dấu gạch ngang.")]
    public string? Slug { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên cơ sở.")]
    [StringLength(100, ErrorMessage = "Tên cơ sở tối đa 100 ký tự.")]
    public string? TenCoSo { get; set; }

    [StringLength(255)]
    public string? DiaChi { get; set; }

    public int? Tinh { get; set; }

    public int? PhuongXa { get; set; }

    [StringLength(20)]
    public string? LoaiCS { get; set; }

    [StringLength(50)]
    public string? TGLamViec { get; set; }

    [StringLength(50)]
    public string? NgayLamViec { get; set; }

    public string? GioMoCua { get; set; }

    public string? GioDongCua { get; set; }


    /// <summary>Cột <c>DM_CSKCB.HienThiCongKhai</c> — tên cũ là <c>Active</c>.</summary>
    public bool HienThiCongKhai { get; set; }

    /// <summary>Công ty (đối tác) sở hữu cơ sở — cột <c>DM_CSKCB.TenCongTy</c>, tên cũ <c>IDDoiTac</c>.</summary>
    public string? TenCongTy { get; set; }

    // ---- Kết nối HIS: gộp từ bảng 1:1 DM_DoiTacApi vào thẳng DM_CSKCB ----------

    [StringLength(255, ErrorMessage = "Đường dẫn chuyển hướng tối đa 255 ký tự.")]
    public string? KetNoi_UrlChuyenHuong { get; set; }

    [StringLength(255, ErrorMessage = "Base URL của HIS tối đa 255 ký tự.")]
    public string? KetNoi_BaseUrlHIS { get; set; }

    /// <summary>
    /// 🔴 Ô BÍ MẬT. Màn Sửa KHÔNG đổ giá trị cũ ra đây (để trống + placeholder);
    /// gửi NULL lên <c>DM_CSKCB_Save</c> nghĩa là GIỮ NGUYÊN khóa cũ.
    /// </summary>
    [StringLength(500, ErrorMessage = "Khóa gọi HIS tối đa 500 ký tự.")]
    public string? KetNoi_KhoaGoiHIS { get; set; }

    public bool KetNoi_Active { get; set; } = true;

    // ---- Kho phiếu cơ sở: FTP của phòng khám, cổng CHỈ ĐỌC (ADR 0030) ----------
    // Mỗi cơ sở đúng một kho ⇒ nằm thẳng cột trên DM_CSKCB, không còn bảng con.

    [StringLength(200, ErrorMessage = "Máy chủ kho tối đa 200 ký tự.")]
    public string? Ftp_Host { get; set; }

    /// <summary>🔴 Ô BÍ MẬT — không đổ giá trị cũ ra màn hình, để trống là GIỮ NGUYÊN.</summary>
    [StringLength(100, ErrorMessage = "Tài khoản kho tối đa 100 ký tự.")]
    public string? Ftp_TaiKhoan { get; set; }

    /// <summary>
    /// 🔴 Ô BÍ MẬT, lưu THÔ theo chốt 36 (tiền lệ ADR 0005) — FTP cần đăng nhập.
    /// Để trống là GIỮ NGUYÊN mật khẩu cũ (stored nhận NULL thì không ghi đè).
    /// </summary>
    [StringLength(200, ErrorMessage = "Mật khẩu kho tối đa 200 ký tự.")]
    public string? Ftp_MatKhau { get; set; }

    [StringLength(200, ErrorMessage = "Thư mục gốc tối đa 200 ký tự.")]
    public string? Ftp_ThuMucGoc { get; set; }

    public bool Ftp_Active { get; set; }

    /// <summary>Chỉ để hiện trạng thái — chưa có mốc thì ô bật bị khóa (chốt 41).</summary>
    public DateTime? Ftp_NgayThuDat { get; set; }

    [StringLength(20)]
    public string? SDT { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(100)]
    public string? Email { get; set; }

    /// <summary>Cột <c>DM_CSKCB.AnhBia</c> — tên cũ là <c>Img</c>.</summary>
    [StringLength(500)]
    public string? AnhBia { get; set; }

    public IFormFile? ImageFile { get; set; }

    [StringLength(500)]
    public string? Logo { get; set; }

    public IFormFile? LogoFile { get; set; }

    public bool LogoRemoved { get; set; }

    [StringLength(2000)]
    public string? LogoUrlInput { get; set; }

    /// <summary>Cột <c>DM_CSKCB.QcSoTienDaTra</c> — tên cũ là <c>QuangCao</c>. Khóa xếp hạng quảng cáo.</summary>
    [Range(0, 999999999999999, ErrorMessage = "Số tiền quảng cáo không hợp lệ.")]
    public decimal? QcSoTienDaTra { get; set; }

    /// <summary>Gộp từ <c>DM_CSKCB_QuangCao.NoiDung</c> vào thẳng <c>DM_CSKCB.QcNoiDung</c>.</summary>
    public string? QcNoiDung { get; set; }

    /// <summary>Gộp từ <c>DM_CSKCB_QuangCao.Img</c> vào thẳng <c>DM_CSKCB.QcAnh</c>.</summary>
    [StringLength(2000)]
    public string? QcAnh { get; set; }

    public IFormFile? QuangCaoImageFile { get; set; }

    /// <summary>O dan duong dan anh quang cao — ghi vao cot <c>DM_CSKCB.QcAnh</c>.</summary>
    [StringLength(2000)]
    public string? QcAnhUrlInput { get; set; }

    public IReadOnlyList<DMNhomCS> NhomCSList { get; set; } = Array.Empty<DMNhomCS>();
    public IReadOnlyList<DMChuDe> ChuDeList { get; set; } = Array.Empty<DMChuDe>();
    public IReadOnlyList<DMCSKCB> FacilityList { get; set; } = Array.Empty<DMCSKCB>();
    public long? SelectedNhomCSId { get; set; }
    public long? SelectedFacilityId { get; set; }
    public long? SelectedTopicId { get; set; }
    public long TopicId { get; set; }
    public string? ActiveSection { get; set; }
    public string? TopicContentsJson { get; set; }
    public string? NoiDung { get; set; }

    public string? NoiDungGioiThieu { get; set; }
    public string? NoiDungDichVu { get; set; }
    public string? NoiDungDoiNgu { get; set; }
    public string? NoiDungTrangThietBi { get; set; }
    public string? NoiDungLienHe { get; set; }
}

public sealed class BenhNhanNhomItemViewModel
{
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public int SoLuongHoSo => DanhSachHoSo?.Count ?? 0;
    public List<HoSoBenhNhanItemViewModel> DanhSachHoSo { get; set; } = new();
}

public sealed class BenhNhanListViewModel
{
    public IReadOnlyList<BenhNhanNhomItemViewModel> Items { get; init; } = Array.Empty<BenhNhanNhomItemViewModel>();
    public IReadOnlyList<DMCSKCB> DanhSachCoSo { get; init; } = Array.Empty<DMCSKCB>();
    public IReadOnlyList<DMGioiTinh> DanhMucGioiTinh { get; init; } = Array.Empty<DMGioiTinh>();
    public string? Query { get; init; }
    public string? LoaiCS { get; init; }
    public string? CCCD { get; init; }
    public string? SDT { get; init; }
    public string? MaBN { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)(PageSize > 0 ? PageSize : 50)));
    public bool DaLoc { get; init; }
}

public sealed class PaginationViewModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public string? Query { get; init; }
    public string? Role { get; init; }
    public string? LoaiCS { get; init; }
    public string? MaCoSo { get; init; }
    public string? CCCD { get; init; }
    public string? SDT { get; init; }
    public string? MaBN { get; init; }
}

public sealed class FacilityGroupStat
{
    public string TenLoaiCS { get; set; } = "";
    public string LoaiCS { get; set; } = "";
    public int FacilityCount => Facilities.Count;
    public int TotalPatientCount => Facilities.Sum(x => x.PatientCount);
    public List<FacilityStat> Facilities { get; set; } = new();
}

public sealed class FacilityStat
{
    public long Id { get; set; }
    public List<PatientAccountStat> PatientAccounts { get; set; } = new();
    public string MaCoSo { get; set; } = "";
    public string TenCoSo { get; set; } = "";
    public int PatientCount { get; set; }
}




public sealed class PatientAccountStat
{
    /// <summary>
    /// ID tài khoản đăng nhập, <b>null khi hồ sơ chưa gắn tài khoản nào</b> (hồ sơ do
    /// HIS đẩy sang / cơ sở tự khai). Trước đây kiểu <c>long</c> nên các hồ sơ này in ra
    /// <c>#0</c> — cơ sở nào toàn hồ sơ từ HIS thì cả bảng là một cột <c>#0</c> vô nghĩa.
    /// </summary>
    public long? Id { get; set; }
    public string SDT { get; set; } = "";
    public string CCCD { get; set; } = "";
}

public sealed class CauHinhListViewModel
{
    public IReadOnlyList<HTConfig> Items { get; init; } = Array.Empty<HTConfig>();
    public IReadOnlyList<string> DanhSachNhom { get; init; } = Array.Empty<string>();
    public string? Query { get; init; }
    public string? Nhom { get; init; }
}


