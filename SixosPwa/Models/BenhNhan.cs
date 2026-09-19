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

    /// <summary>
    /// Tai khoan dang quan ho so nay (ADR 0019). Quan he 1-N: mot tai khoan
    /// quan nhieu con nguoi — con dat kham cho me, me theo doi ket qua cho con.
    /// Truoc day chieu nguoc lai, <c>HT_TaiKhoan.IDBenhNhan</c>, la 1-1.
    /// Cot cu VAN CON vi khu Admin doc no; cot nay moi la nguon su that cua cong.
    /// </summary>
    public long? IdTaiKhoan { get; set; }

    /// <summary>Mot trong BON o cua luat gop ho so (ADR 0018, ban sua doi 2026-09-09).</summary>
    public DateTime? NgaySinh { get; set; }

    /// <summary>
    /// O thu TU cua luat gop, them 2026-09-09. Giu nguyen MA cua HIS
    /// (<c>DM_GioiTinh.MaGioiTinh</c>): "1" Nam, "2" Nu, "3" Chua xac dinh —
    /// khong dich sang bit/enum vi dich la them mot cho de lech.
    ///
    /// <para>
    /// Vi sao them: bo CCCD ra khoi phep khop thi con <b>348 nhom</b> trung ca ho
    /// ten, ngay sinh lan gioi tinh ma CCCD hop le KHAC NHAU — chac chan la hai con
    /// nguoi. Ba o khong du chat.
    /// </para>
    /// </summary>
    public string? GioiTinh { get; set; }

    /// <summary>
    /// Ten da chuan hoa bo dau — mot o cua luat gop. Ben HIS cot cung ten nay
    /// RONG 100% (74.725/74.725) nen day la ket qua chuan hoa cua CONG, khong
    /// phai ban chep tu HIS.
    /// </summary>
    public string? HoTenKhongDau { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;
}

/// <summary>
/// Giới tính. 🔴 Đợt A đã XOÁ bảng <c>DM_GioiTinh</c> — đây KHÔNG còn là thực thể EF
/// (không có DbSet, không có mapping). Ba giá trị <c>1=Nam · 2=Nữ · 3=Không xác định</c>
/// nay là HẰNG trong C# + <c>CHECK</c> trên <c>DM_BenhNhan.GioiTinh</c>.
/// Lớp này giữ lại chỉ để các màn đang dựng danh mục tại chỗ không phải viết lại kiểu.
/// </summary>
public class DMGioiTinh
{
    public string MaGioiTinh { get; set; } = "";
    public string TenGioiTinh { get; set; } = "";
}

