using System.Text.Json.Serialization;

namespace SixosPwa.Models.Dto;

/// <summary>
/// Một LÔ đợt khám của MỘT bệnh nhân. Đẩy theo lô chứ không từng dòng: lúc bệnh
/// nhân vừa nối hồ sơ, HIS phải kéo trọn qua khứ về — vài chục đợt cho một người
/// là bình thường, và từng dòng một sẽ thành vài chục cuộc gọi.
/// </summary>
public class NhanDotKhamRequest
{
    [JsonPropertyName("maBenhNhan")]
    public string MaBenhNhan { get; set; } = "";

    [JsonPropertyName("dotKham")]
    public List<DotKhamItem> DotKham { get; set; } = new();
}

/// <summary>
/// Một lần đến. Tối thiểu theo chốt 13 đợt 1 — không cõng thêm trường nào chưa
/// có người hỏi, và KHÔNG mang chi tiết từng dòng thuốc (đơn thuốc đi đường
/// tài liệu, dạng PDF).
/// </summary>
/// <summary>
/// 🔴 GIỮ NGUYÊN <c>MaBenhNhan</c>, <c>NgayGioRa</c>, <c>ChanDoan</c> dù ba cột
/// tương ứng trong <c>QL_DotKham</c> đã bị bỏ ở đợt A: HIS đang GỬI LÊN, bỏ tham số
/// là vỡ bên HIS (tiền lệ V14). Nhận rồi BỎ — không INSERT xuống DB.
/// </summary>
public class DotKhamItem
{
    [JsonPropertyName("maVaoVien")]
    public string MaVaoVien { get; set; } = "";

    [JsonPropertyName("ngayGioVao")]
    public DateTime NgayGioVao { get; set; }

    [JsonPropertyName("ngayGioRa")]
    public DateTime? NgayGioRa { get; set; }

    [JsonPropertyName("tenKhoa")]
    public string? TenKhoa { get; set; }

    [JsonPropertyName("tenBacSi")]
    public string? TenBacSi { get; set; }

    [JsonPropertyName("chanDoan")]
    public string? ChanDoan { get; set; }
}

public class NhanDotKhamResponseData
{
    [JsonPropertyName("maBenhNhan")]
    public string MaBenhNhan { get; set; } = "";

    [JsonPropertyName("soDaNhan")]
    public int SoDaNhan { get; set; }

    [JsonPropertyName("soBoQua")]
    public int SoBoQua { get; set; }

    /// <summary>Lý do từng dòng bị bỏ qua, để bên HIS không phải đoán.</summary>
    [JsonPropertyName("dongBoQua")]
    public List<string> DongBoQua { get; set; } = new();
}

/// <summary>
/// HIS hỏi theo lô: trong danh sách mã bệnh nhân đang tồn ở hàng đợi, mã nào ĐÃ
/// có người nhận bên cổng?
///
/// <para>
/// Vì sao cần cửa này: cổng TỪ CHỐI tài liệu của mã bệnh nhân chưa ai nối hồ sơ
/// (chốt 3). Không có đường hỏi ngược thì bộ đếm tồn bên HIS không bao giờ về 0
/// và người ở quầy sẽ học cách phớt lờ nó.
/// </para>
/// <para>
/// Không rò rỉ gì mới: cơ sở hỏi bằng khóa của chính mình, và danh sách mã bệnh
/// nhân của cơ sở vốn đã là của họ.
/// </para>
/// </summary>
public class KiemTraNhanRequest
{
    [JsonPropertyName("maBenhNhan")]
    public List<string> MaBenhNhan { get; set; } = new();
}

public class KiemTraNhanResponseData
{
    /// <summary>Chỉ trả về những mã ĐÃ có người nhận — HIS tự suy phần còn lại.</summary>
    [JsonPropertyName("daCoNguoiNhan")]
    public List<string> DaCoNguoiNhan { get; set; } = new();

    [JsonPropertyName("soDaHoi")]
    public int SoDaHoi { get; set; }
}
