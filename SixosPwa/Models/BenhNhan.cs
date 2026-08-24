namespace SixosPwa.Models;

public class BenhNhan
{
    public long Id { get; set; }
    public string MaBN { get; set; } = "";
    public string MaDT { get; set; } = "";
    public string? SDT { get; set; }
    public string TenBN { get; set; } = "";
    public string? DiaChi { get; set; }
    public string? Email { get; set; }
}