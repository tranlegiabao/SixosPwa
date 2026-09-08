namespace SixosPwa.Models;

/// <summary>
/// Con nguoi + ho so cua ho tai co so cua phien dang dang nhap.
///
/// Lop rieng chu khong dung tuple nullable: <c>hoSoInfo.HoSo</c> doc duoc thang
/// sau khi da kiem null, con tuple nullable thi phai <c>.Value.HoSo</c> o moi cho.
/// </summary>
public sealed record HoSoDangDung(BenhNhan BenhNhan, BenhNhanCoSo HoSo);
