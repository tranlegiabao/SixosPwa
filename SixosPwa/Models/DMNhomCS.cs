using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SixosPwa.Models;

[Table("DM_NhomCS")]
public class DMNhomCS
{
    [Key]
    public long ID { get; set; }

    /// <summary>Mã nhóm cơ sở — trước đây nằm rải rác dưới tên LoaiCS.</summary>
    [StringLength(10)]
    public string MaNhom { get; set; } = "";

    [StringLength(30)]
    public string TenNhom { get; set; } = "";

    public bool Active { get; set; } = true;
}
