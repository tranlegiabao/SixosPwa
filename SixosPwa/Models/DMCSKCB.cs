namespace SixosPwa.Models;

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
    public string? SoToaNha { get; set; }
    public int? Tinh { get; set; }
    public int? PhuongXa { get; set; }
    public string? SDT { get; set; }
    public string? Email { get; set; }
    public string? TenTM { get; set; }
    public string? Img { get; set; }
    public string? Logo { get; set; }
    public bool XacMinh { get; set; }
    public bool Active { get; set; } = true;

    /// <summary>So nguyen VND — decimal(15,0) la CO Y, khong phai sai kieu tien te.</summary>
    public decimal? QuangCao { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
    public DateTime? NgayCapNhat { get; set; }
}
