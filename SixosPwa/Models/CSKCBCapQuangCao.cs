namespace SixosPwa.Models;

/// <summary>Cấp quảng cáo một cơ sở đang mua. Thay cho ba cột QC_Cap1/2/3 cũ.</summary>
public class CSKCBCapQuangCao
{
    public long Id { get; set; }
    public long IdCoSo { get; set; }
    public byte Cap { get; set; }
    public bool Active { get; set; } = true;
}
