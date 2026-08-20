using System.ComponentModel.DataAnnotations;
using SixosPwa.Models;

namespace SixosPwa.Areas.Admin.Models;

public sealed class DashboardViewModel
{
    public int AccountCount { get; init; }
    public int AdminAccountCount { get; init; }
    public int PatientAccountCount { get; init; }
    public int PartnerCount { get; init; }
    public int PatientCount { get; init; }
    public int FacilityCount { get; init; }
    public int VerifiedFacilityCount { get; init; }
    public int NotificationCount { get; init; }
    public int PushSubscriptionCount { get; init; }
    public int UnreadNotificationCount { get; init; }
    public IReadOnlyList<ThongBao> RecentNotifications { get; init; } = Array.Empty<ThongBao>();
    public IReadOnlyList<TaiKhoan> RecentAccounts { get; init; } = Array.Empty<TaiKhoan>();
    public IReadOnlyList<DoiTac> RecentPartners { get; init; } = Array.Empty<DoiTac>();
    public IReadOnlyList<DMCSKCB> RecentFacilities { get; init; } = Array.Empty<DMCSKCB>();
}

public sealed class TaiKhoanListViewModel
{
    public IReadOnlyList<TaiKhoan> Items { get; init; } = Array.Empty<TaiKhoan>();
    public string? Query { get; init; }
    public string? Role { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}

public sealed class TaiKhoanEditViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [StringLength(20, ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string SDT { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    public string Role { get; set; } = "BenhNhan";
}

public sealed class DoiTacListViewModel
{
    public IReadOnlyList<DoiTac> Items { get; init; } = Array.Empty<DoiTac>();
    public string? Query { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}

public sealed class DoiTacEditViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã đối tác.")]
    [StringLength(20, ErrorMessage = "Mã đối tác tối đa 20 ký tự.")]
    public string MaDT { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đối tác.")]
    [StringLength(100, ErrorMessage = "Tên đối tác tối đa 100 ký tự.")]
    public string TenDT { get; set; } = string.Empty;

    [StringLength(255)]
    public string? DiaChi { get; set; }

    [StringLength(20)]
    public string? SDT { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? BrandName { get; set; }
}

public sealed class CoSoYTeListViewModel
{
    public IReadOnlyList<DMCSKCB> Items { get; init; } = Array.Empty<DMCSKCB>();
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

    [Required(ErrorMessage = "Vui lòng nhập tên cơ sở.")]
    [StringLength(100, ErrorMessage = "Tên cơ sở tối đa 100 ký tự.")]
    public string? TenCoSo { get; set; }

    [StringLength(255)]
    public string? DiaChi { get; set; }

    [StringLength(20)]
    public string? LoaiCS { get; set; }

    [StringLength(50)]
    public string? TGLamViec { get; set; }

    public bool XacMinh { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "URL hình ảnh không hợp lệ.")]
    public string? Img { get; set; }

    [Range(0, 999999999999999, ErrorMessage = "Giá trị quảng cáo không hợp lệ.")]
    public decimal? QuangCao { get; set; }
}

public sealed class BenhNhanListViewModel
{
    public IReadOnlyList<BenhNhan> Items { get; init; } = Array.Empty<BenhNhan>();
    public string? Query { get; init; }
    public string? MaDT { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}

public sealed class PaginationViewModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public string? Query { get; init; }
    public string? Role { get; init; }
    public string? LoaiCS { get; init; }
    public string? MaDT { get; init; }
}
