namespace SixosPwa.Models;

/// <summary>
/// Danh tính đọc được từ một mã QR ở màn Đăng nhập. Hai nguồn, cùng một khuôn 7 mảnh
/// ngăn bằng '|', chỉ khác ở thứ hai:
/// <code>
/// phiếu khám (HIS): cccd | MaBN    | tenBN | ddMMyyyy | Nam/Nu/Chua XD | diaChi | ddMMyyyy
/// CCCD gắn chip   : cccd | CMND cũ | hoTen | ddMMyyyy | Nam/Nu         | diaChi | ddMMyyyy
/// </code>
///
/// 🔴 <b>Đây chỉ là DỮ LIỆU do máy khách gửi lên, không phải bằng chứng.</b> Mọi kết luận
/// "khớp / được nối" phải tính lại ở máy chủ. Đặc biệt <see cref="MaBN"/>: nó mở đường tới
/// bệnh án của người khác nên bắt buộc phải đối chiếu lại với HIS trước khi dùng.
///
/// 🔴 <b>KHÔNG có số điện thoại, và cố ý không bao giờ có</b> (chốt 11/09). Khuôn CCCD gắn
/// chip do Bộ Công an định nên không thêm được; còn QR của HIS thì thêm được nhưng KHÔNG
/// được thêm: số đó in trên giấy sẽ khiến cổng đối chiếu "4 số cuối" mất sạch tác dụng —
/// người nhặt được tờ phiếu chép số từ chính tờ giấy là qua cổng. Chừng nào OTP còn là
/// mã cố định (<c>DangNhapController</c> cho "123456" qua vô điều kiện) thì điều đó đồng
/// nghĩa với: cầm tờ giấy = đọc được bệnh án. Số điện thoại PHẢI do người dùng tự gõ.
/// </summary>
public sealed class DanhTinhQuet
{
    /// <summary>"his" = mã trên phiếu khám · "cccd" = CCCD gắn chip.</summary>
    public string? Nguon { get; set; }

    public string? Cccd { get; set; }

    /// <summary>Chỉ có ở nguồn "his". Rỗng với CCCD gắn chip.</summary>
    public string? MaBN { get; set; }

    public string? HoTen { get; set; }

    /// <summary>Dạng <c>ddMMyyyy</c> như trong mã. Đổi sang ngày thật bằng <see cref="DoiNgay"/>.</summary>
    public string? NgaySinh { get; set; }

    /// <summary>Chữ trong mã: "Nam" / "Nu" / "Chua XD". Đổi sang mã số bằng <see cref="DoiGioiTinh"/>.</summary>
    public string? GioiTinh { get; set; }

    public string? DiaChi { get; set; }

    public string? DienThoai { get; set; }

    public bool LaNguonHis => string.Equals(Nguon, "his", StringComparison.OrdinalIgnoreCase);

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
    /// Chữ trong mã -> mã số của cổng ("1" Nam · "2" Nữ · "3" chưa xác định)
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
