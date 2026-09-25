using System.Globalization;
using System.Text;

namespace SixosPwa.Services;

/// <summary>
/// Chuẩn hóa họ tên cho *luật gộp hồ sơ* (ADR 0018) — MỘT BẢN DUY NHẤT trong cả
/// ứng dụng (chốt 1 đợt 1).
///
/// <para>
/// 🔴 Đừng nhầm với <c>HomeController.RemoveAccentsAndSpaces</c>: cái đó bỏ LUÔN
/// khoảng trắng vì nó dùng để khớp slug URL. Ở đây khoảng trắng phải GIỮ (gom
/// về một dấu cách) — bỏ hết thì "Nguyen Van An" và "Nguyen Vanan" hóa thành
/// một, tức gộp nhầm hai con người.
/// </para>
/// <para>
/// Vì sao chuẩn hóa lúc chạy chứ không tin cột trong CSDL: bên HIS cột
/// <c>DM_BenhNhan.HoTenKhongDau</c> RỖNG 100% (74.725/74.725 trên Thiên Nam) —
/// code bên đó chỉ ĐỌC cột này để lọc chứ không bao giờ ghi. Cột cùng tên bên
/// cổng là kết quả chuẩn hóa của CỔNG, và vẫn phải chuẩn hóa lại khi so sánh vì
/// dữ liệu cũ có thể được ghi trước khi hàm này tồn tại.
/// </para>
/// </summary>
public static class ChuanHoaTen
{
    /// <summary>
    /// Bỏ dấu, về chữ HOA, gom khoảng trắng. Trả chuỗi rỗng khi đầu vào rỗng —
    /// và chuỗi rỗng KHÔNG được coi là khớp với bất kỳ tên nào.
    /// </summary>
    public static string BoDau(string? ten)
    {
        if (string.IsNullOrWhiteSpace(ten)) return string.Empty;

        // FormD tách dấu ra thành ký tự riêng để lọc bỏ.
        var tach = ten.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(tach.Length);

        foreach (var c in tach)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            // 'đ'/'Đ' có gạch ngang KHÔNG mang dấu phụ nên FormD không tách được,
            // phải đổi tay.
            if (c == 'đ' || c == 'Đ') { sb.Append('D'); continue; }

            if (char.IsWhiteSpace(c)) { sb.Append(' '); continue; }

            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
        }

        // Gom nhiều dấu cách liên tiếp về một: dữ liệu thật có cả "NGUYEN  VAN A".
        return string.Join(' ', sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Hai tên có cùng một người không, xét riêng ở TÊN của luật gộp.
    /// Tên rỗng thì trả <c>false</c> — thiếu dữ liệu không phải là bằng chứng khớp.
    /// </summary>
    public static bool CungTen(string? a, string? b)
    {
        var x = BoDau(a);
        var y = BoDau(b);
        return x.Length > 0 && x == y;
    }
}
