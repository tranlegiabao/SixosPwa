namespace SixosPwa.Models;

/// <summary>
/// Mot CON NGUOI, khoa dinh danh la CCCD.
/// Ho so tai tung co so nam o <see cref="BenhNhanCoSo"/> — truoc dot tai kien truc
/// hai khai niem nay bi tron chung trong mot bang DMBenhNhan.
/// </summary>
public class BenhNhan
{
    public long Id { get; set; }
    public string CCCD { get; set; } = "";
    public string TenBN { get; set; } = "";
    public string? SDT { get; set; }
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
}
