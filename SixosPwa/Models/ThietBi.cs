namespace SixosPwa.Models;

public class ThietBi
{
    public long Id { get; set; }
    public string? SDT { get; set; }
    public string MaBN { get; set; } = "";
    public string? IdThietBi { get; set; }
    public bool TrangThai { get; set; } = true;
}