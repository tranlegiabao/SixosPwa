namespace SixosPwa.Models;

/// <summary>
/// Ho so dang dung cua phien: tu dot 1B mot dong <see cref="BenhNhan"/> DA LA
/// "con nguoi + ho so tai co so", nen ban ghi nay chi con boc dung mot manh.
///
/// <para>
/// Giu lai lop (thay vi tra thang <c>BenhNhan</c>) de khong phai sua chu ky o
/// hang chuc cho goi; va de sau nay con cho gan them du lieu phien.
/// </para>
/// </summary>
public sealed record HoSoDangDung(BenhNhan BenhNhan)
{
    /// <summary>Tuong thich nguoc: truoc 1B day la manh <c>BenhNhanCoSo</c>.</summary>
    public BenhNhan HoSo => BenhNhan;
}
