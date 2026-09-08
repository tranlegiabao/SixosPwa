namespace SixosPwa.Models;

/// <summary>
/// Mot khoa API cap cho MOT co so. Mot co so mang duoc nhieu khoa cung luc nen
/// xoay khoa khong co khoang chet: cap khoa moi, cho HIS doi cau hinh, roi moi
/// tat khoa cu.
///
/// <para>
/// <see cref="KhoaBam"/> la SHA-256 cua khoa tho — trong CSDL khong bao gio co
/// khoa tho. Muon tra cuu thi bam khoa nguoi goi gui len roi so bang bam.
/// </para>
/// <para>
/// 🔴 <see cref="Active"/> o day la cong cua RIENG duong API. No khong lien quan
/// gi toi <c>DM_CSKCB.Active</c> — co ay chi quyet dinh co so co hien o cong
/// cong khai va co nhan dang nhap moi khong (ADR 0013). Tat mot co so khoi trang
/// quang ba KHONG duoc lam HIS cua ho ngung day du lieu.
/// </para>
/// </summary>
public class KhoaApiCoSo
{
    public long Id { get; set; }

    public long IdCoSo { get; set; }

    /// <summary>Nhan de nguoi quan tri biet dang tat/bat khoa nao.</summary>
    public string TenKhoa { get; set; } = "";

    public byte[] KhoaBam { get; set; } = Array.Empty<byte>();

    public bool Active { get; set; } = true;

    public DateTime NgayCap { get; set; } = DateTime.Now;

    public DateTime? NgayHetHan { get; set; }

    /// <summary>Lan cuoi khoa nay duoc dung — de nhan ra khoa da chet ma quen thu hoi.</summary>
    public DateTime? NgayDungCuoi { get; set; }
}
