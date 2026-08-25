using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SixosPwa.Models;

[Table("DM_NhomCS")]
public class DMNhomCS
{
    [Key]
    public long ID { get; set; }

    /// <summary>Ma nhom co so — truoc day nam rai rac duoi ten LoaiCS.</summary>
    [StringLength(10)]
    public string MaNhom { get; set; } = "";

    [StringLength(30)]
    public string TenNhom { get; set; } = "";

    public bool Active { get; set; } = true;
}
