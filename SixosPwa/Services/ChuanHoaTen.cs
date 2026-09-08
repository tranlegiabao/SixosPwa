using System.Globalization;
using System.Text;

namespace SixosPwa.Services;

/// <summary>
/// Chuan hoa ho ten cho *luat gop ho so* (ADR 0018) — MOT BAN DUY NHAT trong ca
/// ung dung (chot 1 dot 1).
///
/// <para>
/// 🔴 Dung nham voi <c>HomeController.RemoveAccentsAndSpaces</c>: cai do bo LUON
/// khoang trang vi no dung de khop slug URL. O day khoang trang phai GIU (gom
/// ve mot dau cach) — bo het thi "Nguyen Van An" va "Nguyen Vanan" hoa thanh
/// mot, tuc gop nham hai con nguoi.
/// </para>
/// <para>
/// Vi sao chuan hoa luc chay chu khong tin cot trong CSDL: ben HIS cot
/// <c>DM_BenhNhan.HoTenKhongDau</c> RONG 100% (74.725/74.725 tren Thien Nam) —
/// code ben do chi DOC cot nay de loc chu khong bao gio ghi. Cot cung ten ben
/// cong la ket qua chuan hoa cua CONG, va van phai chuan hoa lai khi so sanh vi
/// du lieu cu co the duoc ghi truoc khi ham nay ton tai.
/// </para>
/// </summary>
public static class ChuanHoaTen
{
    /// <summary>
    /// Bo dau, ve chu HOA, gom khoang trang. Tra chuoi rong khi dau vao rong —
    /// va chuoi rong KHONG duoc coi la khop voi bat ky ten nao.
    /// </summary>
    public static string BoDau(string? ten)
    {
        if (string.IsNullOrWhiteSpace(ten)) return string.Empty;

        // FormD tach dau ra thanh ky tu rieng de loc bo.
        var tach = ten.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(tach.Length);

        foreach (var c in tach)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            // 'd'/'D' co gach ngang KHONG mang dau phu nen FormD khong tach duoc,
            // phai doi tay.
            if (c == 'đ' || c == 'Đ') { sb.Append('D'); continue; }

            if (char.IsWhiteSpace(c)) { sb.Append(' '); continue; }

            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
        }

        // Gom nhieu dau cach lien tiep ve mot: du lieu that co ca "NGUYEN  VAN A".
        return string.Join(' ', sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Hai ten co cung mot nguoi khong, xet rieng o TEN cua luat gop.
    /// Ten rong thi tra <c>false</c> — thieu du lieu khong phai la bang chung khop.
    /// </summary>
    public static bool CungTen(string? a, string? b)
    {
        var x = BoDau(a);
        var y = BoDau(b);
        return x.Length > 0 && x == y;
    }
}
