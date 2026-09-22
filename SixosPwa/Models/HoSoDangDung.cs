namespace SixosPwa.Models;

/// <summary>
/// Hồ sơ đang dùng của phiên: từ đợt 1B một dòng <see cref="BenhNhan"/> ĐÃ LÀ
/// "con người + hồ sơ tại cơ sở", nên bản ghi này chỉ còn bọc đúng một mảnh.
///
/// <para>
/// Giữ lại lớp (thay vì trả thẳng <c>BenhNhan</c>) để không phải sửa chữ ký ở
/// hàng chục chỗ gọi; và để sau này còn chỗ gắn thêm dữ liệu phiên.
/// </para>
/// </summary>
public sealed record HoSoDangDung(BenhNhan BenhNhan)
{
    /// <summary>Tương thích ngược: trước 1B đây là mảnh <c>BenhNhanCoSo</c>.</summary>
    public BenhNhan HoSo => BenhNhan;
}
