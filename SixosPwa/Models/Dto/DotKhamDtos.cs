using System.Text.Json.Serialization;

namespace SixosPwa.Models.Dto;

/// <summary>
/// Mot LO dot kham cua MOT benh nhan. Day theo lo chu khong tung dong: luc benh
/// nhan vua noi ho so, HIS phai keo tron qua khu ve — vai chuc dot cho mot nguoi
/// la binh thuong, va tung dong mot se thanh vai chuc cuoc goi.
/// </summary>
public class NhanDotKhamRequest
{
    [JsonPropertyName("maBenhNhan")]
    public string MaBenhNhan { get; set; } = "";

    [JsonPropertyName("dotKham")]
    public List<DotKhamItem> DotKham { get; set; } = new();
}

/// <summary>
/// Mot lan den. Toi thieu theo chot 13 dot 1 — khong cong them truong nao chua
/// co nguoi hoi, va KHONG mang chi tiet tung dong thuoc (don thuoc di duong
/// tai lieu, dang PDF).
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

    /// <summary>Ly do tung dong bi bo qua, de ben HIS khong phai doan.</summary>
    [JsonPropertyName("dongBoQua")]
    public List<string> DongBoQua { get; set; } = new();
}

/// <summary>
/// HIS hoi theo lo: trong danh sach ma benh nhan dang ton o hang doi, ma nao DA
/// co nguoi nhan ben cong?
///
/// <para>
/// Vi sao can cua nay: cong TU CHOI tai lieu cua ma benh nhan chua ai noi ho so
/// (chot 3). Khong co duong hoi nguoc thi bo dem ton ben HIS khong bao gio ve 0
/// va nguoi o quay se hoc cach phot lo no.
/// </para>
/// <para>
/// Khong ro ri gi moi: co so hoi bang khoa cua chinh minh, va danh sach ma benh
/// nhan cua co so von da la cua ho.
/// </para>
/// </summary>
public class KiemTraNhanRequest
{
    [JsonPropertyName("maBenhNhan")]
    public List<string> MaBenhNhan { get; set; } = new();
}

public class KiemTraNhanResponseData
{
    /// <summary>Chi tra ve nhung ma DA co nguoi nhan — HIS tu suy phan con lai.</summary>
    [JsonPropertyName("daCoNguoiNhan")]
    public List<string> DaCoNguoiNhan { get; set; } = new();

    [JsonPropertyName("soDaHoi")]
    public int SoDaHoi { get; set; }
}
