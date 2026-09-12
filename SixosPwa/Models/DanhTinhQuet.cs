namespace SixosPwa.Models;

/// <summary>
/// Danh tinh doc duoc tu mot ma QR o man Dang nhap. Hai nguon, cung mot khuon 7 manh
/// ngan bang '|', chi khac o thu hai:
/// <code>
/// phieu kham (HIS): cccd | MaBN    | tenBN | ddMMyyyy | Nam/Nu/Chua XD | diaChi | ddMMyyyy
/// CCCD gan chip   : cccd | CMND cu | hoTen | ddMMyyyy | Nam/Nu         | diaChi | ddMMyyyy
/// </code>
///
/// 🔴 <b>Day chi la DU LIEU do may khach gui len, khong phai bang chung.</b> Moi ket luan
/// "khop / duoc noi" phai tinh lai o may chu. Dac biet <see cref="MaBN"/>: no mo duong toi
/// benh an cua nguoi khac nen bat buoc phai doi chieu lai voi HIS truoc khi dung.
///
/// 🔴 <b>KHONG co so dien thoai, va co y khong bao gio co</b> (chot 11/09). Khuon CCCD gan
/// chip do Bo Cong an dinh nen khong them duoc; con QR cua HIS thi them duoc nhung KHONG
/// duoc them: so do in tren giay se khien cong doi chieu "4 so cuoi" mat sach tac dung —
/// nguoi nhat duoc to phieu chep so tu chinh to giay la qua cong. Chung nao OTP con la
/// ma co dinh (<c>DangNhapController</c> cho "123456" qua vo dieu kien) thi dieu do dong
/// nghia voi: cam to giay = doc duoc benh an. So dien thoai PHAI do nguoi dung tu go.
/// </summary>
public sealed class DanhTinhQuet
{
    /// <summary>"his" = ma tren phieu kham · "cccd" = CCCD gan chip.</summary>
    public string? Nguon { get; set; }

    public string? Cccd { get; set; }

    /// <summary>Chi co o nguon "his". Rong voi CCCD gan chip.</summary>
    public string? MaBN { get; set; }

    public string? HoTen { get; set; }

    /// <summary>Dang <c>ddMMyyyy</c> nhu trong ma. Doi sang ngay that bang <see cref="DoiNgay"/>.</summary>
    public string? NgaySinh { get; set; }

    /// <summary>Chu trong ma: "Nam" / "Nu" / "Chua XD". Doi sang ma so bang <see cref="DoiGioiTinh"/>.</summary>
    public string? GioiTinh { get; set; }

    public string? DiaChi { get; set; }

    public bool LaNguonHis => string.Equals(Nguon, "his", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>ddMMyyyy</c> -> ngay that. Tra <c>null</c> khi khong doc duoc, VA khi la
    /// <c>01/01/1900</c> — do la ngay sinh GIA cua HIS luc chi biet nam sinh (5.611/74.950
    /// ho so Thien Nam). Nhan no vao ho so la ghi mot ngay khong co that.
    /// </summary>
    public DateTime? DoiNgay()
    {
        var s = (NgaySinh ?? "").Trim();
        if (s.Length != 8 || !s.All(char.IsDigit)) return null;

        if (!DateTime.TryParseExact(s, "ddMMyyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var ngay))
            return null;

        return ngay.Date == new DateTime(1900, 1, 1) ? null : ngay;
    }

    /// <summary>
    /// Chu trong ma -> ma so cua cong ("1" Nam · "2" Nu · "3" chua xac dinh), dung dung
    /// bo gia tri cua man <c>HoSo/Them</c>. So sanh sau khi BO DAU de khong phu thuoc
    /// vao cach ma ghi "Nu" hay "Nữ".
    /// </summary>
    public string? DoiGioiTinh()
    {
        var g = Services.ChuanHoaTen.BoDau(GioiTinh).Trim().ToUpperInvariant();
        if (g.Length == 0) return null;
        if (g == "NAM") return "1";
        if (g == "NU") return "2";
        return "3";
    }
}
