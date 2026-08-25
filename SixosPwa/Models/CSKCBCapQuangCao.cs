namespace SixosPwa.Models;

/// <summary>Cap quang cao mot co so dang mua. Thay cho ba cot QC_Cap1/2/3 cu.</summary>
public class CSKCBCapQuangCao
{
    public long Id { get; set; }
    public long IdCoSo { get; set; }
    public byte Cap { get; set; }
    public bool Active { get; set; } = true;
}
