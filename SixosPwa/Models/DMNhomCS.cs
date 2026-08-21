using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SixosPwa.Models;

[Table("DMNhomCS")]
public class DMNhomCS
{
    [Key]
    public long ID { get; set; }
    
    [StringLength(10)]
    public string? LoaiCS { get; set; }
    
    public int? Active { get; set; }
    
    [StringLength(30)]
    public string? TenLoaiCS { get; set; }
}
