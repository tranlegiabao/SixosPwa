namespace SixosPwa.Models;

public class TaiKhoan
{
    public long Id { get; set; }
    public string SDT { get; set; } = "";
    public string Role { get; set; } = "";

    /// <summary>Cot nay DA CO SAN trong DB, truoc day model co y comment di.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Khoa noi benh nhan sang he doi tac (V6). Co the rong voi tai khoan cu
    /// tao truoc khi co cong benh nhan.
    /// </summary>
    public string? CCCD { get; set; }

    public string? MatKhau { get; set; }
}
