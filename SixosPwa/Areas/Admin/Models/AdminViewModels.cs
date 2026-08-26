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
    public IReadOnlyList<DoiTac> RecentPartners { get; init; } = Array.Empty<DoiTac>();
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

    [StringLength(255, ErrorMessage = "Mật khẩu đối tác tối đa 255 ký tự.")]
    public string? MatKhauDoiTac { get; set; }
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

    [StringLength(100)]
    public string? SoToaNha { get; set; }

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


    public bool Active { get; set; }

    [StringLength(20)]
    public string? SDT { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? TenTM { get; set; }

    [StringLength(500)]
    public string? Img { get; set; }

    public IFormFile? ImageFile { get; set; }

    [StringLength(500)]
    public string? Logo { get; set; }

    public IFormFile? LogoFile { get; set; }

    [StringLength(2000)]
    public string? LogoUrlInput { get; set; }

    [Range(0, 999999999999999, ErrorMessage = "Số tiền quảng cáo không hợp lệ.")]
    public decimal? QuangCao { get; set; }

    public string? NoiDungQuangCao { get; set; }

    [StringLength(2000)]
    public string? QuangCaoImg { get; set; }

    public IFormFile? QuangCaoImageFile { get; set; }

    [StringLength(2000)]
    public string? QuangCaoImgUrlInput { get; set; }

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

public sealed class BenhNhanListViewModel
{
    /// <summary>Ma co so cua tung ho so benh nhan — ho so nay nam o DM_BenhNhanCoSo.</summary>
    public IReadOnlyDictionary<long, string> MaCoSoTheoBenhNhan { get; init; } = new Dictionary<long, string>();

    /// <summary>Ma ho so (MaBN) cua tung benh nhan — cung nam o DM_BenhNhanCoSo.</summary>
    public IReadOnlyDictionary<long, string> MaBNTheoBenhNhan { get; init; } = new Dictionary<long, string>();
    public IReadOnlyList<BenhNhan> Items { get; init; } = Array.Empty<BenhNhan>();
    public string? Query { get; init; }
    /// <summary>
    /// Bo loc theo MA CO SO. Truoc day ten la MaDT nhung than ham van loc theo
    /// DM_CSKCB.MaCoSo — ten cu NOI DOI ve nghia. Doi ten o Dot 3.
    /// </summary>
    public string? MaCoSo { get; init; }
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
    public string? MaCoSo { get; init; }
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
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public string CCCD { get; set; } = "";
}

