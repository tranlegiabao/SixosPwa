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

    public string? DienThoai { get; set; }

    public bool LaNguonHis => string.Equals(Nguon, "his", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Hỗ trợ ddMMyyyy, yyyy-MM-dd HH:mm:ss.fff, yyyy-MM-dd, dd/MM/yyyy...
    /// </summary>
    public DateTime? DoiNgay()
    {
        var s = (NgaySinh ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return null;

        if (s.Length == 8 && s.All(char.IsDigit))
        {
            if (DateTime.TryParseExact(s, "ddMMyyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var ngay8))
                return ngay8.Date == new DateTime(1900, 1, 1) ? null : ngay8;
        }

        string[] formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "dd-MM-yyyy",
            "yyyy/MM/dd"
        };

        if (DateTime.TryParseExact(s, formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var ngayChung))
        {
            return ngayChung.Date == new DateTime(1900, 1, 1) ? null : ngayChung;
        }

        if (DateTime.TryParse(s, out var ngayTuDo))
        {
            return ngayTuDo.Date == new DateTime(1900, 1, 1) ? null : ngayTuDo;
        }

        return null;
    }

    /// <summary>
    /// Chu trong ma -> ma so cua cong ("1" Nam · "2" Nu · "3" chua xac dinh)
    /// </summary>
    public string? DoiGioiTinh()
    {
        var g = Services.ChuanHoaTen.BoDau(GioiTinh).Trim().ToUpperInvariant();
        if (g.Length == 0) return null;
        if (g == "NAM" || g == "1" || g == "MALE" || g == "M") return "1";
        if (g == "NU" || g == "2" || g == "FEMALE" || g == "F") return "2";
        return "3";
    }
}
