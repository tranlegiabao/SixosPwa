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
    public IReadOnlyList<DMNhomCS> NhomCSList { get; init; } = Array.Empty<DMNhomCS>();
    public IReadOnlyList<DMChuDe> ChuDeList { get; init; } = Array.Empty<DMChuDe>();
    public IReadOnlyList<DMCSKCB> FacilityList { get; init; } = Array.Empty<DMCSKCB>();
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

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p sá»‘ Ä‘iá»‡n thoáº¡i.")]
    [StringLength(20, ErrorMessage = "Sá»‘ Ä‘iá»‡n thoáº¡i khÃ´ng há»£p lá»‡.")]
    public string SDT { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng chá»n vai trÃ².")]
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

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p mÃ£ Ä‘á»‘i tÃ¡c.")]
    [StringLength(20, ErrorMessage = "MÃ£ Ä‘á»‘i tÃ¡c tá»‘i Ä‘a 20 kÃ½ tá»±.")]
    public string MaDT { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p tÃªn Ä‘á»‘i tÃ¡c.")]
    [StringLength(100, ErrorMessage = "TÃªn Ä‘á»‘i tÃ¡c tá»‘i Ä‘a 100 kÃ½ tá»±.")]
    public string TenDT { get; set; } = string.Empty;

    [StringLength(255)]
    public string? DiaChi { get; set; }

    [StringLength(20)]
    public string? SDT { get; set; }

    [EmailAddress(ErrorMessage = "Email khÃ´ng há»£p lá»‡.")]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? BrandName { get; set; }

    [StringLength(255, ErrorMessage = "Máº­t kháº©u Ä‘á»‘i tÃ¡c tá»‘i Ä‘a 255 kÃ½ tá»±.")]
    public string? Password { get; set; }
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

    [StringLength(10, ErrorMessage = "MÃ£ cÆ¡ sá»Ÿ tá»‘i Ä‘a 10 kÃ½ tá»±.")]
    public string? MaCoSo { get; set; }

    [Required(ErrorMessage = "Vui lÃ²ng nháº­p tÃªn cÆ¡ sá»Ÿ.")]
    [StringLength(100, ErrorMessage = "TÃªn cÆ¡ sá»Ÿ tá»‘i Ä‘a 100 kÃ½ tá»±.")]
    public string? TenCoSo { get; set; }

    [StringLength(255)]
    public string? DiaChi { get; set; }

    [StringLength(100)]
    public string? SoToaNha { get; set; }

    public int? Tinh { get; set; }

    public int? Huyen { get; set; }

    public int? PhuongXa { get; set; }

    [StringLength(20)]
    public string? LoaiCS { get; set; }

    [StringLength(50)]
    public string? TGLamViec { get; set; }

    [StringLength(50)]
    public string? NgayLamViec { get; set; }

    public string? GioMoCua { get; set; }

    public string? GioDongCua { get; set; }

    public bool XacMinh { get; set; }

    [StringLength(500)]
    public string? Img { get; set; }

    public IFormFile? ImageFile { get; set; }

    [StringLength(500)]
    public string? Logo { get; set; }

    public IFormFile? LogoFile { get; set; }

    [Range(0, 999999999999999, ErrorMessage = "Sá»‘ tiá»n quáº£ng cÃ¡o khÃ´ng há»£p lá»‡.")]
    public decimal? QuangCao { get; set; }

    public string? NoiDungQuangCao { get; set; }

    [StringLength(2000)]
    public string? QuangCaoImg { get; set; }

    public IFormFile? QuangCaoImageFile { get; set; }

    public string? NoiDungGioiThieu { get; set; }
    public string? NoiDungDichVu { get; set; }
    public string? NoiDungDoiNgu { get; set; }
    public string? NoiDungTrangThietBi { get; set; }
    public string? NoiDungLienHe { get; set; }
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


