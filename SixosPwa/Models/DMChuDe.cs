using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SixosPwa.Models;

[Table("DM_ChuDe")]
public class DMChuDe
{
    [Key]
    public long ID { get; set; }

    /// <summary>Mã chủ đề — trước đây nằm rải rác dưới tên LoaiND.</summary>
    [StringLength(20)]
    public string MaChuDe { get; set; } = "";

    [StringLength(30)]
    public string TenChuDe { get; set; } = "";

    public bool Active { get; set; } = true;
}
