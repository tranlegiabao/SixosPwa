namespace SixosPwa.Models;

/// <summary>
/// Mot HO SO = mot CON NGUOI TAI MOT CO SO (dot 1B).
/// Truoc 1B khai niem nay tach lam hai bang; nay gop lai, va mot nguoi kham o
/// N co so thi co N dong — do la "chap nhan lap dong" cua C12.
/// Xem CONTEXT.md muc *Dong neo* / *Thang cap* / *Nhan ban*.
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
    /// Co so ma ho so nay thuoc ve. <b>NULL = DONG NEO</b> — trang thai qua do
    /// giua *cua 1* va *cua 4* cua luong HIS day sang (PA-C1).
    ///
    /// <para>
    /// 🔴 MOI cau doc phuc vu giao dien PHAI loc <c>IdCoSo != null</c>. Dong neo
    /// khong duoc hien o man nao va khong dang nhap duoc. Xem ADR 0034.
    /// </para>
    /// </summary>
    public long? IdCoSo { get; set; }

    /// <summary>
    /// Ma do CO SO cap, duy nhat trong pham vi co so (khong duy nhat toan he).
    /// RONG khi ho so con la *tu khai* — cong khong tu bia ma (chot 12 dot 1).
    /// </summary>
    public string? MaBN { get; set; }

    /// <summary>
    /// *Cua tai lieu* (chot 9 dot 1, ADR 0020): ho so nay da duoc phep mo ket
    /// qua can lam sang / don thuoc chua. Doc no la "co so da ghi ban la dau moi
    /// lien lac cua nguoi nay", KHONG phai "ban chinh la nguoi nay" — CCCD go
    /// luc dang nhap KHONG duoc xac thuc, OTP chi xac thuc so dien thoai.
    /// </summary>
    public bool DaMoTaiLieu { get; set; }

    /// <summary>
    /// *Moc xem lich* (ADR 0025) — lan gan nhat nguoi dung mo o *Lich kham cua toi*.
    /// MOT COT, khong phai bang "da doc tung muc": lich hen la TRANG THAI xem di
    /// xem lai. NULL = chua mo lan nao => moi muc deu la moi.
    /// </summary>
    public DateTime? NgayXemLichCuoi { get; set; }

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

