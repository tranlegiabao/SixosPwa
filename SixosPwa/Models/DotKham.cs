namespace SixosPwa.Models;

/// <summary>
/// Một dòng = MỘT LẦN ĐẾN khám. Thay cho <c>QL_LichSuKham</c> cũ (bảng 4 cột
/// đếm, đã khai tử) — ba số đếm đó suy thẳng từ bảng này bằng MIN/MAX/COUNT.
///
/// <para>
/// Khóa tự nhiên là <c>(IdCoSo, MaVaoVien)</c>: HIS đẩy lại cả lô cũng không để
/// dòng trùng. Đo thật bên Thiên Nam: 142.895 đợt khám năm 2026 trên 142.542
/// cặp (bệnh nhân, ngày) — một đợt xấp xỉ một ngày, chỉ 353 ca trùng.
/// </para>
/// <para>
/// Khoa và bác sĩ lưu bằng TÊN chứ không bằng ID: ID của HIS không có nghĩa gì
/// bên cổng, và mỗi cơ sở đánh số một kiểu.
/// </para>
/// </summary>
public class DotKham
{
    public long Id { get; set; }

    public long IdCoSo { get; set; }

    public long IdBenhNhan { get; set; }

    /// <summary>Định danh đợt khám bên HIS — nửa còn lại của khóa tự nhiên.</summary>
    public string MaVaoVien { get; set; } = "";

    // 🔴 Ba cột MaBN / NgayGioRa / ChanDoan đã BỊ BỎ khỏi QL_DotKham (đợt A, §3).
    //    MaBN suy được qua IDBenhNhanCoSo -> DM_BenhNhanCoSo.MaBN, không cần chép lại.
    //    DTO `DotKhamDtos` VẪN GIỮ ba trường này vì HIS đang gửi lên — nhận rồi BỎ.

    public DateTime NgayGioVao { get; set; }

    public string? TenKhoa { get; set; }

    public string? TenBacSi { get; set; }

    public DateTime NgayTao { get; set; } = DateTime.Now;

    public DateTime? NgayCapNhat { get; set; }
}
